#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace MCP
{
	public enum MCPLogLevel
	{
		Quiet = 0,
		Info = 1,
		Verbose = 2
	}

	public static class MCPLog
	{
		private const string EditorPrefsKey = "MDMCP.LogLevel";

		public static MCPLogLevel Level
		{
			get
			{
				int stored = EditorPrefs.GetInt(EditorPrefsKey, (int)MCPLogLevel.Info);
				if (stored < 0 || stored > 2) stored = (int)MCPLogLevel.Info;
				return (MCPLogLevel)stored;
			}
			set
			{
				EditorPrefs.SetInt(EditorPrefsKey, (int)value);
				UpdateMenuChecks();
			}
		}

		static MCPLog()
		{
			// Ensure menu checks are correct after domain reload
			EditorApplication.delayCall += UpdateMenuChecks;
		}

		private static void UpdateMenuChecks()
		{
			try
			{
				Menu.SetChecked("Markdown/MCP Logging/Quiet", Level == MCPLogLevel.Quiet);
				Menu.SetChecked("Markdown/MCP Logging/Info", Level == MCPLogLevel.Info);
				Menu.SetChecked("Markdown/MCP Logging/Verbose", Level == MCPLogLevel.Verbose);
			}
			catch { /* Menu API may not be available in some contexts */ }
		}

		public static void Info(string message)
		{
			if (Level >= MCPLogLevel.Info)
			{
				Debug.Log($"[MDMCP] {message}");
			}
		}

		public static void Warning(string message)
		{
			if (Level >= MCPLogLevel.Info)
			{
				Debug.LogWarning($"[MDMCP] {message}");
			}
		}

		public static void Error(string message)
		{
			Debug.LogError($"[MDMCP] {message}");
		}

		public static void ActionStart(string action, string requestId, string dispatch, string payloadPreview = null)
		{
			if (Level < MCPLogLevel.Info) return;
			if (Level >= MCPLogLevel.Verbose && !string.IsNullOrEmpty(payloadPreview))
			{
				Debug.Log($"[MDMCP][ActionStart] action={action} req={(string.IsNullOrEmpty(requestId) ? "null" : requestId)} dispatch={dispatch} payloadPreview={payloadPreview}");
			}
			else
			{
				Debug.Log($"[MDMCP][ActionStart] action={action} req={(string.IsNullOrEmpty(requestId) ? "null" : requestId)} dispatch={dispatch}");
			}
		}

		public static void ActionEnd(string action, string requestId, bool ok, double durationMs, int warningsCount)
		{
			if (Level < MCPLogLevel.Info) return;
			Debug.Log($"[MDMCP][ActionEnd] action={action} req={(string.IsNullOrEmpty(requestId) ? "null" : requestId)} ok={ok.ToString().ToLower()} warnings={warningsCount} durationMs={durationMs:0.0}");
		}

		public static string BuildPayloadPreviewFromCommandJson(string commandJson, int maxLength = 200)
		{
			try
			{
				var root = JObject.Parse(commandJson);
				if (root.TryGetValue("payload", out var payloadToken) && payloadToken != null && payloadToken.Type != JTokenType.Null)
				{
					string compact = payloadToken.ToString(Formatting.None);
					if (compact.Length > maxLength)
					{
						compact = compact.Substring(0, maxLength) + "...";
					}
					return $"\"{compact}\"";
				}
			}
			catch { }
			return null;
		}

		[MenuItem("Markdown/MCP Logging/Quiet")]
		private static void SetQuiet()
		{
			Level = MCPLogLevel.Quiet;
			Info("Logging level set to Quiet");
		}

		[MenuItem("Markdown/MCP Logging/Info")]
		private static void SetInfo()
		{
			Level = MCPLogLevel.Info;
			Info("Logging level set to Info");
		}

		[MenuItem("Markdown/MCP Logging/Verbose")]
		private static void SetVerbose()
		{
			Level = MCPLogLevel.Verbose;
			Info("Logging level set to Verbose");
		}
	}
}
#endif


