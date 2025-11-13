using MCP;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MCP.Actions
{
	public class WaitAction : IEditorAction
	{
		public string ActionName => "wait";

		public object Execute(EditorActionPayload payload)
		{
			float seconds = 0f;
			if (payload?.payload == null) return new { error = "Missing payload for wait." };
			try
			{
				// Accept either a raw number or an object { "seconds": 1.0 }
				if (payload.payload.Type == JTokenType.Float || payload.payload.Type == JTokenType.Integer)
				{
					seconds = payload.payload.ToObject<float>();
				}
				else if (payload.payload.Type == JTokenType.Object)
				{
					var obj = (JObject)payload.payload;
					var tok = obj["seconds"];
					if (tok == null) return new { error = "Expected number or { \"seconds\": <number> }" };
					seconds = tok.ToObject<float>();
				}
				else
				{
					return new { error = "Expected number or { \"seconds\": <number> }" };
				}
			}
			catch
			{
				return new { error = "Wait payload must be a number (seconds) or object with 'seconds'." };
			}
			if (seconds <= 0)
				return new { error = "Wait duration must be a positive number." };

			Task.Delay((int)(seconds * 1000)).Wait();
			return new { status = "OK", message = $"Waited for {seconds} seconds." };
		}
	}
}


