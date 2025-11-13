#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using System.Linq;
using MCP; // Use the new generic namespace
using Newtonsoft.Json; // Use Newtonsoft for more robust JSON handling
using System.Threading;
using Newtonsoft.Json.Linq;

[InitializeOnLoad]
public static class MDMCPServerUnity
{
    private static readonly HttpListener listener = new HttpListener();
    private static string _lastContextJson = "{}";
    private static readonly object _lock = new object();
    private static readonly Dictionary<string, IEditorAction> _actions = new Dictionary<string, IEditorAction>();
    private static Thread _serverThread;
    private static object _lastActionResponse; // last action envelope (ok/result/error)

	private const string AutoStartKey = "MDMCP.AutoStart";
	private const string PortKey = "MDMCP.Port";
	private const string RefreshOnceKey = "MDMCP.RefreshAndWaitOnFirstRequest";
	private const string FirstWaitSecKey = "MDMCP.FirstRequestMaxWaitSeconds";

	public static bool IsRunning => listener.IsListening;

	private static string _instanceId = System.Guid.NewGuid().ToString("N");
	private static bool _firstRequestRefreshDone = false;

    static MDMCPServerUnity()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        Selection.selectionChanged += UpdateContext;
        EditorSceneManager.activeSceneChanged += (a, b) => UpdateContext();
        EditorApplication.update += OnEditorUpdate;
        UpdateContext(); // Initial context update
		// Auto-start based on EditorPrefs
		bool autoStart = EditorPrefs.GetBool(AutoStartKey, true);
		if (autoStart)
		{
			StartServer(); // Start the server on launch/reload if enabled.
		}
    }

    private static float _lastContextUpdateTime = 0f;
    private const float CONTEXT_UPDATE_INTERVAL = 0.5f; // Update context every 0.5 seconds

    private static void OnEditorUpdate()
    {
        if (UnityEngine.Time.realtimeSinceStartup - _lastContextUpdateTime > CONTEXT_UPDATE_INTERVAL)
        {
            _lastContextUpdateTime = UnityEngine.Time.realtimeSinceStartup;
            UpdateContext();
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
        {
            StopServer();
        }
        else if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            StartServer();
        }
    }

	private static void RegisterActions()
    {
        _actions.Clear();
        var actionTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
			.Where(p => typeof(IEditorAction).IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);

        foreach (var type in actionTypes)
        {
            try
            {
                IEditorAction actionInstance = (IEditorAction)Activator.CreateInstance(type);
				if (!string.IsNullOrEmpty(actionInstance.ActionName))
                {
					string name = actionInstance.ActionName;
					if (_actions.TryGetValue(name, out var existing))
					{
						// Prefer project implementations over package ones on duplicates
						bool existingIsPackage = IsPackageAssembly(existing.GetType().Assembly.GetName().Name);
						bool incomingIsPackage = IsPackageAssembly(type.Assembly.GetName().Name);
						if (existingIsPackage && !incomingIsPackage)
						{
							_actions[name] = actionInstance;
							Debug.Log($"[MDMCP] Using project override for action '{name}' ({type.Name} over {existing.GetType().Name}).");
						}
						// else keep existing
					}
					else
					{
						_actions[name] = actionInstance;
					}
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MDMCP] Failed to instantiate action '{type.Name}': {ex.Message}");
            }
        }
        Debug.Log($"[MDMCP] Registered {_actions.Count} actions.");
    }


	[MenuItem("Markdown/Start MCP Server")]
	public static void StartServer()
    {
        if (listener.IsListening)
        {
            Debug.Log("MCP Server is already running.");
            return;
        }
        
        RegisterActions();
        
		var serverUrl = GetServerURL();
		Debug.Log("Starting MDMCP Server on " + serverUrl);
        listener.Prefixes.Clear();
		listener.Prefixes.Add(serverUrl);
        listener.Start();
        
        _serverThread = new Thread(() => Listen());
        _serverThread.IsBackground = true;
        _serverThread.Start();

		// Register this Unity instance in the local registry for MCP bridges
		try { RegisterUnityInstance(); } catch (Exception ex) { Debug.LogWarning($"[MDMCP] Failed to register instance: {ex.Message}"); }
    }

    [MenuItem("Markdown/Stop MCP Server")]
	public static void StopServer()
    {
        if (listener.IsListening)
        {
            Debug.Log("Stopping MDMCP Server.");
            listener.Stop();
            if (_serverThread != null && _serverThread.IsAlive)
            {
                _serverThread.Abort();
            }
            _serverThread = null;
        }

		// Unregister this Unity instance
		try { UnregisterUnityInstance(); } catch (Exception ex) { Debug.LogWarning($"[MDMCP] Failed to unregister instance: {ex.Message}"); }
    }

    private static void Listen()
    {
        while (listener.IsListening)
        {
            try
            {
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                if (request.HttpMethod == "GET")
                {
                    string contextToSend;
                    lock (_lock) { contextToSend = _lastContextJson; }
                    byte[] buffer = Encoding.UTF8.GetBytes(contextToSend);
                    response.ContentType = "application/json";
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                else if (request.HttpMethod == "POST")
                {
                    string commandJson;
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        commandJson = reader.ReadToEnd();
                    }

                    var command = JsonConvert.DeserializeObject<EditorActionPayload>(commandJson);
                    
                    // Extract requestId and payload.sync (parse independently)
                    string requestId = null;
                    bool syncRequested = false;
                    try
                    {
                        var tempObj = Newtonsoft.Json.Linq.JObject.Parse(commandJson);
                        requestId = tempObj?["requestId"]?.ToString();
                        var syncToken = tempObj?["payload"]?["sync"];
                        if (syncToken != null && syncToken.Type != JTokenType.Null)
                        {
                            // Accept bools and truthy strings like "true"/"True"/"1"
                            if (syncToken.Type == JTokenType.Boolean)
                            {
                                syncRequested = syncToken.Value<bool>();
                            }
                            else
                            {
                                var s = syncToken.ToString();
                                syncRequested = string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "1";
                            }
                        }
                    }
                    catch { /* ignore parse errors; default syncRequested=false */ }
                    
                    object responseObject = null;
                    
                    // Determine if this action should run synchronously
					bool defaultSync = (command.action == "wait" || command.action == "getSceneHierarchy" || command.action == "getGameObjectDetails" || command.action == "getPrefabDetails" || command.action == "takeScreenshot" || command.action == "getContext" || command.action == "listActions" || command.action == "listActionsVerbose" || command.action == "findGameObjects" || command.action == "ping" || command.action == "highlight");
                    bool isWriteAction =
                        (command.action == "modifyPrefab"
                        || command.action == "setProperty"
                        || command.action == "setSerializedProperty"
                        || command.action == "duplicateAsset"
                        || command.action == "createGameObject"
                        || command.action == "instantiatePrefab"
                        || command.action == "setTransform"
                        || command.action == "duplicateGameObject"
                        || command.action == "deleteGameObject"
                        || command.action == "applySceneOperations"
                        || command.action == "addComponent"
                        || command.action == "removeComponent"
                        || command.action == "setParent");
                    // Run write actions synchronously by default to make NL flows reliable (honor explicit payload.sync too)
                    bool shouldSync = syncRequested || defaultSync || isWriteAction;

                    if (shouldSync)
                    {
                        var tcs = new TaskCompletionSource<object>();
                        EditorApplication.delayCall += async () => {
                            try {
                                EnsureRefreshedOnce();
                                // Logging start for synchronous actions
                                var actionNameLocal = command.action;
                                var payloadPreview = MCPLog.BuildPayloadPreviewFromCommandJson(commandJson);
                                MCPLog.ActionStart(actionNameLocal, requestId, "sync", payloadPreview);
                                var start = DateTime.UtcNow;
                                object result = ExecuteAction(commandJson);
                                if (result is Task task)
                                {
                                    await task;
                                    var resultProperty = task.GetType().GetProperty("Result");
                                    result = resultProperty.GetValue(task);
                                }
                                // Wrap legacy responses (that don't use ActionResponse) into envelope
                                object wrappedResponse = ActionResponse.WrapLegacyResponse(result, requestId);
                                lock (_lock) { _lastActionResponse = wrappedResponse; }
                                // After successful write actions, refresh hierarchy visibility immediately
                                if (isWriteAction)
                                {
                                    try { EditorApplication.QueuePlayerLoopUpdate(); EditorApplication.RepaintHierarchyWindow(); } catch { }
                                }
                                // Logging end
                                var durationMs = (DateTime.UtcNow - start).TotalMilliseconds;
                                try
                                {
                                    var j = JObject.FromObject(wrappedResponse);
                                    bool ok = j["ok"]?.Value<bool>() ?? false;
                                    int warningsCount = (j["warnings"] as JArray)?.Count ?? 0;
                                    // Log result path when available for quick diagnostics
                                    try
                                    {
                                        var path = j["result"]?["path"]?.ToString();
                                        if (!string.IsNullOrEmpty(path)) Debug.Log($"[MDMCP] Result path: {path}");
                                    }
                                    catch { }
                                    MCPLog.ActionEnd(actionNameLocal, requestId, ok, durationMs, warningsCount);
									try { TelemetryService.LogActionEvent(actionNameLocal, requestId, ok, durationMs, warningsCount, null, null, "http"); } catch { }
                                }
                                catch
                                {
                                    MCPLog.ActionEnd(actionNameLocal, requestId, true, durationMs, 0);
									try { TelemetryService.LogActionEvent(actionNameLocal, requestId, true, durationMs, 0, null, null, "http"); } catch { }
                                }
                                tcs.SetResult(wrappedResponse);
                            } catch (Exception ex) {
                                object errorResponse = ActionResponse.Error("EXECUTION_ERROR", $"Action execution failed: {ex.Message}", new { exception = ex.ToString() }, requestId);
                                tcs.SetResult(errorResponse);
                                MCPLog.ActionEnd(command.action, requestId, false, 0.0, 0);
								try { TelemetryService.LogActionEvent(command.action, requestId, false, 0.0, 0, "EXECUTION_ERROR", ex.GetType().Name, "http"); } catch { }
                            }
                        };
                        tcs.Task.Wait();
                        responseObject = tcs.Task.Result;
                    }
                    else // State-changing actions are dispatched asynchronously.
                    {
                        EditorApplication.delayCall += () => {
                            try {
                                EnsureRefreshedOnce();
                                var actionNameLocal = command.action;
                                var payloadPreview = MCPLog.BuildPayloadPreviewFromCommandJson(commandJson);
                                MCPLog.ActionStart(actionNameLocal, requestId, "async", payloadPreview);
                                var start = DateTime.UtcNow;
                                var result = ExecuteAction(commandJson);
                                // Wrap legacy responses
                                var wrapped = ActionResponse.WrapLegacyResponse(result, requestId);
                                lock (_lock) { _lastActionResponse = wrapped; }
                                var durationMs = (DateTime.UtcNow - start).TotalMilliseconds;
                                try
                                {
                                    var j = JObject.FromObject(wrapped);
                                    bool ok = j["ok"]?.Value<bool>() ?? false;
                                    int warningsCount = (j["warnings"] as JArray)?.Count ?? 0;
                                    MCPLog.ActionEnd(actionNameLocal, requestId, ok, durationMs, warningsCount);
									try { TelemetryService.LogActionEvent(actionNameLocal, requestId, ok, durationMs, warningsCount, null, null, "http"); } catch { }
                                }
                                catch
                                {
                                    MCPLog.ActionEnd(actionNameLocal, requestId, true, durationMs, 0);
									try { TelemetryService.LogActionEvent(actionNameLocal, requestId, true, durationMs, 0, null, null, "http"); } catch { }
                                }
                                // Note: async actions don't return their result, but we still respond with acknowledgment
                            } catch (Exception ex) {
                                Debug.LogError($"[MDMCP] Asynchronous action failed: {ex.Message}");
                                MCPLog.ActionEnd(command.action, requestId, false, 0.0, 0);
								try { TelemetryService.LogActionEvent(command.action, requestId, false, 0.0, 0, "EXECUTION_ERROR", ex.GetType().Name, "http"); } catch { }
                            }
                        };
                        responseObject = ActionResponse.Ok(new { status = "Command received and dispatched to main thread." }, null, requestId);
                    }

                    if (responseObject != null)
                    {
                        string responseString = JsonConvert.SerializeObject(responseObject, Formatting.Indented,
                            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
                        byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                        response.ContentType = "application/json";
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                    }
                }
                response.Close();
            }
            catch (ThreadAbortException) { break; }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                // Dispatch logging to the main thread
                EditorApplication.delayCall += () => Debug.LogError($"MCP Server Error: {ex.Message}");
            }
        }
    }
    
    private static object ExecuteAction(string commandJson)
    {
        try
        {
            var command = JsonConvert.DeserializeObject<EditorActionPayload>(commandJson);

            if (_actions.TryGetValue(command.action, out IEditorAction action))
            {
                return action.Execute(command);
            }
            else
            {
                Debug.LogWarning($"[MDMCP] Unknown action: '{command.action}'");
                return ActionResponse.Error("UNKNOWN_ACTION", $"Unknown action: '{command.action}'", new { availableActions = _actions.Keys.ToArray() });
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MDMCP] Failed to execute action: {ex.Message}");
            return ActionResponse.Error("EXECUTION_ERROR", $"Failed to execute action: {ex.Message}", new { exception = ex.ToString() });
        }
    }

    private static void UpdateContext()
    {
        try
        {
            var contextData = new Dictionary<string, object>();
            string editorState = "Editing";
            if (EditorApplication.isPlaying) editorState = "Playing";
            if (EditorApplication.isPaused) editorState = "Paused";
            contextData["editorState"] = editorState;
            // Editor status flags for bridges to poll readiness
            try { contextData["editorCompiling"] = EditorApplication.isCompiling; } catch { contextData["editorCompiling"] = false; }
            try { contextData["editorUpdating"] = EditorApplication.isUpdating; } catch { contextData["editorUpdating"] = false; }
            
            var activeScene = EditorSceneManager.GetActiveScene();
            contextData["activeScene"] = activeScene.IsValid() ? activeScene.name : "No Active Scene";
            contextData["serverReady"] = listener.IsListening;
            contextData["timestamp"] = DateTime.UtcNow.ToString("o");
            
            var selectionsList = new List<object>();
            foreach (var selectedObject in Selection.objects)
            {
                if (selectedObject is GameObject go)
                {
                    selectionsList.Add(MCPUtils.GenerateContextForGameObject(go));
                }
                else if (AssetDatabase.Contains(selectedObject))
                {
                    selectionsList.Add(MCPUtils.GenerateContextForAsset(selectedObject));
                }
            }
            contextData["selections"] = selectionsList;
            contextData["registeredActionsCount"] = _actions.Count;
            contextData["lastActionResult"] = _lastActionResponse;
            
            lock (_lock)
            {
                _lastContextJson = JsonConvert.SerializeObject(contextData, Formatting.Indented);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MDMCP] Failed to update context: {ex.Message}");
        }
    }

	private static string GetServerURL()
	{
		int port = 43210;
		try
		{
			port = EditorPrefs.GetInt(PortKey, 43210);
			if (port < 1024 || port > 65535) port = 43210;
		}
		catch { port = 43210; }
		return $"http://localhost:{port}/";
	}

	private static void EnsureRefreshedOnce()
	{
		if (_firstRequestRefreshDone) return;
		_firstRequestRefreshDone = true;
		bool enabled = true;
		try { enabled = EditorPrefs.GetBool(RefreshOnceKey, true); } catch { enabled = true; }
		if (!enabled) return;
		try
		{
			AssetDatabase.Refresh();
			float waitSec = 30f;
			try { waitSec = Mathf.Max(0.0f, EditorPrefs.GetFloat(FirstWaitSecKey, 30f)); } catch { waitSec = 30f; }
			double end = EditorApplication.timeSinceStartup + waitSec;
			while ((EditorApplication.isCompiling || EditorApplication.isUpdating) && EditorApplication.timeSinceStartup < end)
			{
				System.Threading.Thread.Sleep(100);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[MDMCP] First-request refresh failed: {ex.Message}");
		}
	}

	private static bool IsPackageAssembly(string assemblyName)
	{
		// Actions compiled from this package have the asmdef name below
		return assemblyName == "Clokk.MDMCP.Editor";
	}

	#region Instance Registry
	private static string GetInstancesRegistryPath()
	{
		string path = null;
#if UNITY_EDITOR_OSX
		string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
		path = System.IO.Path.Combine(home, "Library", "Application Support", "MDMCP", "instances.json");
#elif UNITY_EDITOR_WIN
		string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		path = System.IO.Path.Combine(appData, "MDMCP", "instances.json");
#else
		string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
		path = System.IO.Path.Combine(home, ".local", "share", "MDMCP", "instances.json");
#endif
		try
		{
			var dir = System.IO.Path.GetDirectoryName(path);
			if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
		}
		catch { /* ignore */ }
		return path;
	}

	private static int GetConfiguredPort()
	{
		try
		{
			int port = EditorPrefs.GetInt(PortKey, 43210);
			if (port < 1024 || port > 65535) port = 43210;
			return port;
		}
		catch { return 43210; }
	}

	private static void RegisterUnityInstance()
	{
		string path = GetInstancesRegistryPath();
		var list = new List<Dictionary<string, object>>();
		try
		{
			if (System.IO.File.Exists(path))
			{
				var json = System.IO.File.ReadAllText(path);
				var arr = Newtonsoft.Json.Linq.JArray.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
				foreach (var j in arr)
				{
					try { list.Add(j.ToObject<Dictionary<string, object>>()); } catch { }
				}
			}
		}
		catch { /* ignore read errors */ }

		// Remove any stale entries for this process or same id
		int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
		list = list.Where(e =>
		{
			try
			{
				string id = e.ContainsKey("id") ? e["id"]?.ToString() : e.GetValueOrDefault("instanceId")?.ToString();
				int existingPid = 0;
				if (e.ContainsKey("pid")) int.TryParse(e["pid"]?.ToString(), out existingPid);
				return id != _instanceId && existingPid != pid;
			}
			catch { return true; }
		}).ToList();

		// Add current
		string projectPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
		string projectName = System.IO.Path.GetFileName(projectPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
		string unityAppPath = null;
#if UNITY_EDITOR_OSX
		try
		{
			// applicationContentsPath points to .../Unity.app/Contents
			var contents = EditorApplication.applicationContentsPath;
			if (!string.IsNullOrEmpty(contents))
			{
				unityAppPath = System.IO.Path.GetDirectoryName(contents); // The .app bundle path
			}
		}
		catch { /* ignore */ }
#else
		try { unityAppPath = EditorApplication.applicationPath; } catch { /* ignore */ }
#endif
		var entry = new Dictionary<string, object>
		{
			{ "id", _instanceId },
			{ "pid", pid },
			{ "projectPath", projectPath },
			{ "projectName", string.IsNullOrEmpty(projectName) ? "UnityProject" : projectName },
			{ "port", GetConfiguredPort() },
			{ "unityVersion", Application.unityVersion },
			{ "startedAt", DateTime.UtcNow.ToString("o") },
			{ "unityApplicationPath", unityAppPath }
		};
		list.Add(entry);

		try
		{
			var jsonOut = Newtonsoft.Json.JsonConvert.SerializeObject(list, Newtonsoft.Json.Formatting.Indented);
			System.IO.File.WriteAllText(path, jsonOut);
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[MDMCP] Failed to write instances registry: {ex.Message}");
		}
	}

	private static void UnregisterUnityInstance()
	{
		string path = GetInstancesRegistryPath();
		try
		{
			if (!System.IO.File.Exists(path)) return;
			var json = System.IO.File.ReadAllText(path);
			var arr = Newtonsoft.Json.Linq.JArray.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
			var list = new List<Dictionary<string, object>>();
			int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
			foreach (var j in arr)
			{
				try
				{
					var d = j.ToObject<Dictionary<string, object>>();
					string id = d.ContainsKey("id") ? d["id"]?.ToString() : d.GetValueOrDefault("instanceId")?.ToString();
					int existingPid = 0;
					if (d.ContainsKey("pid")) int.TryParse(d["pid"]?.ToString(), out existingPid);
					if (id == _instanceId || existingPid == pid) continue;
					list.Add(d);
				}
				catch { /* ignore */ }
			}
			var jsonOut = Newtonsoft.Json.JsonConvert.SerializeObject(list, Newtonsoft.Json.Formatting.Indented);
			System.IO.File.WriteAllText(path, jsonOut);
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[MDMCP] Failed to unregister instance: {ex.Message}");
		}
	}
	#endregion
}
#endif


