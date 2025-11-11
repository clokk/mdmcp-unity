using MCP;
using MCP.Payloads;
using UnityEditor;
using UnityEngine;
using System;

namespace MCP.Actions
{
	public class SetSerializedPropertyAction : IEditorAction
	{
		public string ActionName => "setSerializedProperty";

		public object Execute(EditorActionPayload payload)
		{
			var p = payload.payload.ToObject<SetSerializedPropertyPayload>();
			if (p == null)
				return ActionResponse.Error("INVALID_PAYLOAD", "Invalid payload for setSerializedProperty. Expected SetSerializedPropertyPayload.");

			UnityEngine.Object target = null;

			if (!string.IsNullOrEmpty(p.assetPath))
			{
				target = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p.assetPath);
				if (target == null)
					return ActionResponse.Error("ASSET_NOT_FOUND", $"Asset not found at path: {p.assetPath}", new { assetPath = p.assetPath });
			}
			else
			{
				GameObject go = null;
				if (!string.IsNullOrEmpty(p.targetPath))
					go = MCPUtils.FindGameObjectByPath(p.targetPath);
				else if (p.targetInstanceID != 0)
					go = EditorUtility.InstanceIDToObject(p.targetInstanceID) as GameObject;

				if (go == null)
					return ActionResponse.Error("OBJECT_NOT_FOUND", "GameObject not found.", new { targetPath = p.targetPath, targetInstanceID = p.targetInstanceID });

				if (string.IsNullOrEmpty(p.componentName))
					target = go;
				else
				{
					var component = go.GetComponent(p.componentName);
					if (component == null)
						return ActionResponse.Error("COMPONENT_NOT_FOUND", $"Component '{p.componentName}' not found on GameObject.", new { componentName = p.componentName, targetPath = p.targetPath });
					target = component;
				}
			}

			var serializedObject = new SerializedObject(target);
			var serializedProperty = serializedObject.FindProperty(p.propertyPath);
			if (serializedProperty == null)
				return ActionResponse.Error("PROPERTY_NOT_FOUND", $"Property path '{p.propertyPath}' not found.", new { propertyPath = p.propertyPath, targetType = target.GetType().Name });

			try
			{
				Undo.RecordObject(target, $"Set SerializedProperty '{p.propertyPath}'");
				serializedObject.Update();

				if (serializedProperty.propertyType == SerializedPropertyType.Integer)
				{
					if (int.TryParse(p.propertyValue, out int intValue)) serializedProperty.intValue = intValue;
					else return ActionResponse.Error("INVALID_VALUE", $"Cannot parse '{p.propertyValue}' as int.", new { propertyValue = p.propertyValue, propertyType = "Integer" });
				}
				else if (serializedProperty.propertyType == SerializedPropertyType.Float)
				{
					if (float.TryParse(p.propertyValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float floatValue))
						serializedProperty.floatValue = floatValue;
					else return ActionResponse.Error("INVALID_VALUE", $"Cannot parse '{p.propertyValue}' as float.", new { propertyValue = p.propertyValue, propertyType = "Float" });
				}
				else if (serializedProperty.propertyType == SerializedPropertyType.Boolean)
				{
					if (bool.TryParse(p.propertyValue, out bool boolValue)) serializedProperty.boolValue = boolValue;
					else return ActionResponse.Error("INVALID_VALUE", $"Cannot parse '{p.propertyValue}' as bool.", new { propertyValue = p.propertyValue, propertyType = "Boolean" });
				}
				else if (serializedProperty.propertyType == SerializedPropertyType.String)
				{
					serializedProperty.stringValue = p.propertyValue;
				}
				else if (serializedProperty.propertyType == SerializedPropertyType.Enum)
				{
					if (int.TryParse(p.propertyValue, out int enumInt)) serializedProperty.enumValueIndex = enumInt;
					else
					{
						var enumNames = serializedProperty.enumNames;
						for (int i = 0; i < enumNames.Length; i++)
						{
							if (enumNames[i].Equals(p.propertyValue, StringComparison.OrdinalIgnoreCase))
							{
								serializedProperty.enumValueIndex = i;
								break;
							}
						}
					}
				}
				else if (serializedProperty.propertyType == SerializedPropertyType.Vector2)
				{
					var parts = p.propertyValue.Split(',');
					if (parts.Length == 2 &&
						float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
						float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y))
						serializedProperty.vector2Value = new Vector2(x, y);
					else return ActionResponse.Error("INVALID_VALUE", $"Cannot parse '{p.propertyValue}' as Vector2. Expected format: 'x,y'.", new { propertyValue = p.propertyValue });
				}
				else if (serializedProperty.propertyType == SerializedPropertyType.Vector3)
				{
					var parts = p.propertyValue.Split(',');
					if (parts.Length == 3 &&
						float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
						float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y) &&
						float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z))
						serializedProperty.vector3Value = new Vector3(x, y, z);
					else return ActionResponse.Error("INVALID_VALUE", $"Cannot parse '{p.propertyValue}' as Vector3. Expected format: 'x,y,z'.", new { propertyValue = p.propertyValue });
				}
				else if (serializedProperty.propertyType == SerializedPropertyType.ObjectReference)
				{
					UnityEngine.Object objRef = null;
					if (p.propertyValue.StartsWith("Assets/"))
						objRef = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p.propertyValue);
					else if (p.propertyValue.StartsWith("/"))
					{
						var go = MCPUtils.FindGameObjectByPath(p.propertyValue);
						if (go != null) objRef = go;
					}
					if (objRef != null && serializedProperty.objectReferenceValue != objRef)
						serializedProperty.objectReferenceValue = objRef;
				}
				else
				{
					return ActionResponse.Error("UNSUPPORTED_PROPERTY_TYPE", $"Property type '{serializedProperty.propertyType}' is not directly supported. Property path: '{p.propertyPath}'.", new { propertyType = serializedProperty.propertyType.ToString(), propertyPath = p.propertyPath });
				}

				serializedObject.ApplyModifiedProperties();
				EditorUtility.SetDirty(target);
				return ActionResponse.Ok(new { status = "OK", propertyPath = p.propertyPath, propertyType = serializedProperty.propertyType.ToString() });
			}
			catch (Exception ex)
			{
				return ActionResponse.Error("SET_PROPERTY_FAILED", $"Failed to set property: {ex.Message}", new { exception = ex.ToString(), propertyPath = p.propertyPath });
			}
		}
	}
}


