using MCP;
using MCP.Payloads;
using UnityEditor.SceneManagement;
using UnityEditor;

namespace MCP.Actions
{
	public class OpenSceneAction : IEditorAction
	{
		public string ActionName => "openScene";

		public object Execute(EditorActionPayload payload)
		{
			var p = payload.payload.ToObject<OpenScenePayload>();
			if (p != null && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				EditorSceneManager.OpenScene(p.scenePath);
				return new { status = "OK" };
			}
			return new { status = "cancelled" };
		}
	}
}


