using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace MCP.Actions
{
	[MCPAction(Description = "Create a new GameObject in the active scene. Synonyms: new object, spawn empty, create empty", ExamplePayload = "{\"name\":\"Player\",\"parentPath\":\"/\",\"transform\":{\"position\":{\"x\":0,\"y\":1,\"z\":0}}}")]
	[MCPPayloadSchema(typeof(CreateGameObjectPayload))]
	public class CreateGameObjectAction : IEditorAction
	{
		public string ActionName => "createGameObject";

		public object Execute(EditorActionPayload payload)
		{
			try
			{
				var dto = payload.payload?.ToObject<CreateGameObjectPayload>();
				if (dto == null || string.IsNullOrEmpty(dto.name))
				{
					// Diagnostic: show what payload arrived to help debug bridge formatting issues
					try
					{
						UnityEngine.Debug.LogWarning($"[MDMCP][createGameObject] Invalid payload: {payload.payload?.ToString(Newtonsoft.Json.Formatting.None)}");
					}
					catch { /* ignore */ }
					return ActionResponse.Error("INVALID_PAYLOAD", "Missing 'name'");
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

				var go = new GameObject(dto.name);
				// If a parent is provided, ensure we first move the new object to the parent's scene, then parent it.
				if (parent != null)
				{
					try
					{
						if (go.scene != parent.scene)
						{
							UnityEditor.SceneManagement.EditorSceneManager.MoveGameObjectToScene(go, parent.scene);
						}
					}
					catch { /* ignore */ }
					go.transform.SetParent(parent.transform, worldPositionStays: true);
				}
				else
				{
					// Root-level creation: ensure object is in the active scene (handles Prefab Stage or non-active scene contexts)
					var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
					if (go.scene != activeScene)
					{
						UnityEditor.SceneManagement.EditorSceneManager.MoveGameObjectToScene(go, activeScene);
					}
				}

				// Transform
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

				// Layer and tag
				if (!string.IsNullOrEmpty(dto.layerName))
				{
					int layer = LayerMask.NameToLayer(dto.layerName);
					if (layer >= 0) go.layer = layer;
				}
				if (!string.IsNullOrEmpty(dto.tag))
				{
					try { go.tag = dto.tag; } catch { /* ignore invalid tags */ }
				}

				Undo.RegisterCreatedObjectUndo(go, "Create GameObject");
				EditorUtility.SetDirty(go);
				UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
				// Force UI refresh so the Hierarchy reflects the new object immediately
				try
				{
					EditorApplication.QueuePlayerLoopUpdate();
					EditorApplication.RepaintHierarchyWindow();
				}
				catch { /* Editor API differences across versions */ }

				// Auto-highlight newly created object (default on; allow per-call opt-out via payload.highlight=false)
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
					created = true,
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


