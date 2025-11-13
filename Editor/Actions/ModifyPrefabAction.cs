using MCP;
using MCP.Payloads;
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace MCP.Actions
{
	public class ModifyPrefabAction : IEditorAction
	{
		public string ActionName => "modifyPrefab";

		public object Execute(EditorActionPayload payload)
		{
			var p = (payload.payload as Newtonsoft.Json.Linq.JToken)?.ToObject<ModifyPrefabPayload>();
			if (p == null)
				return ActionResponse.Error("INVALID_PAYLOAD", "modifyPrefab requires a payload with prefabPath and modifications");

			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.prefabPath);
			if (prefab == null) return ActionResponse.Error("PREFAB_NOT_FOUND", $"Prefab not found at {p.prefabPath}", new { prefabPath = p.prefabPath });

			// Auto-highlight prefab asset in Project
			bool? highlightOverride = null;
			bool? frameOverride = null;
			try
			{
				var h = payload.payload?["highlight"];
				if (h != null && h.Type != JTokenType.Null) highlightOverride = h.Type == JTokenType.Boolean ? h.Value<bool>() : (bool?)null;
				var hf = payload.payload?["highlightFrame"];
				if (hf != null && hf.Type != JTokenType.Null) frameOverride = hf.Type == JTokenType.Boolean ? hf.Value<bool>() : (bool?)null;
			}
			catch { }
			try
			{
				bool doHl = MCPUtils.ShouldHighlight(highlightOverride, false);
				if (doHl)
				{
					bool frame = MCPUtils.GetFrameSceneViewOverride(frameOverride);
					MCPUtils.Highlight(prefab, frame);
				}
			}
			catch { }

			var results = new List<object>();
			GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

			try
			{
				foreach (var mod in p.modifications)
				{
					Transform targetTransform = null;
					if (!string.IsNullOrEmpty(mod.targetPath))
					{
						var parts = mod.targetPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
						targetTransform = instance.transform;
						foreach (var part in parts)
						{
							targetTransform = targetTransform.Find(part);
							if (targetTransform == null) break;
						}
					}
					if (targetTransform == null && !string.IsNullOrEmpty(mod.targetPath))
					{
						var errorMsg = $"[ModifyPrefab] Could not find target path '{mod.targetPath}'";
						if (p.dryRun) results.Add(new { operation = mod.operation, status = "SKIPPED", reason = errorMsg });
						else
						{
							Debug.LogError(errorMsg);
							results.Add(new { operation = mod.operation, status = "ERROR", error = errorMsg });
						}
						continue;
					}

					GameObject targetObject = targetTransform == null ? instance : targetTransform.gameObject;
					object operationResult = null;

					switch (mod.operation)
					{
						case "ensureChild":
						{
							if (string.IsNullOrEmpty(mod.childName))
							{
								operationResult = new { status = "ERROR", error = "childName is required for ensureChild operation" };
								break;
							}
							var existingChild = targetObject.transform.Find(mod.childName);
							if (existingChild == null)
							{
								if (!p.dryRun)
								{
									GameObject newChild = new GameObject(mod.childName);
									newChild.transform.SetParent(targetObject.transform, false);
									Undo.RegisterCreatedObjectUndo(newChild, "Ensure Child in Prefab");
								}
								operationResult = new { status = p.dryRun ? "WOULD_CREATE" : "CREATED", childName = mod.childName, targetPath = mod.targetPath };
							}
							else
							{
								operationResult = new { status = "EXISTS", childName = mod.childName, targetPath = mod.targetPath };
							}
							break;
						}
						case "addGameObject":
						{
							if (string.IsNullOrEmpty(mod.childName))
							{
								operationResult = new { status = "ERROR", error = "childName is required for addGameObject operation" };
								break;
							}
							if (!p.dryRun)
							{
								GameObject newChild = new GameObject(mod.childName);
								newChild.transform.SetParent(targetObject.transform, false);
								Undo.RegisterCreatedObjectUndo(newChild, "Add GameObject to Prefab");
							}
							operationResult = new { status = p.dryRun ? "WOULD_CREATE" : "CREATED", childName = mod.childName, targetPath = mod.targetPath };
							break;
						}
						case "addComponent":
						{
							var componentType = MCPUtils.FindType(mod.componentName);
							if (componentType == null)
							{
								operationResult = new { status = "ERROR", error = $"Component type '{mod.componentName}' not found" };
								break;
							}
							var existingComponent = targetObject.GetComponent(componentType);
							if (existingComponent == null)
							{
								if (!p.dryRun)
								{
									targetObject.AddComponent(componentType);
									Undo.RegisterCreatedObjectUndo(targetObject, "Add Component to Prefab");
								}
								operationResult = new { status = p.dryRun ? "WOULD_ADD" : "ADDED", componentName = mod.componentName, targetPath = mod.targetPath };
							}
							else
							{
								operationResult = new { status = "EXISTS", componentName = mod.componentName, targetPath = mod.targetPath };
							}
							break;
						}
						case "setProperty":
						{
							var component = targetObject.GetComponent(mod.componentName);
							if (component == null)
							{
								operationResult = new { status = "ERROR", error = $"Component '{mod.componentName}' not found" };
								break;
							}
							if (!p.dryRun)
							{
								Undo.RecordObject(component, "Set Property on Prefab");
								MCPUtils.SetProperty(component, mod.propertyName, mod.propertyValue);
								EditorUtility.SetDirty(component);
							}
							operationResult = new { status = p.dryRun ? "WOULD_SET" : "SET", componentName = mod.componentName, propertyName = mod.propertyName, targetPath = mod.targetPath };
							break;
						}
						case "setSerializedProperty":
						{
							var component = targetObject.GetComponent(mod.componentName);
							if (component == null)
							{
								operationResult = new { status = "ERROR", error = $"Component '{mod.componentName}' not found" };
								break;
							}
							if (string.IsNullOrEmpty(mod.propertyPath))
							{
								operationResult = new { status = "ERROR", error = "propertyPath is required for setSerializedProperty operation" };
								break;
							}
							if (!p.dryRun)
							{
								var serializedObject = new SerializedObject(component);
								var serializedProperty = serializedObject.FindProperty(mod.propertyPath);
								if (serializedProperty == null)
								{
									operationResult = new { status = "ERROR", error = $"Property path '{mod.propertyPath}' not found" };
									break;
								}
								Undo.RecordObject(component, "Set SerializedProperty on Prefab");
								serializedObject.Update();
								SetSerializedPropertyValue(serializedProperty, mod.propertyValue);
								serializedObject.ApplyModifiedProperties();
								EditorUtility.SetDirty(component);
							}
							operationResult = new { status = p.dryRun ? "WOULD_SET" : "SET", componentName = mod.componentName, propertyPath = mod.propertyPath, targetPath = mod.targetPath };
							break;
						}
						case "setReference":
						{
							var sourceComponent = instance.transform.Find(mod.sourcePath)?.GetComponent(mod.sourceComponent);
							var targetRefObject = instance.transform.Find(mod.targetPath)?.gameObject;
							if (sourceComponent == null || targetRefObject == null)
							{
								operationResult = new { status = "ERROR", error = "Source component or target object not found" };
								break;
							}
							if (!p.dryRun)
							{
								Undo.RecordObject(sourceComponent, "Set Reference on Prefab");
								MCPUtils.SetProperty(sourceComponent, mod.propertyName, targetRefObject);
								EditorUtility.SetDirty(sourceComponent);
							}
							operationResult = new { status = p.dryRun ? "WOULD_SET" : "SET", sourcePath = mod.sourcePath, propertyName = mod.propertyName };
							break;
						}
						default:
							operationResult = new { status = "ERROR", error = $"Unknown operation: {mod.operation}" };
							break;
					}

					if (operationResult != null)
						results.Add(operationResult);
				}

				if (!p.dryRun)
				{
					PrefabUtility.SaveAsPrefabAsset(instance, p.prefabPath);
				}
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(instance);
			}

			return ActionResponse.Ok(new
			{
				status = p.dryRun ? "DRY_RUN" : "OK",
				prefabPath = p.prefabPath,
				modificationsCount = p.modifications.Count,
				results = results
			});
		}

		private void SetSerializedPropertyValue(SerializedProperty prop, string value)
		{
			switch (prop.propertyType)
			{
				case SerializedPropertyType.Integer:
					if (int.TryParse(value, out int intVal)) prop.intValue = intVal;
					break;
				case SerializedPropertyType.Float:
					if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float floatVal))
						prop.floatValue = floatVal;
					break;
				case SerializedPropertyType.Boolean:
					if (bool.TryParse(value, out bool boolVal)) prop.boolValue = boolVal;
					break;
				case SerializedPropertyType.String:
					prop.stringValue = value;
					break;
				case SerializedPropertyType.Vector2:
					var v2Parts = value.Split(',');
					if (v2Parts.Length == 2 && float.TryParse(v2Parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x2) &&
						float.TryParse(v2Parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y2))
						prop.vector2Value = new Vector2(x2, y2);
					break;
				case SerializedPropertyType.Vector3:
					var v3Parts = value.Split(',');
					if (v3Parts.Length == 3 && float.TryParse(v3Parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x3) &&
						float.TryParse(v3Parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y3) &&
						float.TryParse(v3Parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z3))
						prop.vector3Value = new Vector3(x3, y3, z3);
					break;
			}
		}
	}
}


