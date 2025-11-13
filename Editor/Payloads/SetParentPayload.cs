using System;

namespace MCP
{
	[Serializable]
	public class SetParentPayload
	{
		public string targetPath; // optional if targetInstanceID provided
		public int targetInstanceID; // optional
		public string parentPath; // optional; "/" means scene root
		public int parentInstanceID; // optional; 0 means scene root
		public bool keepWorldPosition = true; // default true
	}
}


