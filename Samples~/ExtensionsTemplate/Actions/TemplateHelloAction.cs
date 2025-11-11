using MCP;

namespace YourCompany.MDMCP.Extensions.Actions
{
	public class TemplateHelloAction : IEditorAction
	{
		public string ActionName => "templateHello";

		public object Execute(EditorActionPayload payload)
		{
			return ActionResponse.Ok(new { message = "Hello from your project extension!" });
		}
	}
}



