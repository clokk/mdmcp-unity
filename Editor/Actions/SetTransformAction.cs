using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace MCP.Actions
{
	[MCPAction(Description = "Set or adjust a GameObject's transform. Synonyms: move, rotate, scale, reposition")]
	[MCPPayloadSchema(typeof(SetTransformPayload))]
	public class SetTransformAction : IEditorAction
	{
		public string ActionName => "setTransform";

		public object Execute(EditorActionPayload payload)
		{
			try
			{
				var dto = payload.payload?.ToObject<SetTransformPayload>();
				if (dto == null || string.IsNullOrEmpty(dto.targetPath))
				{
					return ActionResponse.Error("INVALID_PAYLOAD", "Missing 'targetPath'");
				}
				var go = MCPUtils.FindGameObjectByPath(dto.targetPath);
				if (go == null) return ActionResponse.Error("TARGET_NOT_FOUND", $"Target not found at '{dto.targetPath}'");
				if (dto.transform == null) return ActionResponse.Error("INVALID_PAYLOAD", "Missing 'transform'");

				// Auto-highlight target before applying
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

				var t = go.transform;
				Undo.RecordObject(t, "Set Transform");

				if (dto.transform.position != null)
				{
					var v = new Vector3(dto.transform.position.x, dto.transform.position.y, dto.transform.position.z);
					t.position = dto.relative ? t.position + v : v;
				}
				if (dto.transform.rotationEuler != null)
				{
					var v = new Vector3(dto.transform.rotationEuler.x, dto.transform.rotationEuler.y, dto.transform.rotationEuler.z);
					if (dto.relative) t.eulerAngles = t.eulerAngles + v;
					else t.eulerAngles = v;
				}
				if (dto.transform.scale != null)
				{
					var v = new Vector3(dto.transform.scale.x, dto.transform.scale.y, dto.transform.scale.z);
					if (dto.relative) t.localScale = Vector3.Scale(t.localScale, v);
					else t.localScale = v;
				}

				EditorUtility.SetDirty(t);
				return ActionResponse.Ok(new
				{
					updated = true,
					path = MCPUtils.GetGameObjectPath(t),
					primaryTargetPath = MCPUtils.GetGameObjectPath(t)
				});
			}
			catch (System.Exception ex)
			{
				return ActionResponse.Error("EXECUTION_ERROR", ex.Message, new { exception = ex.ToString() });
			}
		}
	}
}


