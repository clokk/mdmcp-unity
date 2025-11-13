using System;

namespace MCP
{
	[Serializable]
	public class InstantiatePrefabPayload
	{
		public string assetPath;
		public string parentPath; // optional
		public TransformDto transform; // optional
	}
}


