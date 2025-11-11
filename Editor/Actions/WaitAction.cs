using MCP;
using System.Threading.Tasks;

namespace MCP.Actions
{
	public class WaitAction : IEditorAction
	{
		public string ActionName => "wait";

		public object Execute(EditorActionPayload payload)
		{
			var seconds = payload.payload.ToObject<float>();
			if (seconds <= 0)
				return new { error = "Wait duration must be a positive number." };

			Task.Delay((int)(seconds * 1000)).Wait();
			return new { status = "OK", message = $"Waited for {seconds} seconds." };
		}
	}
}


