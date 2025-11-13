using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace MCP.Actions
{
	[MCPAction(Description = "Delete a GameObject. Synonyms: remove object, destroy object")]
	[MCPPayloadSchema(typeof(DeleteGameObjectPayload))]
	public class DeleteGameObjectAction : IEditorAction
	{
		public string ActionName => "deleteGameObject";

		public object Execute(EditorActionPayload payload)
		{
			try
			{
				var dto = payload.payload?.ToObject<DeleteGameObjectPayload>();
				if (dto == null || string.IsNullOrEmpty(dto.targetPath))
					return ActionResponse.Error("INVALID_PAYLOAD", "Missing 'targetPath'");

				var go = MCPUtils.FindGameObjectByPath(dto.targetPath);
				if (go == null) return ActionResponse.Error("TARGET_NOT_FOUND", $"Target not found at '{dto.targetPath}'");

				// Auto-highlight target before delete (do not re-select after)
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
						MCPUtils.Highlight(go, frame);
					}
				}
				catch { }

				string parentPath = null;
				try { if (go.transform.parent != null) parentPath = MCPUtils.GetGameObjectPath(go.transform.parent); } catch { }

				Undo.DestroyObjectImmediate(go);
				return ActionResponse.Ok(new { deleted = true, primaryTargetPath = dto.targetPath, parentPath = parentPath });
			}
			catch (System.Exception ex)
			{
				return ActionResponse.Error("EXECUTION_ERROR", ex.Message, new { exception = ex.ToString() });
			}
		}
	}
}


