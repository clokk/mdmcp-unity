using System;

namespace MCP.Payloads
{
	[System.Serializable]
	public class FindGameObjectsPayload
	{
		public string namePattern;
		public string tag;
		public int layer = -1;
		public string componentType;
		public bool activeOnly = true;
	}
}


