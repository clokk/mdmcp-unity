using MCP;
using MCP.Payloads;
using UnityEngine;

namespace MCP.Actions
{
	public class GetGameObjectDetailsAction : IEditorAction
	{
		public string ActionName => "getGameObjectDetails";

		public object Execute(EditorActionPayload payload)
		{
			var propertyPayload = payload.payload.ToObject<UniversalSetPropertyPayload>();
			if (propertyPayload == null)
				return ActionResponse.Error("INVALID_PAYLOAD", "Invalid payload for getGameObjectDetails. Expected UniversalSetPropertyPayload.");

			GameObject targetObject = null;
			if (!string.IsNullOrEmpty(propertyPayload.targetPath))
				targetObject = MCPUtils.FindGameObjectByPath(propertyPayload.targetPath);
			else if (propertyPayload.targetInstanceID != 0)
				targetObject = UnityEditor.EditorUtility.InstanceIDToObject(propertyPayload.targetInstanceID) as GameObject;

			if (targetObject != null)
			{
				var contextData = MCPUtils.GenerateContextForGameObject(targetObject);
				return ActionResponse.Ok(contextData);
			}

			UnityEngine.Debug.LogError($"[MDMCP] getGameObjectDetails failed: Could not find GameObject.");
			return ActionResponse.Error("OBJECT_NOT_FOUND", "GameObject not found with the provided path or instanceID.", new { targetPath = propertyPayload.targetPath, targetInstanceID = propertyPayload.targetInstanceID });
		}
	}
}


