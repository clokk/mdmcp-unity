using MCP;
using MCP.InternalPayloads;
using UnityEditor;
using UnityEngine;
using System;
using Newtonsoft.Json.Linq;

namespace MCP.Actions
{
	[MCPAction(Description = "Add a component to a GameObject. Synonyms: attach component, add script", ExamplePayload = "{ \"targetPath\": \"/Player\", \"componentName\": \"Rigidbody\" }")]
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

			// Auto-highlight target before adding component (default on; allow per-call opt-out via payload.highlight=false)
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
				MCPUtils.Highlight(targetObject, frame);
			}

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



