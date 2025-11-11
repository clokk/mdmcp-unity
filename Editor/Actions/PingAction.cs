using MCP;

namespace MCP.Actions
{
	public class PingAction : IEditorAction
	{
		public string ActionName => "ping";

		public object Execute(EditorActionPayload payload)
		{
			return ActionResponse.Ok(new { status = "OK" });
		}
	}
}



