using MCP;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.Linq;

namespace MCP.Actions
{
	public class GetSceneHierarchyAction : IEditorAction
	{
		public string ActionName => "getSceneHierarchy";

		public object Execute(EditorActionPayload payload)
		{
			var scene = EditorSceneManager.GetActiveScene();
			var rootObjects = scene.GetRootGameObjects();
			var hierarchy = rootObjects.Select(go => MCPUtils.BuildHierarchyNode(go.transform)).ToList();
			return ActionResponse.Ok(new { sceneName = scene.name, hierarchy });
		}
	}
}


