using MCP;
using MCP.InternalPayloads;
using UnityEditor;
using UnityEngine;
using System;

namespace MCP.Actions
{
	public class AddComponentAction : IEditorAction
	{
		public string ActionName => "addComponent";

		public object Execute(EditorActionPayload payload)
		{
			var p = payload.payload.ToObject<AddRemoveComponentPayload>();
			if (p == null || string.IsNullOrEmpty(p.componentName))
				return ActionResponse.Error("INVALID_PAYLOAD", "addComponent requires { targetPath|targetInstanceID, componentName }");

			GameObject targetObject = null;
			if (!string.IsNullOrEmpty(p.targetPath)) targetObject = MCPUtils.FindGameObjectByPath(p.targetPath);
			else if (p.targetInstanceID != 0) targetObject = EditorUtility.InstanceIDToObject(p.targetInstanceID) as GameObject;

			if (targetObject == null)
				return ActionResponse.Error("OBJECT_NOT_FOUND", "GameObject not found.", new { p.targetPath, p.targetInstanceID });

			var componentType = MCPUtils.FindType(p.componentName) ?? MCPUtils.FindTypeInAllAssemblies(p.componentName);
			if (componentType == null)
				return ActionResponse.Error("TYPE_NOT_FOUND", $"Component type '{p.componentName}' not found.", new { p.componentName });

			Undo.AddComponent(targetObject, componentType);
			EditorUtility.SetDirty(targetObject);
			return ActionResponse.Ok(new
			{
				status = "OK",
				component = componentType.Name,
				targetPath = MCPUtils.GetGameObjectPath(targetObject.transform)
			});
		}
	}
}



