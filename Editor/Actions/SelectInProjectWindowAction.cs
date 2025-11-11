using MCP;
using UnityEditor;

namespace MCP.Actions
{
	public class SelectInProjectWindowAction : IEditorAction
	{
		public string ActionName => "selectInProjectWindow";

		public object Execute(EditorActionPayload payload)
		{
			var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>((string)payload.payload);
			if (asset != null)
			{
				EditorUtility.FocusProjectWindow();
				Selection.activeObject = asset;
				return new { status = "OK" };
			}
			return new { status = "error", message = "Asset not found." };
		}
	}
}


