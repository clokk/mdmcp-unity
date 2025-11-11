using System;

namespace MCP
{
	/// <summary>
	/// Helper class for creating consistent response envelopes for MCP actions.
	/// </summary>
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

		/// <summary>
		/// Wraps a legacy action result (that doesn't use ActionResponse) into the new envelope format.
		/// If the result already has an 'ok' field, it's returned as-is.
		/// </summary>
		public static object WrapLegacyResponse(object result, string requestId = null)
		{
			if (result == null) return Ok(null, null, requestId);

			// Check if it already has an envelope structure
			var resultType = result.GetType();
			var okProperty = resultType.GetProperty("ok");
			if (okProperty != null)
			{
				// Already wrapped, just add requestId if missing
				var requestIdProperty = resultType.GetProperty("requestId");
				if (requestIdProperty == null || requestIdProperty.GetValue(result) == null)
				{
					// Create a new object with requestId added
					return new
					{
						ok = okProperty.GetValue(result),
						result = resultType.GetProperty("result")?.GetValue(result),
						warnings = resultType.GetProperty("warnings")?.GetValue(result) ?? Array.Empty<string>(),
						error = resultType.GetProperty("error")?.GetValue(result),
						requestId = requestId
					};
				}
				return result;
			}

			// Check if it's an error response (has 'error' field)
			var errorProperty = resultType.GetProperty("error");
			if (errorProperty != null)
			{
				var errorValue = errorProperty.GetValue(result);
				if (errorValue != null)
				{
					var errorType = errorValue.GetType();
					var codeProp = errorType.GetProperty("code") ?? errorType.GetProperty("Code");
					var msgProp = errorType.GetProperty("message") ?? errorType.GetProperty("Message");
					
					string code = codeProp?.GetValue(errorValue)?.ToString() ?? "UNKNOWN_ERROR";
					string message = msgProp?.GetValue(errorValue)?.ToString() ?? errorValue.ToString();
					
					return Error(code, message, errorValue, requestId);
				}
			}

			// Wrap as successful result
			return Ok(result, null, requestId);
		}
	}
}


