using System;

namespace MCP
{
	/// <summary>
	/// Optional attribute to indicate the payload DTO type for an action.
	/// When present, the server can emit a JSON Schema for IDE/MCP auto-complete.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class MCPPayloadSchemaAttribute : Attribute
	{
		public Type PayloadType { get; }

		public MCPPayloadSchemaAttribute(Type payloadType)
		{
			PayloadType = payloadType;
		}
	}
}



