using System;

namespace MCP
{
	public static class ActionResponse
	{
		public static object Ok(object result = null, string[] warnings = null, string requestId = null)
		{
			return new
			{
				ok = true,
				result = result,
				warnings = warnings ?? Array.Empty<string>(),
				requestId = requestId
			};
		}

		public static object Error(string code, string message, object details = null, string requestId = null)
		{
			return new
			{
				ok = false,
				error = new
				{
					code = code,
					message = message,
					details = details
				},
				requestId = requestId
			};
		}
	}
}


