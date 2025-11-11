using System;
using Newtonsoft.Json.Linq; // We need this for the GetPayload<T> method

namespace MCP
{
	[Serializable]
	public class EditorActionPayload
	{
		public string action;
		public JToken payload; // Flexible payload for all actions (legacy fields removed)

		// This helper is no longer needed and was part of the problem.
		// The actions will now handle their own payload conversion.
	}
}


