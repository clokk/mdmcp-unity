using System;

namespace MCP
{
	/// <summary>
	/// Attribute to provide metadata about an MCP action for introspection.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class MCPActionAttribute : Attribute
	{
		public string Category { get; set; } = "Core"; // "Core" or "Project"
		public string Description { get; set; } = "";
		public string ExamplePayload { get; set; } = ""; // JSON example as string

		public MCPActionAttribute(string description = "")
		{
			Description = description;
		}
	}
}


