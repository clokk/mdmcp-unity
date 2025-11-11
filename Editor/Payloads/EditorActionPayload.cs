using System;
using Newtonsoft.Json.Linq;

namespace MCP
{
	[Serializable]
	public class EditorActionPayload
	{
		public string action;
		public JToken payload;
	}
}


