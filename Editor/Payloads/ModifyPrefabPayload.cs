using System.Collections.Generic;

namespace MCP.Payloads
{
	[System.Serializable]
	public class ModifyPrefabPayload
	{
		public string prefabPath;
		public List<PrefabModification> modifications;
		public bool dryRun = false;
	}
}


