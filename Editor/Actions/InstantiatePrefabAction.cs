using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace MCP.Actions
{
	[MCPAction(Description = "Instantiate a prefab asset into the active scene. Synonyms: spawn prefab, place prefab")]
	[MCPPayloadSchema(typeof(InstantiatePrefabPayload))]
	public class InstantiatePrefabAction : IEditorAction
	{
		public string ActionName => "instantiatePrefab";

		public object Execute(EditorActionPayload payload)
		{
			try
			{
				var dto = payload.payload?.ToObject<InstantiatePrefabPayload>();
				if (dto == null || string.IsNullOrEmpty(dto.assetPath))
				{
					return ActionResponse.Error("INVALID_PAYLOAD", "Missing 'assetPath'");
				}

				var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dto.assetPath);
				if (prefab == null)
				{
					return ActionResponse.Error("ASSET_NOT_FOUND", $"Prefab not found at '{dto.assetPath}'");
				}

				GameObject parent = null;
				// Treat "/" or empty/null as "no explicit parent" (root-level)
				if (!string.IsNullOrEmpty(dto.parentPath) && dto.parentPath != "/")
				{
					parent = MCPUtils.FindGameObjectByPath(dto.parentPath);
					if (parent == null)
					{
						return ActionResponse.Error("PARENT_NOT_FOUND", $"Parent not found at path '{dto.parentPath}'");
					}
				}

				GameObject go = null;
				if (parent != null)
				{
					go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
					go.transform.SetParent(parent.transform, true);
				}
				else
				{
					go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
				}

				if (dto.transform != null)
				{
					if (dto.transform.position != null)
					{
						go.transform.position = new Vector3(dto.transform.position.x, dto.transform.position.y, dto.transform.position.z);
					}
					if (dto.transform.rotationEuler != null)
					{
						go.transform.eulerAngles = new Vector3(dto.transform.rotationEuler.x, dto.transform.rotationEuler.y, dto.transform.rotationEuler.z);
					}
					if (dto.transform.scale != null)
					{
						go.transform.localScale = new Vector3(dto.transform.scale.x, dto.transform.scale.y, dto.transform.scale.z);
					}
				}

				Undo.RegisterCreatedObjectUndo(go, "Instantiate Prefab");
				EditorUtility.SetDirty(go);
				EditorSceneManager.MarkSceneDirty(go.scene);

				// Auto-highlight instantiated instance (default on; allow per-call opt-out via payload.highlight=false)
				bool highlight = true;
				try { highlight = EditorPrefs.GetBool("MDMCP.AutoHighlightWriteActions", true); } catch { highlight = true; }
				try
				{
					var h = payload.payload?["highlight"];
					if (h != null && h.Type != JTokenType.Null)
					{
						if (h.Type == JTokenType.Boolean) highlight = h.Value<bool>();
						else
						{
							bool parsed;
							if (bool.TryParse(h.ToString(), out parsed)) highlight = parsed;
						}
					}
				}
				catch { /* ignore */ }
				if (highlight)
				{
					bool frame = true;
					try { frame = EditorPrefs.GetBool("MDMCP.FrameSceneViewOnHighlight", true); } catch { frame = true; }
					MCPUtils.Highlight(go, frame);
				}

				return ActionResponse.Ok(new
				{
					instantiated = true,
					name = go.name,
					path = MCPUtils.GetGameObjectPath(go.transform)
				});
			}
			catch (System.Exception ex)
			{
				return ActionResponse.Error("EXECUTION_ERROR", ex.Message, new { exception = ex.ToString() });
			}
		}
	}
}


