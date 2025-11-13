using System;

namespace MCP
{
	[Serializable]
	public class SetTransformPayload
	{
		public string targetPath;
		public TransformDto transform; // any subset of position/rotationEuler/scale
		public bool relative; // if true, add to current instead of set
	}
}


