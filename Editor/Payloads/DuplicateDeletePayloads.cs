using System;

namespace MCP
{
	[Serializable]
	public class DuplicateGameObjectPayload
	{
		public string targetPath;
		public string newName; // optional
		public string parentPath; // optional
	}

	[Serializable]
	public class DeleteGameObjectPayload
	{
		public string targetPath;
	}
}


