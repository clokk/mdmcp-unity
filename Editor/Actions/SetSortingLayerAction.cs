using MCP;
using MCP.Payloads;
using UnityEditor;
using UnityEngine;

namespace MCP.Actions
{
	public class SetSortingLayerAction : IEditorAction
	{
		public string ActionName => "setSortingLayer";
		
		public object Execute(EditorActionPayload payload)
		{
			var p = payload.payload.ToObject<SetSortingLayerPayload>();
			var go = MCPUtils.FindGameObjectByPath(p.targetPath);
			if (go == null) return ActionResponse.Error("OBJECT_NOT_FOUND", "GameObject not found.", new { targetPath = p.targetPath });

			var component = go.GetComponent(p.componentName) as Renderer;
			if (component == null) return ActionResponse.Error("COMPONENT_NOT_FOUND", $"Renderer component '{p.componentName}' not found.", new { componentName = p.componentName, targetPath = p.targetPath });

			Undo.RecordObject(component, "Set Sorting Layer");
			component.sortingLayerName = p.sortingLayerName;
			component.sortingOrder = p.orderInLayer;
			EditorUtility.SetDirty(component);

			return ActionResponse.Ok(new { status = "OK", sortingLayerName = p.sortingLayerName, orderInLayer = p.orderInLayer });
		}
	}
}


