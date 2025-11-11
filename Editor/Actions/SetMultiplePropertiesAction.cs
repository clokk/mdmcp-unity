using MCP;
using MCP.InternalPayloads;
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace MCP.Actions
{
	public class SetMultiplePropertiesAction : IEditorAction
	{
		public string ActionName => "setMultipleProperties";

		public object Execute(EditorActionPayload payload)
		{
			var p = payload.payload.ToObject<SetMultiplePropertiesPayload>();
			if (p == null)
				return ActionResponse.Error("INVALID_PAYLOAD", "setMultipleProperties requires a payload.");

			UnityEngine.Object target = null;

			if (!string.IsNullOrEmpty(p.assetPath))
			{
				// Asset edit (supports ScriptableObject assets or prefab root object)
				target = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p.assetPath);
				if (target == null)
					return ActionResponse.Error("ASSET_NOT_FOUND", $"Asset not found at path: {p.assetPath}", new { p.assetPath });

				if (!string.IsNullOrEmpty(p.componentName) && target is GameObject prefabRoot)
				{
					var comp = prefabRoot.GetComponent(p.componentName);
					if (comp == null)
						return ActionResponse.Error("COMPONENT_NOT_FOUND", $"Component '{p.componentName}' not found on prefab root.", new { p.componentName, p.assetPath });
					target = comp;
				}
			}
			else
			{
				GameObject go = null;
				if (!string.IsNullOrEmpty(p.targetPath)) go = MCPUtils.FindGameObjectByPath(p.targetPath);
				else if (p.targetInstanceID != 0) go = EditorUtility.InstanceIDToObject(p.targetInstanceID) as GameObject;

				if (go == null)
					return ActionResponse.Error("OBJECT_NOT_FOUND", "GameObject not found.", new { p.targetPath, p.targetInstanceID });

				target = string.IsNullOrEmpty(p.componentName) ? (UnityEngine.Object)go : (UnityEngine.Object)go.GetComponent(p.componentName);
				if (target == null)
					return ActionResponse.Error("COMPONENT_NOT_FOUND", $"Component '{p.componentName}' not found on GameObject.", new { p.componentName, p.targetPath });
			}

			var properties = CollectProperties(p);
			if (properties.Count == 0)
				return ActionResponse.Error("NO_PROPERTIES", "No properties provided to set.");

			Undo.RecordObject(target, "Set Multiple Properties");

			foreach (var kv in properties)
			{
				MCPUtils.SetProperty(target, kv.Key, kv.Value);
			}

			EditorUtility.SetDirty(target);
			if (!string.IsNullOrEmpty(p.assetPath)) AssetDatabase.SaveAssets();

			return ActionResponse.Ok(new
			{
				status = "OK",
				count = properties.Count
			});
		}

		private Dictionary<string, string> CollectProperties(SetMultiplePropertiesPayload p)
		{
			var dict = new Dictionary<string, string>();

			if (p.properties != null && p.properties.Length > 0)
			{
				foreach (var entry in p.properties)
				{
					if (entry != null && !string.IsNullOrEmpty(entry.name))
					{
						dict[entry.name] = entry.value;
					}
				}
			}

			if (p.propertyNames != null && p.propertyValues != null)
			{
				int len = Math.Min(p.propertyNames.Length, p.propertyValues.Length);
				for (int i = 0; i < len; i++)
				{
					var name = p.propertyNames[i];
					if (!string.IsNullOrEmpty(name))
					{
						dict[name] = p.propertyValues[i];
					}
				}
			}

			return dict;
		}
	}
}



