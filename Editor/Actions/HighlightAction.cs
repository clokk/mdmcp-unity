using UnityEditor;
using UnityEngine;
using MCP;

namespace MCP.Actions
{
	[MCPAction(Description = "Highlight a scene object or asset by selecting and pinging it.", ExamplePayload = "{ \"targetPath\": \"/Canvas/MainMenu/PlayButton\", \"frameSceneView\": true }")]
	[MCPPayloadSchema(typeof(HighlightPayload))]
	public class HighlightAction : IEditorAction
	{
		public string ActionName => "highlight";

		public object Execute(EditorActionPayload payload)
		{
			try
			{
				var dto = payload.payload?.ToObject<HighlightPayload>() ?? new HighlightPayload();
				bool frame = true;
				try { frame = EditorPrefs.GetBool("MDMCP.FrameSceneViewOnHighlight", true); } catch { frame = true; }
				if (dto.frameSceneView.HasValue) frame = dto.frameSceneView.Value;

				UnityEngine.Object target = null;
				string selectedPath = null;

				if (!string.IsNullOrEmpty(dto.targetPath))
				{
					var go = MCPUtils.FindGameObjectByPath(dto.targetPath);
					if (go != null)
					{
						target = go;
						selectedPath = MCPUtils.GetGameObjectPath(go.transform);
					}
					else
					{
						// Fallbacks: try with leading slash, then by simple name
						try
						{
							if (!dto.targetPath.StartsWith("/"))
							{
								var alt = "/" + dto.targetPath;
								go = MCPUtils.FindGameObjectByPath(alt);
								if (go != null)
								{
									target = go;
									selectedPath = MCPUtils.GetGameObjectPath(go.transform);
								}
							}
							if (target == null)
							{
								// Attempt by name only (last segment)
								var last = dto.targetPath.Trim('/');
								var parts = last.Split('/');
								var nameOnly = parts.Length > 0 ? parts[parts.Length - 1] : last;
								if (!string.IsNullOrEmpty(nameOnly))
								{
									go = GameObject.Find(nameOnly);
									if (go != null)
									{
										target = go;
										selectedPath = MCPUtils.GetGameObjectPath(go.transform);
									}
								}
							}
						}
						catch { /* ignore */ }
					}
				}
				else if (!string.IsNullOrEmpty(dto.assetPath))
				{
					target = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dto.assetPath);
					selectedPath = dto.assetPath;
				}

				if (target == null)
				{
					return ActionResponse.Error("NOT_FOUND", "Target not found for highlight.", new { dto.targetPath, dto.assetPath });
				}

				MCPUtils.Highlight(target, frame);
				return ActionResponse.Ok(new
				{
					selectedInstanceId = target.GetInstanceID(),
					selectedPath = selectedPath
				});
			}
			catch (System.Exception ex)
			{
				return ActionResponse.Error("EXECUTION_ERROR", ex.Message, new { exception = ex.ToString() });
			}
		}
	}
}


