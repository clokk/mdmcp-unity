using System;

// Note: Internal namespace to avoid collisions with project-defined DTOs
namespace MCP.InternalPayloads
{
	[Serializable]
	public class AddRemoveComponentPayload
	{
		public string targetPath;
		public int targetInstanceID;
		public string componentName;
		public bool all; // for removeComponent: remove all matching instances if true
	}
}



