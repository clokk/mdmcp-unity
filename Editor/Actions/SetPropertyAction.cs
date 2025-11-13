using MCP;
using MCP.Payloads;
using UnityEditor;
using UnityEngine;
using System;
using Newtonsoft.Json.Linq;

namespace MCP.Actions
{
	[MCPAction(Description = "Set a public field or property on a component or object. Synonyms: change value, update property", ExamplePayload = "{ \"targetPath\": \"/Player\", \"componentName\": \"PlayerController\", \"propertyName\": \"speed\", \"propertyValue\": \"10.5\" }")]
	public class SetPropertyAction : IEditorAction
	{
		public string ActionName => "setProperty";

		public object Execute(EditorActionPayload payload)
		{
			var p = payload.payload.ToObject<UniversalSetPropertyPayload>();
			UnityEngine.Object target = null;
			
			if (!string.IsNullOrEmpty(p.assetPath))
			{
				target = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p.assetPath);
				if (target == null) return ActionResponse.Error("ASSET_NOT_FOUND", $"Asset not found at path: {p.assetPath}", new { assetPath = p.assetPath });
			}
			else
			{
				GameObject go = null;
				if (!string.IsNullOrEmpty(p.targetPath)) go = MCPUtils.FindGameObjectByPath(p.targetPath);
				else if (p.targetInstanceID != 0) go = EditorUtility.InstanceIDToObject(p.targetInstanceID) as GameObject;

				if (go == null) return ActionResponse.Error("OBJECT_NOT_FOUND", "GameObject not found.", new { targetPath = p.targetPath, targetInstanceID = p.targetInstanceID });

				target = string.IsNullOrEmpty(p.componentName) ? go : (UnityEngine.Object)go.GetComponent(p.componentName);
				if (target == null) return ActionResponse.Error("COMPONENT_NOT_FOUND", $"Component '{p.componentName}' not found on GameObject.", new { targetPath = p.targetPath, componentName = p.componentName });
			}

			var memberInfo = MCPUtils.FindPropertyOrField(target.GetType(), p.propertyName);
			if (memberInfo == null) return ActionResponse.Error("MEMBER_NOT_FOUND", $"Property or field '{p.propertyName}' not found on target.", new { propertyName = p.propertyName, targetType = target.GetType().Name });

			Type memberType = (memberInfo is System.Reflection.PropertyInfo pi) ? pi.PropertyType : (memberInfo as System.Reflection.FieldInfo).FieldType;
			
			object valueToSet = null;
			if (typeof(UnityEngine.Object).IsAssignableFrom(memberType))
			{
				valueToSet = MCPUtils.FindObjectFromValue(p.propertyValue, memberType);
			}
			else
			{
				valueToSet = MCPUtils.ConvertValue(p.propertyValue, memberType);
			}
			
			// Auto-highlight target (component's GameObject or asset) before applying
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
					var objToSelect = target is Component c ? (UnityEngine.Object)c.gameObject : target;
					MCPUtils.Highlight(objToSelect, frame);
				}
			}
			catch { }

			Undo.RecordObject(target, "Set Property");
			MCPUtils.SetProperty(target, p.propertyName, valueToSet);
			EditorUtility.SetDirty(target);
			
			string primaryPath = null;
			try
			{
				if (target is Component comp && comp.gameObject != null) primaryPath = MCPUtils.GetGameObjectPath(comp.gameObject.transform);
				else if (target is GameObject goRef) primaryPath = MCPUtils.GetGameObjectPath(goRef.transform);
			}
			catch { }
			return ActionResponse.Ok(new { status = "OK", propertyName = p.propertyName, targetName = target.name, primaryTargetPath = primaryPath, assetPath = p.assetPath });
		}
	}
}


