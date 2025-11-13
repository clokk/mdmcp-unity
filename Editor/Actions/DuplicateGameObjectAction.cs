using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace MCP.Actions
{
	[MCPAction(Description = "Duplicate a GameObject. Synonyms: copy object, clone")]
	[MCPPayloadSchema(typeof(DuplicateGameObjectPayload))]
	public class DuplicateGameObjectAction : IEditorAction
	{
		public string ActionName => "duplicateGameObject";

		public object Execute(EditorActionPayload payload)
		{
			try
			{
				var dto = payload.payload?.ToObject<DuplicateGameObjectPayload>();
				if (dto == null || string.IsNullOrEmpty(dto.targetPath))
					return ActionResponse.Error("INVALID_PAYLOAD", "Missing 'targetPath'");

				var source = MCPUtils.FindGameObjectByPath(dto.targetPath);
				if (source == null) return ActionResponse.Error("TARGET_NOT_FOUND", $"Target not found at '{dto.targetPath}'");

				var parent = source.transform.parent;
				if (!string.IsNullOrEmpty(dto.parentPath))
				{
					var explicitParent = MCPUtils.FindGameObjectByPath(dto.parentPath);
					if (explicitParent != null) parent = explicitParent.transform;
				}

				var clone = Object.Instantiate(source);
				clone.name = string.IsNullOrEmpty(dto.newName) ? source.name + " Copy" : dto.newName;
				if (parent != null) clone.transform.SetParent(parent, true);

				Undo.RegisterCreatedObjectUndo(clone, "Duplicate GameObject");
				EditorUtility.SetDirty(clone);

				// Auto-highlight duplicate (after)
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
						MCPUtils.Highlight(clone, frame);
					}
				}
				catch { }

				return ActionResponse.Ok(new
				{
					duplicated = true,
					name = clone.name,
					path = MCPUtils.GetGameObjectPath(clone.transform),
					primaryTargetPath = MCPUtils.GetGameObjectPath(clone.transform)
				});
			}
			catch (System.Exception ex)
			{
				return ActionResponse.Error("EXECUTION_ERROR", ex.Message, new { exception = ex.ToString() });
			}
		}
	}
}


