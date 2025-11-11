using System;

// Note: Internal namespace to avoid collisions with project-defined DTOs
namespace MCP.InternalPayloads
{
	[Serializable]
	public class SetMultiplePropertiesPayload
	{
		public string targetPath;
		public int targetInstanceID;
		public string assetPath;
		public string componentName;

		// Parallel arrays form (for compatibility with some callers)
		public string[] propertyNames;
		public string[] propertyValues;

		// Structured list form (preferred)
		public PropertyEntry[] properties;
	}

	[Serializable]
	public class PropertyEntry
	{
		public string name;
		public string value;
	}
}



