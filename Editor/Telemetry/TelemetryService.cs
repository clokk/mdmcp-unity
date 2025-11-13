#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MCP
{
	public static class TelemetryService
	{
		private const string DisableEnv = "DISABLE_TELEMETRY";
		private static string _installId;
		private static string _sessionId;
		private static string _filePath;
		private static bool _initialized;

		public static void EnsureInitialized()
		{
			if (_initialized) return;
			if (IsDisabled()) { _initialized = true; return; }
			_installId = LoadOrCreateInstallId();
			_sessionId = Guid.NewGuid().ToString("N");
			_filePath = GetLogFilePath();
			_initialized = true;
		}

		public static void LogActionEvent(string actionName, string requestId, bool ok, double durationMs, int warningsCount, string errorCode = null, string errorClass = null, string client = "http")
		{
			try
			{
				if (IsDisabled()) return;
				EnsureInitialized();
				var now = DateTime.UtcNow.ToString("o");
				var payload = new Dictionary<string, object>
				{
					{ "ts", now },
					{ "installId", _installId },
					{ "sessionId", _sessionId },
					{ "unityVersion", Application.unityVersion },
					{ "os", SystemInfo.operatingSystem },
					{ "action", actionName },
					{ "requestId", string.IsNullOrEmpty(requestId) ? null : requestId },
					{ "ok", ok },
					{ "durationMs", durationMs },
					{ "warnings", warningsCount },
					{ "errorCode", string.IsNullOrEmpty(errorCode) ? null : errorCode },
					{ "errorClass", string.IsNullOrEmpty(errorClass) ? null : errorClass },
					{ "client", client }
				};
				var line = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
				AppendLine(line);
			}
			catch { /* ignore telemetry errors */ }
		}

		private static void AppendLine(string line)
		{
			try
			{
				var dir = Path.GetDirectoryName(_filePath);
				if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
				File.AppendAllText(_filePath, line + "\n", Encoding.UTF8);
			}
			catch { /* ignore */ }
		}

		private static bool IsDisabled()
		{
			try
			{
				var v = Environment.GetEnvironmentVariable(DisableEnv);
				return !string.IsNullOrEmpty(v) && (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
			}
			catch { return false; }
		}

		private static string LoadOrCreateInstallId()
		{
			try
			{
				var path = GetInstallIdPath();
				if (File.Exists(path))
				{
					return File.ReadAllText(path).Trim();
				}
				var id = Guid.NewGuid().ToString("N");
				var dir = Path.GetDirectoryName(path);
				if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
				File.WriteAllText(path, id, Encoding.UTF8);
				return id;
			}
			catch { return Guid.NewGuid().ToString("N"); }
		}

		private static string GetInstallIdPath()
		{
#if UNITY_EDITOR_OSX
			var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			return Path.Combine(home, "Library", "Application Support", "MDMCP", "telemetry", "install_id");
#elif UNITY_EDITOR_WIN
			var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			return Path.Combine(appData, "MDMCP", "telemetry", "install_id");
#else
			var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			return Path.Combine(home, ".local", "share", "MDMCP", "telemetry", "install_id");
#endif
		}

		private static string GetLogFilePath()
		{
#if UNITY_EDITOR_OSX
			var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			return Path.Combine(home, "Library", "Application Support", "MDMCP", "telemetry", "events.ndjson");
#elif UNITY_EDITOR_WIN
			var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			return Path.Combine(appData, "MDMCP", "telemetry", "events.ndjson");
#else
			var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			return Path.Combine(home, ".local", "share", "MDMCP", "telemetry", "events.ndjson");
#endif
		}
	}
}
#endif


