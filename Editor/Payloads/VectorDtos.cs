using System;

namespace MCP
{
	[Serializable]
	public class Vector3Dto
	{
		public float x;
		public float y;
		public float z;
	}

	[Serializable]
	public class TransformDto
	{
		public Vector3Dto position; // world position
		public Vector3Dto rotationEuler; // world euler angles
		public Vector3Dto scale; // local scale
	}
}


