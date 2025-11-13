using MCP;
using MCP.InternalPayloads;
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace MCP.Actions
{
	public class RemoveComponentAction : IEditorAction
	{
		public string ActionName => "removeComponent";

		public object Execute(EditorActionPayload payload)
		{
			var p = payload.payload.ToObject<AddRemoveComponentPayload>();
			if (p == null || string.IsNullOrEmpty(p.componentName))
				return ActionResponse.Error("INVALID_PAYLOAD", "removeComponent requires { targetPath|targetInstanceID, componentName, all? }");

			GameObject targetObject = null;
			if (!string.IsNullOrEmpty(p.targetPath)) targetObject = MCPUtils.FindGameObjectByPath(p.targetPath);
			else if (p.targetInstanceID != 0) targetObject = EditorUtility.InstanceIDToObject(p.targetInstanceID) as GameObject;

			if (targetObject == null)
				return ActionResponse.Error("OBJECT_NOT_FOUND", "GameObject not found.", new { p.targetPath, p.targetInstanceID });

			var componentType = MCPUtils.FindType(p.componentName) ?? MCPUtils.FindTypeInAllAssemblies(p.componentName);
			if (componentType == null)
				return ActionResponse.Error("TYPE_NOT_FOUND", $"Component type '{p.componentName}' not found.", new { p.componentName });

			// Auto-highlight target before removal
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
					MCPUtils.Highlight(targetObject, frame);
				}
			}
			catch { }

			var comps = targetObject.GetComponents(componentType);
			if (comps == null || comps.Length == 0)
				return ActionResponse.Error("COMPONENT_NOT_FOUND", $"Component '{p.componentName}' not found on GameObject.", new { p.componentName, target = targetObject.name });

			int removed = 0;
			if (p.all)
			{
				foreach (var c in comps.ToList())
				{
					Undo.DestroyObjectImmediate(c);
					removed++;
				}
			}
			else
			{
				Undo.DestroyObjectImmediate(comps.First());
				removed = 1;
			}

			EditorUtility.SetDirty(targetObject);
			return ActionResponse.Ok(new
			{
				status = "OK",
				removedCount = removed,
				component = componentType.Name,
				targetPath = MCPUtils.GetGameObjectPath(targetObject.transform),
				primaryTargetPath = MCPUtils.GetGameObjectPath(targetObject.transform)
			});
		}
	}
}



