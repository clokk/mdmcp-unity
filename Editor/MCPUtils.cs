using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using System.Globalization;

namespace MCP
{
	public static class MCPUtils
	{
		private static double _lastHighlightTime = 0.0;

		public static void SetProperty(UnityEngine.Object targetObject, string propertyName, string propertyValueRaw)
		{
			var member = targetObject.GetType().GetMember(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.FirstOrDefault();

			if (member != null)
			{
				object valueToSet = null;
				Type memberType = null;

				if (member is PropertyInfo prop) memberType = prop.PropertyType;
				else if (member is FieldInfo field) memberType = field.FieldType;

				try
				{
					if (memberType == typeof(string)) valueToSet = propertyValueRaw;
					else if (memberType == typeof(int)) valueToSet = int.Parse(propertyValueRaw);
					else if (memberType == typeof(float)) valueToSet = float.Parse(propertyValueRaw, System.Globalization.CultureInfo.InvariantCulture);
					else if (memberType == typeof(bool)) valueToSet = bool.Parse(propertyValueRaw);
					else if (memberType.IsEnum) valueToSet = Enum.Parse(memberType, propertyValueRaw, true);
					else if (memberType == typeof(Vector2))
					{
						string[] parts = propertyValueRaw.Split(',');
						if (parts.Length == 2)
						{
							valueToSet = new Vector2(float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
						}
					}
					else if (memberType == typeof(Vector3))
					{
						string[] parts = propertyValueRaw.Split(',');
						if (parts.Length == 3)
						{
							valueToSet = new Vector3(float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
						}
					}
					else if (memberType == typeof(Color))
					{
						string[] parts = propertyValueRaw.Split(',');
						if (parts.Length == 4)
						{
							valueToSet = new Color(float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture));
						}
						else if (parts.Length == 3)
						{
							valueToSet = new Color(float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
						}
					}
				}
				catch (Exception ex)
				{
					Debug.LogError($"[MDMCP] Failed to parse property '{propertyName}' with value '{propertyValueRaw}'. Error: {ex.Message}");
					return;
				}

				if (valueToSet != null)
				{
					if (member is PropertyInfo pi && pi.CanWrite) pi.SetValue(targetObject, valueToSet);
					else if (member is FieldInfo fi) fi.SetValue(targetObject, valueToSet);
					Debug.Log($"[MDMCP] Set property '{propertyName}' on '{targetObject.GetType().Name}'.");
				}
				else
				{
					Debug.LogError($"[MDMCP] Unsupported or invalid value for property '{propertyName}' of type '{memberType}': {propertyValueRaw}");
				}
			}
			else
			{
				Debug.LogError($"[MDMCP] Could not find writable property or field '{propertyName}' on object '{targetObject.name}'.");
			}
		}

		public static void SetProperty(Component component, string propertyName, string propertyValue)
		{
			SetProperty(component, propertyName, (object)propertyValue);
		}

		public static void SetProperty(object target, string propertyName, object propertyValue)
		{
			if (target == null)
			{
				Debug.LogError($"[MCPUtils] SetProperty failed: target object is null.");
				return;
			}

			var type = target.GetType();
			var field = type.GetField(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (field != null)
			{
				try
				{
					object value = ConvertValue(propertyValue, field.FieldType);
					field.SetValue(target, value);
					Debug.Log($"[MCPUtils] Set field '{propertyName}' on {target.GetType().Name} to '{value}'.");
					if (target is UnityEngine.Object unityObject) EditorUtility.SetDirty(unityObject);
					return;
				}
				catch (Exception e)
				{
					Debug.LogError($"[MCPUtils] Failed to set field '{propertyName}' on {target.GetType().Name}: {e.Message}");
					return;
				}
			}

			var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (prop != null && prop.CanWrite)
			{
				try
				{
					object value = ConvertValue(propertyValue, prop.PropertyType);
					prop.SetValue(target, value, null);
					Debug.Log($"[MCPUtils] Set property '{propertyName}' on {target.GetType().Name} to '{value}'.");
					if (target is UnityEngine.Object unityObject) EditorUtility.SetDirty(unityObject);
					return;
				}
				catch (Exception e)
				{
					Debug.LogError($"[MCPUtils] Failed to set property '{propertyName}' on {target.GetType().Name}: {e.Message}");
					return;
				}
			}

			Debug.LogWarning($"[MCPUtils] Could not find settable property or field '{propertyName}' on type {target.GetType().Name}.");
		}

		public static object ConvertValue(object value, Type targetType)
		{
			if (value == null) return null;

			// If the value is already assignable to the target type (e.g., it's a GameObject), just return it.
			if (targetType.IsAssignableFrom(value.GetType()))
			{
				return value;
			}

			// NEW: Special case for converting a GameObject to a Component on that GameObject.
			if (value is GameObject go && typeof(Component).IsAssignableFrom(targetType))
			{
				Component component = go.GetComponent(targetType);
				if (component != null) return component;
			}

			// If the value is a string, attempt to parse it.
			if (value is string stringValue)
			{
				if (targetType == typeof(string)) return stringValue;
				if (targetType == typeof(int)) return int.Parse(stringValue);
				if (targetType == typeof(float)) return float.Parse(stringValue, System.Globalization.CultureInfo.InvariantCulture);
				if (targetType == typeof(bool)) return bool.Parse(stringValue);
				if (targetType == typeof(Vector2))
				{
					var parts = stringValue.Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
					return new Vector2(parts[0], parts[1]);
				}
				if (targetType == typeof(Vector2Int))
				{
					var parts = stringValue.Split(',').Select(s => int.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
					return new Vector2Int(parts[0], parts[1]);
				}
				if (targetType == typeof(Vector3))
				{
					var parts = stringValue.Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
					return new Vector3(parts[0], parts[1], parts[2]);
				}
				if (targetType == typeof(Color))
				{
					var parts = stringValue.Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
					return new Color(parts[0], parts[1], parts[2], parts.Length > 3 ? parts[3] : 1.0f);
				}
				if (targetType.IsEnum)
				{
					return Enum.Parse(targetType, stringValue, true);
				}
				if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
				{
					return AssetDatabase.LoadAssetAtPath(stringValue, targetType);
				}
			}

			// Fallback for other types
			return Convert.ChangeType(value, targetType);
		}

		public static Dictionary<string, object> GenerateContextForGameObject(GameObject go)
		{
			var contextData = new Dictionary<string, object>
			{
				["type"] = "GameObject",
				["instanceID"] = go.GetInstanceID(),
				["name"] = go.name,
				["path"] = GetGameObjectPath(go.transform),
				["isActiveInHierarchy"] = go.activeInHierarchy
			};
			var componentsList = new List<object>();
			foreach (Component component in go.GetComponents<Component>())
			{
				if (component != null) componentsList.Add(SerializeComponent(component));
			}
			contextData["components"] = componentsList;
			return contextData;
		}

		public static string GetGameObjectPath(Transform t)
		{
			if (t.parent == null) return "/" + t.name;
			return GetGameObjectPath(t.parent) + "/" + t.name;
		}

		/// <summary>
		/// Finds a GameObject by its hierarchical path (e.g., "/Root/Child/Sub").
		/// Correctly handles paths with or without leading slashes and traverses the hierarchy segment by segment.
		/// </summary>
		public static GameObject FindGameObjectByPath(string path)
		{
			if (string.IsNullOrEmpty(path)) return null;

			var trimmed = path.TrimStart('/');
			if (string.IsNullOrEmpty(trimmed)) return null;

			var parts = trimmed.Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 0) return null;

			// Get active scene root objects
			var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
			if (!scene.IsValid()) return null;

			GameObject current = null;
			var rootObjects = scene.GetRootGameObjects();

			// Find the root object matching the first segment
			foreach (var root in rootObjects)
			{
				if (root.name == parts[0])
				{
					current = root;
					break;
				}
			}

			if (current == null) return null;

			// Traverse down the hierarchy for remaining segments
			for (int i = 1; i < parts.Length; i++)
			{
				var next = current.transform.Find(parts[i]);
				if (next == null) return null;
				current = next.gameObject;
			}

			return current;
		}

		public static Dictionary<string, object> BuildHierarchyNode(Transform t)
		{
			var node = new Dictionary<string, object>
			{
				["name"] = t.gameObject.name,
				["instanceID"] = t.gameObject.GetInstanceID()
			};

			var children = new List<object>();
			foreach (Transform child in t)
			{
				children.Add(BuildHierarchyNode(child));
			}
			node["children"] = children;

			return node;
		}

		public static Dictionary<string, object> SerializeComponent(Component component)
		{
			var componentData = new Dictionary<string, object>
			{
				["type"] = component.GetType().Name
			};
			if (component == null) return componentData;

			// --- Handle specific types ---
			if (component is Transform t)
			{
				componentData["position"] = Jsonify(t.position);
				componentData["rotation"] = Jsonify(t.eulerAngles);
				componentData["scale"] = Jsonify(t.localScale);
			}
			if (component is RectTransform rt)
			{
				componentData["anchoredPosition"] = Jsonify(rt.anchoredPosition);
				componentData["sizeDelta"] = Jsonify(rt.sizeDelta);
			}

			if (component is UnityEngine.UI.Image image)
			{
				componentData["color"] = Jsonify(image.color);
				if (image.sprite != null) componentData["sprite"] = image.sprite.name;
			}

			if (component is SpriteRenderer sr)
			{
				componentData["color"] = Jsonify(sr.color);
			}

			if (component is UnityEngine.UI.Text text)
			{
				componentData["text"] = text.text;
				componentData["font"] = text.font?.name;
				componentData["fontSize"] = text.fontSize;
			}

			// --- Generic MonoBehaviour public field serialization ---
			if (component is MonoBehaviour)
			{
				var fields = component.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
				foreach (var field in fields)
				{
					if (field.IsDefined(typeof(ObsoleteAttribute), true)) continue;

					try
					{
						var value = field.GetValue(component);
						var serializedValue = TrySerializeValue(value);
						if (serializedValue != null)
						{
							componentData[field.Name] = serializedValue;
						}
					}
					catch { /* Ignore fields that can't be read */ }
				}

				var properties = component.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
				foreach (var prop in properties)
				{
					if (!prop.CanRead || prop.IsDefined(typeof(ObsoleteAttribute), true)) continue;
					if (prop.GetIndexParameters().Length > 0) continue; // Skip indexed properties

					try
					{
						var value = prop.GetValue(component);
						var serializedValue = TrySerializeValue(value);
						if (serializedValue != null)
						{
							componentData[prop.Name] = serializedValue;
						}
					}
					catch { /* Ignore properties that can't be read */ }
				}
			}
			return componentData;
		}

		public static object TrySerializeValue(object value)
		{
			if (value == null) return "null";

			Type type = value.GetType();

			if (type.IsPrimitive || value is string) return value;
			if (value is Vector2 v2) return Jsonify(v2);
			if (value is Vector2Int v2i) return Jsonify(v2i);
			if (value is Vector3 v3) return Jsonify(v3);
			if (value is Color c) return Jsonify(c);
			if (type.IsEnum) return value.ToString();

			if (value is UnityEngine.Object unityObject)
			{
				return $"{type.Name} ({unityObject.name})";
			}

			return null; // Don't serialize if we can't easily represent it
		}

		public static Dictionary<string, float> Jsonify(Vector2 v) => new Dictionary<string, float> { { "x", v.x }, { "y", v.y } };
		public static Dictionary<string, int> Jsonify(Vector2Int v) => new Dictionary<string, int> { { "x", v.x }, { "y", v.y } };
		public static Dictionary<string, float> Jsonify(Vector3 v) => new Dictionary<string, float> { { "x", v.x }, { "y", v.y }, { "z", v.z } };
		public static Dictionary<string, float> Jsonify(Color c) => new Dictionary<string, float> { { "r", c.r }, { "g", c.g }, { "b", c.b }, { "a", c.a } };

		/// <summary>
		/// Finds a FieldInfo or PropertyInfo on a given type with robust search flags.
		/// </summary>
		public static MemberInfo FindPropertyOrField(Type type, string memberName)
		{
			const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
			return type.GetMember(memberName, flags).FirstOrDefault();
		}

		/// <summary>
		/// Resolves a string value into a UnityEngine.Object reference.
		/// Handles asset paths and scene object paths.
		/// </summary>
		public static UnityEngine.Object FindObjectFromValue(string value, Type targetType)
		{
			if (string.IsNullOrEmpty(value)) return null;

			// Handle asset paths
			if (value.StartsWith("Assets/"))
			{
				return AssetDatabase.LoadAssetAtPath(value, targetType);
			}

			// Handle scene objects by path
			if (value.StartsWith("/"))
			{
				var go = FindGameObjectByPath(value);
				if (go != null)
				{
					if (targetType == typeof(GameObject))
					{
						return go;
					}
					return go.GetComponent(targetType);
				}
			}
			
			// Handle simple scene objects by name without path (fallback for backward compatibility)
			var sceneGo = FindGameObjectByPath("/" + value);
			if (sceneGo == null)
			{
				// Last resort: use GameObject.Find for simple names
				sceneGo = GameObject.Find(value);
			}
			if (sceneGo != null)
			{
				if (targetType == typeof(GameObject))
				{
					return sceneGo;
				}
				var component = sceneGo.GetComponent(targetType);
				if (component != null)
				{
					return component;
				}
			}

			return null;
		}

		/// <summary>
		/// Searches all loaded assemblies for a type with the given name.
		/// </summary>
		public static Type FindTypeInAllAssemblies(string typeName)
		{
			// First, try a direct lookup, which works for assembly-qualified names
			Type type = Type.GetType(typeName);
			if (type != null)
			{
				return type;
			}

			// If that fails, search all loaded assemblies
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				type = assembly.GetType(typeName);
				if (type != null)
				{
					return type;
				}
			}
	
			// As a final fallback for simple names, try adding the UnityEngine namespace
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				type = assembly.GetType("UnityEngine." + typeName);
				if (type != null)
				{
					return type;
				}
			}


			return null; // Return null if the type is not found
		}
		
		public static Type FindType(string typeName)
		{
			var type = Type.GetType(typeName);
			if (type != null) return type;

			// Handle built-in Unity types that don't have a namespace in the string
			switch (typeName)
			{
				case "RectTransform": return typeof(RectTransform);
				// Add other common types here if needed
			}

			// Search in common namespaces
			if (!typeName.Contains("."))
			{
				// TMPro is a very common case
				type = Type.GetType($"TMPro.{typeName}, Unity.TextMeshPro");
				if (type != null) return type;
			}

			foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
			{
				type = a.GetType(typeName);
				if (type != null)
					return type;
			}
			return null;
		}

		public static Dictionary<string, object> GenerateContextForAsset(UnityEngine.Object asset)
		{
			return new Dictionary<string, object>
			{
				["type"] = "Asset",
				["name"] = asset.name,
				["assetType"] = asset.GetType().Name,
				["path"] = AssetDatabase.GetAssetPath(asset)
			};
		}

		public static void CreateScriptableObjectAsset(string assetTypeStr, string path)
		{
			var assetType = FindType(assetTypeStr);
			if (assetType == null)
			{
				Debug.LogError($"[MDMCP] Type '{assetTypeStr}' not found in any loaded assembly.");
				return;
			}

			if (typeof(ScriptableObject).IsAssignableFrom(assetType))
			{
				var newAsset = ScriptableObject.CreateInstance(assetType);
				AssetDatabase.CreateAsset(newAsset, path);
				Debug.Log($"[MDMCP] Created new {assetTypeStr} at '{path}'");
			}
			else
			{
				Debug.LogError($"[MDMCP] Type '{assetTypeStr}' is not a ScriptableObject.");
			}
		}

		public static void Highlight(UnityEngine.Object target, bool frameSceneView)
		{
			if (target == null) return;
			try
			{
				ThrottleHighlightIfNeeded();
				if (target is GameObject)
				{
					Selection.activeObject = target;
					EditorGUIUtility.PingObject(target);
					if (frameSceneView)
					{
						try { SceneView.FrameLastActiveSceneView(); } catch { /* API differences across versions */ }
					}
				}
				else
				{
					EditorUtility.FocusProjectWindow();
					Selection.activeObject = target;
					EditorGUIUtility.PingObject(target);
				}
			}
			catch { /* ignore */ }
		}

		public static bool HighlightByScenePath(string targetPath, bool frameSceneView)
		{
			if (string.IsNullOrEmpty(targetPath)) return false;
			var go = FindGameObjectByPath(targetPath);
			if (go == null) return false;
			Highlight(go, frameSceneView);
			return true;
		}

		public static void HighlightMany(IEnumerable<UnityEngine.Object> targets, bool frameSceneView, int limit, bool firstOnly)
		{
			if (targets == null) return;
			try
			{
				var list = targets.Where(t => t != null).ToList();
				if (list.Count == 0) return;
				if (firstOnly)
				{
					Highlight(list[0], frameSceneView);
					return;
				}
				if (limit > 0 && list.Count > limit) list = list.Take(limit).ToList();
				ThrottleHighlightIfNeeded();
				Selection.objects = list.ToArray();
				if (frameSceneView)
				{
					try { SceneView.FrameLastActiveSceneView(); } catch { }
				}
				foreach (var t in list)
				{
					try { EditorGUIUtility.PingObject(t); } catch { }
				}
			}
			catch { /* ignore */ }
		}

		public static bool ShouldHighlight(bool? payloadOverride, bool isReadAction)
		{
			// Suppress in play mode if configured
			try
			{
				if (EditorApplication.isPlaying && EditorPrefs.GetBool("MDMCP.SuppressHighlightInPlay", false))
					return false;
			}
			catch { }

			// Global setting based on action type
			bool defaultOn = true;
			try
			{
				if (isReadAction) defaultOn = EditorPrefs.GetBool("MDMCP.AutoHighlightReadActions", true);
				else defaultOn = EditorPrefs.GetBool("MDMCP.AutoHighlightWriteActions", true);
			}
			catch { defaultOn = true; }

			// Per-call override wins
			if (payloadOverride.HasValue) return payloadOverride.Value;
			return defaultOn;
		}

		public static bool GetFrameSceneViewOverride(bool? payloadOverride)
		{
			bool frame = true;
			try { frame = EditorPrefs.GetBool("MDMCP.FrameSceneViewOnHighlight", true); } catch { frame = true; }
			if (payloadOverride.HasValue) return payloadOverride.Value;
			return frame;
		}

		public static (bool firstOnly, int limit) GetMultiSelectPolicy()
		{
			int mode = 0;
			int limit = 8;
			try { mode = EditorPrefs.GetInt("MDMCP.HighlightMultiSelectMode", 0); } catch { mode = 0; }
			try { limit = Mathf.Max(1, EditorPrefs.GetInt("MDMCP.AutoHighlightMultiSelectLimit", 8)); } catch { limit = 8; }
			bool firstOnly = mode == 0;
			return (firstOnly, limit);
		}

		private static void ThrottleHighlightIfNeeded()
		{
			try
			{
				int throttleMs = 150;
				try { throttleMs = Mathf.Max(0, EditorPrefs.GetInt("MDMCP.HighlightThrottleMs", 150)); } catch { throttleMs = 150; }
				double now = EditorApplication.timeSinceStartup;
				if (_lastHighlightTime > 0.0 && throttleMs > 0)
				{
					double nextAllowed = _lastHighlightTime + (throttleMs / 1000.0);
					if (now < nextAllowed)
					{
						int sleep = Mathf.CeilToInt((float)((nextAllowed - now) * 1000.0));
						if (sleep > 0) System.Threading.Thread.Sleep(sleep);
					}
				}
				_lastHighlightTime = EditorApplication.timeSinceStartup;
			}
			catch { /* ignore */ }
		}
	}
}


