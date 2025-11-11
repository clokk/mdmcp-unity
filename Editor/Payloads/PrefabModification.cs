namespace MCP.Payloads
{
	[System.Serializable]
	public class PrefabModification
	{
		public string operation;
		public string targetPath;
		public string componentName;
		public string propertyName;
		public string propertyPath;
		public string propertyValue;
		public string childName;
		public string sourcePath;
		public string sourceComponent;
	}
}


