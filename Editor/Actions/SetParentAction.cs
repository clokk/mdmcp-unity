using UnityEditor;
using UnityEngine;
using MCP;
using Newtonsoft.Json.Linq;

namespace MCP.Actions
{
	public class SetParentAction : IEditorAction
	{
		public string ActionName => "setParent";

		public object Execute(EditorActionPayload payload)
		{
			try
			{
				var p = payload.payload?.ToObject<SetParentPayload>();
				if (p == null) return ActionResponse.Error("INVALID_PAYLOAD", "setParent requires { targetPath|targetInstanceID, parentPath|'/'|parentInstanceID, keepWorldPosition? }");

				GameObject target = null;
				if (!string.IsNullOrEmpty(p.targetPath)) target = MCPUtils.FindGameObjectByPath(p.targetPath);
				else if (p.targetInstanceID != 0) target = EditorUtility.InstanceIDToObject(p.targetInstanceID) as GameObject;
				// Fallback: if path lookup failed but a name was provided, try by name anywhere in the active scene
				if (target == null && !string.IsNullOrEmpty(p.targetPath))
				{
					try
					{
						var last = p.targetPath.Trim('/'); // handle "A/B/C" -> "A/B/C"
						var parts = last.Split('/');
						var nameOnly = parts.Length > 0 ? parts[parts.Length - 1] : last;
						if (!string.IsNullOrEmpty(nameOnly)) target = GameObject.Find(nameOnly);
					}
					catch { /* ignore */ }
				}
				if (target == null) return ActionResponse.Error("TARGET_NOT_FOUND", "Target GameObject not found.", new { p.targetPath, p.targetInstanceID });

				Transform newParent = null;
				// "/" or parentInstanceID==0 means scene root
				if (!string.IsNullOrEmpty(p.parentPath) && p.parentPath != "/")
				{
					var parentGo = MCPUtils.FindGameObjectByPath(p.parentPath);
					if (parentGo == null)
					{
						// Fallback by name for robustness
						try
						{
							var last = p.parentPath.Trim('/');
							var parts = last.Split('/');
							var nameOnly = parts.Length > 0 ? parts[parts.Length - 1] : last;
							if (!string.IsNullOrEmpty(nameOnly)) parentGo = GameObject.Find(nameOnly);
						}
						catch { /* ignore */ }
					}
					if (parentGo == null) return ActionResponse.Error("PARENT_NOT_FOUND", $"Parent not found at '{p.parentPath}'");
					newParent = parentGo.transform;
				}
				else if (p.parentInstanceID != 0)
				{
					var parentGo = EditorUtility.InstanceIDToObject(p.parentInstanceID) as GameObject;
					if (parentGo == null) return ActionResponse.Error("PARENT_NOT_FOUND", $"Parent instance not found: {p.parentInstanceID}");
					newParent = parentGo.transform;
				}
				// else null => set to scene root

				// Highlight before change (default on; allow per-call opt-out via payload.highlight=false)
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
						MCPUtils.Highlight(target, frame);
					}
				}
				catch { }

				// If moving between scenes, ensure target is in the parent's scene first
				if (newParent != null && target.scene != newParent.gameObject.scene)
				{
					UnityEditor.SceneManagement.EditorSceneManager.MoveGameObjectToScene(target, newParent.gameObject.scene);
				}
				Undo.SetTransformParent(target.transform, newParent, "Set Parent");

				EditorUtility.SetDirty(target);
				return ActionResponse.Ok(new
				{
					status = "OK",
					primaryTargetPath = MCPUtils.GetGameObjectPath(target.transform),
					parentPath = newParent != null ? MCPUtils.GetGameObjectPath(newParent) : "/"
				});
			}
			catch (System.Exception ex)
			{
				return ActionResponse.Error("EXECUTION_ERROR", ex.Message, new { exception = ex.ToString() });
			}
		}
	}
}


