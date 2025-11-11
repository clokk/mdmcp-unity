#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class MCPSettingsWindow : EditorWindow
{
	private const string AutoStartKey = "MDMCP.AutoStart";
	private const string PortKey = "MDMCP.Port";

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


