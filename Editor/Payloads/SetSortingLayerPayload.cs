using System;

namespace MCP.Payloads
{
	[Serializable]
	public class SetSortingLayerPayload
	{
		public string targetPath;
		public string componentName;
		public string sortingLayerName;
		public int orderInLayer;
	}
}


