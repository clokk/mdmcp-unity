using System;

namespace MCP
{
	[Serializable]
	public class HighlightPayload
	{
		public string targetPath; // optional: scene hierarchical path like "/Root/Child"
		public string assetPath; // optional: "Assets/..."
		public bool? frameSceneView; // optional override; defaults to settings
	}
}


