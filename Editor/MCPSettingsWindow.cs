#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class MCPSettingsWindow : EditorWindow
{
	private const string AutoStartKey = "MDMCP.AutoStart";
	private const string PortKey = "MDMCP.Port";
	private const string RefreshOnceKey = "MDMCP.RefreshAndWaitOnFirstRequest";
	private const string FirstWaitSecKey = "MDMCP.FirstRequestMaxWaitSeconds";
	private const string AutoHighlightKey = "MDMCP.AutoHighlightWriteActions";
	private const string FrameOnHighlightKey = "MDMCP.FrameSceneViewOnHighlight";
	private const string AutoHighlightReadKey = "MDMCP.AutoHighlightReadActions";
	private const string SuppressInPlayKey = "MDMCP.SuppressHighlightInPlay";
	private const string MultiSelectModeKey = "MDMCP.HighlightMultiSelectMode"; // 0=FirstOnly,1=UpToLimit
	private const string MultiSelectLimitKey = "MDMCP.AutoHighlightMultiSelectLimit";
	private const string HighlightThrottleKey = "MDMCP.HighlightThrottleMs";
	private const string AutoHighlightFindKey = "MDMCP.AutoHighlightFindActions";

	[MenuItem("Markdown/MCP Settings…")]
	public static void ShowWindow()
	{
		var window = GetWindow<MCPSettingsWindow>("MCP Settings");
		window.minSize = new Vector2(360, 240);
		window.Show();
	}

	private void OnGUI()
	{
		GUILayout.Label("MCP Server Settings", EditorStyles.boldLabel);

		// Auto-start toggle
		bool autoStart = EditorPrefs.GetBool(AutoStartKey, true);
		bool newAutoStart = EditorGUILayout.ToggleLeft("Auto-start on domain reload", autoStart);
		if (newAutoStart != autoStart)
		{
			EditorPrefs.SetBool(AutoStartKey, newAutoStart);
		}

		// Port
		int currentPort = EditorPrefs.GetInt(PortKey, 43210);
		int newPort = EditorGUILayout.IntField(new GUIContent("Port", "HTTP listener port (1024–65535)"), currentPort);
		newPort = Mathf.Clamp(newPort, 1024, 65535);
		if (newPort != currentPort)
		{
			EditorPrefs.SetInt(PortKey, newPort);
			// If running, restart on new port
			if (MDMCPServerUnity.IsRunning)
			{
				MDMCPServerUnity.StopServer();
				MDMCPServerUnity.StartServer();
			}
		}

		// First-request refresh/wait
		bool refreshOnce = EditorPrefs.GetBool(RefreshOnceKey, true);
		bool newRefreshOnce = EditorGUILayout.ToggleLeft(new GUIContent("Refresh assets and wait on first request", "On first MCP request after domain reload, force AssetDatabase.Refresh() and wait for compilation/imports to complete."), refreshOnce);
		if (newRefreshOnce != refreshOnce)
		{
			EditorPrefs.SetBool(RefreshOnceKey, newRefreshOnce);
		}
		float currentWait = EditorPrefs.GetFloat(FirstWaitSecKey, 30f);
		float newWait = EditorGUILayout.FloatField(new GUIContent("First-request wait (seconds)", "Maximum time to wait for compile/imports after first request."), currentWait);
		newWait = Mathf.Clamp(newWait, 0f, 300f);
		if (Mathf.Abs(newWait - currentWait) > 0.0001f)
		{
			EditorPrefs.SetFloat(FirstWaitSecKey, newWait);
		}

		EditorGUILayout.Space(6);
		EditorGUILayout.LabelField("Highlight", EditorStyles.boldLabel);
		bool autoHl = EditorPrefs.GetBool(AutoHighlightKey, true);
		bool newAutoHl = EditorGUILayout.ToggleLeft(new GUIContent("Auto-highlight write actions", "After create/instantiate and before add component, select and ping target so users can follow along."), autoHl);
		if (newAutoHl != autoHl)
		{
			EditorPrefs.SetBool(AutoHighlightKey, newAutoHl);
		}
		bool frameOnHl = EditorPrefs.GetBool(FrameOnHighlightKey, true);
		bool newFrameOnHl = EditorGUILayout.ToggleLeft(new GUIContent("Frame Scene view on highlight", "Frame last active Scene view on highlighted GameObjects."), frameOnHl);
		if (newFrameOnHl != frameOnHl)
		{
			EditorPrefs.SetBool(FrameOnHighlightKey, newFrameOnHl);
		}
		bool readHl = EditorPrefs.GetBool(AutoHighlightReadKey, true);
		bool newReadHl = EditorGUILayout.ToggleLeft(new GUIContent("Auto-highlight read actions", "Highlight targets during read/inspect actions like getGameObjectDetails/getPrefabDetails."), readHl);
		if (newReadHl != readHl)
		{
			EditorPrefs.SetBool(AutoHighlightReadKey, newReadHl);
		}
		bool suppressPlay = EditorPrefs.GetBool(SuppressInPlayKey, false);
		bool newSuppressPlay = EditorGUILayout.ToggleLeft(new GUIContent("Suppress highlight in Play Mode", "Avoid visual selection changes during Play Mode."), suppressPlay);
		if (newSuppressPlay != suppressPlay)
		{
			EditorPrefs.SetBool(SuppressInPlayKey, newSuppressPlay);
		}
		bool findHl = EditorPrefs.GetBool(AutoHighlightFindKey, false);
		bool newFindHl = EditorGUILayout.ToggleLeft(new GUIContent("Auto-highlight find results", "When using findGameObjects, select results to follow along (configurable multi-select)."), findHl);
		if (newFindHl != findHl)
		{
			EditorPrefs.SetBool(AutoHighlightFindKey, newFindHl);
		}
		int mode = EditorPrefs.GetInt(MultiSelectModeKey, 0);
		int newMode = EditorGUILayout.Popup(new GUIContent("Multi-select mode", "How to highlight when an action targets multiple objects."), mode, new[] { "FirstOnly", "UpToLimit" });
		if (newMode != mode)
		{
			EditorPrefs.SetInt(MultiSelectModeKey, newMode);
		}
		int limit = Mathf.Clamp(EditorPrefs.GetInt(MultiSelectLimitKey, 8), 1, 256);
		int newLimit = EditorGUILayout.IntField(new GUIContent("Multi-select limit", "Maximum number of objects to select when highlighting multiple."), limit);
		newLimit = Mathf.Clamp(newLimit, 1, 256);
		if (newLimit != limit)
		{
			EditorPrefs.SetInt(MultiSelectLimitKey, newLimit);
		}
		int throttle = Mathf.Clamp(EditorPrefs.GetInt(HighlightThrottleKey, 150), 0, 2000);
		int newThrottle = EditorGUILayout.IntField(new GUIContent("Highlight throttle (ms)", "Minimum milliseconds between highlight operations."), throttle);
		newThrottle = Mathf.Clamp(newThrottle, 0, 2000);
		if (newThrottle != throttle)
		{
			EditorPrefs.SetInt(HighlightThrottleKey, newThrottle);
		}

		// Log level
		var logLevel = MCP.MCPLog.Level;
		var newLevel = (MCP.MCPLogLevel)EditorGUILayout.EnumPopup("Log level", logLevel);
		if (newLevel != logLevel)
		{
			MCP.MCPLog.Level = newLevel;
		}

		EditorGUILayout.Space(8);
		EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);

		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("Open Docs"))
		{
			var docPath = "Packages/com.clokk.mdmcp-unity/Documentation/MDMCPServer.md";
			EditorUtility.RevealInFinder(docPath);
			// Also try open with default app
			EditorUtility.OpenWithDefaultApp(docPath);
		}

		GUI.enabled = !MDMCPServerUnity.IsRunning;
		if (GUILayout.Button("Start Server"))
		{
			MDMCPServerUnity.StartServer();
		}
		GUI.enabled = MDMCPServerUnity.IsRunning;
		if (GUILayout.Button("Stop Server"))
		{
			MDMCPServerUnity.StopServer();
		}
		GUI.enabled = true;
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space(8);
		EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
		EditorGUILayout.LabelField("Server running", MDMCPServerUnity.IsRunning ? "Yes" : "No");
		EditorGUILayout.LabelField("Listening on", $"http://localhost:{EditorPrefs.GetInt(PortKey, 43210)}/");
	}
}
#endif


