using System;

namespace MCP
{
	[Serializable]
	public class CreateGameObjectPayload
	{
		public string name;
		public string parentPath; // optional
		public TransformDto transform; // optional
		public string layerName; // optional
		public string tag; // optional
	}
}


