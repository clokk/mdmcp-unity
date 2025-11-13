using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;
using System;
using System.Threading;

namespace MCP
{
	public class MCPClientSetupWindow : EditorWindow
	{
		private Vector2 _scroll;
		private string _bridgeDir;
		private string _cursorSnippet;
		private string _claudeCmd;
		private string _vscodeSnippet;
		private string _status = "";

		[MenuItem("Markdown/MCP Client Setup…")]
		public static void Open()
		{
			var win = GetWindow<MCPClientSetupWindow>("MCP Client Setup");
			win.minSize = new Vector2(640, 420);
			win.Initialize();
			win.Show();
		}

		private void Initialize()
		{
			try
			{
				var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
				_bridgeDir = Path.Combine(projectRoot, "tools", "mdmcp-mcp-bridge");
				BuildSnippets();
			}
			catch { _bridgeDir = ""; }
		}

		private void BuildSnippets()
		{
			var abs = _bridgeDir.Replace("\\", "/");
			_cursorSnippet = "{\n  \"servers\": {\n    \"unityMCP\": {\n      \"command\": \"uv\",\n      \"args\": [\"--directory\",\"" + abs + "\",\"run\",\"mdmcp-bridge\"],\n      \"type\": \"stdio\"\n    }\n  }\n}";

			_claudeCmd = "claude mcp add --scope user UnityMCP -- uv --directory \"" + abs + "\" run mdmcp-bridge";

			_vscodeSnippet = "{\n  \"servers\": {\n    \"unityMCP\": {\n      \"command\": \"uv\",\n      \"args\": [\"--directory\",\"" + abs + "\",\"run\",\"mdmcp-bridge\"],\n      \"type\": \"stdio\"\n    }\n  }\n}";
		}

		private void OnGUI()
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("MCP Client Setup", EditorStyles.boldLabel);
			EditorGUILayout.Space();
			_scroll = EditorGUILayout.BeginScrollView(_scroll);

			EditorGUILayout.LabelField("Bridge Directory");
			EditorGUI.BeginChangeCheck();
			_bridgeDir = EditorGUILayout.TextField(_bridgeDir);
			if (EditorGUI.EndChangeCheck())
			{
				BuildSnippets();
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Cursor/Windsurf mcp.json snippet");
			if (GUILayout.Button("Copy Cursor snippet"))
			{
				EditorGUIUtility.systemCopyBuffer = _cursorSnippet;
				_status = "Copied Cursor snippet";
			}
			EditorGUILayout.TextArea(_cursorSnippet, GUILayout.MinHeight(100));

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Claude (CLI) add command");
			if (GUILayout.Button("Copy Claude command"))
			{
				EditorGUIUtility.systemCopyBuffer = _claudeCmd;
				_status = "Copied Claude CLI command";
			}
			EditorGUILayout.TextArea(_claudeCmd, GUILayout.MinHeight(40));

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("VS Code MCP servers snippet");
			if (GUILayout.Button("Copy VS Code snippet"))
			{
				EditorGUIUtility.systemCopyBuffer = _vscodeSnippet;
				_status = "Copied VS Code snippet";
			}
			EditorGUILayout.TextArea(_vscodeSnippet, GUILayout.MinHeight(100));

			EditorGUILayout.Space();
			if (GUILayout.Button("Write project mcp.unity.json"))
			{
				try
				{
					var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
					var outPath = Path.Combine(projectRoot, "mcp.unity.json");
					File.WriteAllText(outPath, _cursorSnippet, Encoding.UTF8);
					_status = "Wrote " + outPath;
				}
				catch (Exception ex)
				{
					_status = "Failed to write: " + ex.Message;
				}
			}

			EditorGUILayout.Space();
			if (GUILayout.Button("Test connection (ping)"))
			{
				TestConnection();
			}

			if (!string.IsNullOrEmpty(_status))
			{
				EditorGUILayout.HelpBox(_status, MessageType.Info);
			}

			EditorGUILayout.EndScrollView();
		}

		private void TestConnection()
		{
			_status = "Testing...";
			Repaint();

			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					// Ensure server starts on main thread without blocking UI
					if (!MDMCPServerUnity.IsRunning)
					{
						EditorApplication.delayCall += () =>
						{
							if (!MDMCPServerUnity.IsRunning) MDMCPServerUnity.StartServer();
						};
						Thread.Sleep(300);
					}

					var url = "http://localhost:" + GetPort() + "/";
					var payload = "{\"action\":\"ping\"}";
					var bytes = Encoding.UTF8.GetBytes(payload);
					var req = System.Net.WebRequest.Create(url);
					req.Method = "POST";
					req.ContentType = "application/json";
					req.ContentLength = bytes.Length;
					using (var stream = req.GetRequestStream())
					{
						stream.Write(bytes, 0, bytes.Length);
					}
					string text;
					using (var resp = req.GetResponse())
					using (var rs = resp.GetResponseStream())
					using (var reader = new StreamReader(rs))
					{
						text = reader.ReadToEnd();
					}

					EditorApplication.delayCall += () =>
					{
						_status = "Ping ok: " + (text.Length > 200 ? text.Substring(0, 200) + "..." : text);
						Repaint();
					};
				}
				catch (Exception ex)
				{
					EditorApplication.delayCall += () =>
					{
						_status = "Ping failed: " + ex.Message;
						Repaint();
					};
				}
			});
		}

		private int GetPort()
		{
			try { return EditorPrefs.GetInt("MDMCP.Port", 43210); } catch { return 43210; }
		}
	}
}


