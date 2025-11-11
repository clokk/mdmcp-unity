using System;

namespace MCP
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class MCPActionAttribute : Attribute
	{
		public string Category { get; set; } = "Core";
		public string Description { get; set; } = "";
		public string ExamplePayload { get; set; } = "";

		public MCPActionAttribute(string description = "")
		{
			Description = description;
		}
	}
}


