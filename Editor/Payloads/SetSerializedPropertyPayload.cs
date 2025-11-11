using System;

namespace MCP.Payloads
{
	[System.Serializable]
	public class SetSerializedPropertyPayload
	{
		public string targetPath;
		public int targetInstanceID;
		public string assetPath;
		public string componentName;
		public string propertyPath;
		public string propertyValue;
		public string valueType;
	}
}


