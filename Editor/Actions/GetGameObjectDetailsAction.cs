using MCP;
using MCP.Payloads;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

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
				// Optional highlight for read actions
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
					bool doHl = MCPUtils.ShouldHighlight(highlightOverride, true);
					if (doHl)
					{
						bool frame = MCPUtils.GetFrameSceneViewOverride(frameOverride);
						MCPUtils.Highlight(targetObject, frame);
					}
				}
				catch { }
				var contextData = MCPUtils.GenerateContextForGameObject(targetObject);
				return ActionResponse.Ok(contextData);
			}

			UnityEngine.Debug.LogError($"[MDMCP] getGameObjectDetails failed: Could not find GameObject.");
			return ActionResponse.Error("OBJECT_NOT_FOUND", "GameObject not found with the provided path or instanceID.", new { targetPath = propertyPayload.targetPath, targetInstanceID = propertyPayload.targetInstanceID });
		}
	}
}


