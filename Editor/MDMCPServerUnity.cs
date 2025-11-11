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
using Assets.Editor.MCP.Actions; // To find the actions
using Newtonsoft.Json; // Use Newtonsoft for more robust JSON handling
using System.Threading;
using Newtonsoft.Json.Linq;

[InitializeOnLoad]
public static class MDMCPServerUnity
{
    private static readonly HttpListener listener = new HttpListener();
    private const string ServerURL = "http://localhost:43210/";
    private static string _lastContextJson = "{}";
    private static readonly object _lock = new object();
    private static readonly Dictionary<string, IEditorAction> _actions = new Dictionary<string, IEditorAction>();
    private static Thread _serverThread;
    private static object _lastActionResponse; // last action envelope (ok/result/error)

    static MDMCPServerUnity()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        Selection.selectionChanged += UpdateContext;
        EditorSceneManager.activeSceneChanged += (a, b) => UpdateContext();
        EditorApplication.update += OnEditorUpdate;
        UpdateContext(); // Initial context update
        StartServer(); // Automatically start the server on launch/reload.
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
            .Where(p => typeof(IEditorAction).IsAssignableFrom(p) && !p.IsInterface);

        foreach (var type in actionTypes)
        {
            try
            {
                IEditorAction actionInstance = (IEditorAction)Activator.CreateInstance(type);
                if (!string.IsNullOrEmpty(actionInstance.ActionName))
                {
                    _actions[actionInstance.ActionName] = actionInstance;
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
    private static void StartServer()
    {
        if (listener.IsListening)
        {
            Debug.Log("MCP Server is already running.");
            return;
        }
        
        RegisterActions();
        
        Debug.Log("Starting MDMCP Server on " + ServerURL);
        listener.Prefixes.Clear();
        listener.Prefixes.Add(ServerURL);
        listener.Start();
        
        _serverThread = new Thread(() => Listen());
        _serverThread.IsBackground = true;
        _serverThread.Start();
    }

    [MenuItem("Markdown/Stop MCP Server")]
    private static void StopServer()
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
                    
                    // Extract request ID if present
                    string requestId = null;
                    bool syncRequested = false;
                    if (commandJson.contains("requestId"))
                    {
                        try
                        {
                            var tempObj = JsonConvert.DeserializeObject<dynamic>(commandJson);
                            requestId = tempObj?.requestId?.ToString();
                            // payload.sync support
                            try { syncRequested = (bool)(tempObj?.payload?.sync ?? false); } catch { syncRequested = false; }
                        }
                        catch { }
                    }
                    
                    object responseObject = null;
                    
                    // Determine if this action should run synchronously
                    bool defaultSync = (command.action == "wait" || command.action == "getSceneHierarchy" || command.action == "getGameObjectDetails" || command.action == "getPrefabDetails" || command.action == "takeScreenshot" || command.action == "getContext" || command.action == "listActions" || command.action == "findGameObjects");
                    bool isWriteAction = (command.action == "modifyPrefab" || command.action == "setProperty" || command.action == "setSerializedProperty" || command.action == "duplicateAsset");
                    bool shouldSync = defaultSync || (isWriteAction && syncRequested);

                    if (shouldSync)
                    {
                        var tcs = new TaskCompletionSource<object>();
                        EditorApplication.delayCall += async () => {
                            try {
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
                                // Logging end
                                var durationMs = (DateTime.UtcNow - start).TotalMilliseconds;
                                try
                                {
                                    var j = JObject.FromObject(wrappedResponse);
                                    bool ok = j["ok"]?.Value<bool>() ?? false;
                                    int warningsCount = (j["warnings"] as JArray)?.Count ?? 0;
                                    MCPLog.ActionEnd(actionNameLocal, requestId, ok, durationMs, warningsCount);
                                }
                                catch
                                {
                                    MCPLog.ActionEnd(actionNameLocal, requestId, true, durationMs, 0);
                                }
                                tcs.SetResult(wrappedResponse);
                            } catch (Exception ex) {
                                object errorResponse = ActionResponse.Error("EXECUTION_ERROR", $"Action execution failed: {ex.Message}", new { exception = ex.ToString() }, requestId);
                                tcs.SetResult(errorResponse);
                                MCPLog.ActionEnd(command.action, requestId, false, 0.0, 0);
                            }
                        };
                        tcs.Task.Wait();
                        responseObject = tcs.Task.Result;
                    }
                    else // State-changing actions are dispatched asynchronously.
                    {
                        EditorApplication.delayCall += () => {
                            try {
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
                                }
                                catch
                                {
                                    MCPLog.ActionEnd(actionNameLocal, requestId, true, durationMs, 0);
                                }
                                // Note: async actions don't return their result, but we still respond with acknowledgment
                            } catch (Exception ex) {
                                Debug.LogError($"[MDMCP] Asynchronous action failed: {ex.Message}");
                                MCPLog.ActionEnd(command.action, requestId, false, 0.0, 0);
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
}
#endif


