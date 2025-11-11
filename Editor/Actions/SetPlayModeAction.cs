using MCP;
using UnityEditor;

namespace MCP.Actions
{
	public class SetPlayModeAction : IEditorAction
	{
		public string ActionName => "setPlayMode";

		public object Execute(EditorActionPayload payload)
		{
			var isPlaying = payload.payload.ToObject<bool>();
			EditorApplication.isPlaying = isPlaying;
			return new { status = "OK", isPlaying = EditorApplication.isPlaying };
		}
	}
}


