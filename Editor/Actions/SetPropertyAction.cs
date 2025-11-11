using MCP;
using MCP.Payloads;
using UnityEditor;
using UnityEngine;
using System;

namespace MCP.Actions
{
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
			
			Undo.RecordObject(target, "Set Property");
			MCPUtils.SetProperty(target, p.propertyName, valueToSet);
			EditorUtility.SetDirty(target);
			
			return ActionResponse.Ok(new { status = "OK", propertyName = p.propertyName, targetName = target.name });
		}
	}
}


