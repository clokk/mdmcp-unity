using System;

namespace MCP.Payloads
{
	[System.Serializable]
	public class UniversalSetPropertyPayload
	{
		public int targetInstanceID;
		public string targetPath;
		public string assetPath;
		public string componentName;
		public string propertyName;
		public string propertyValue;
	}
}


