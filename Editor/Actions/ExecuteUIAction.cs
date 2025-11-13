using MCP;
using MCP.Payloads;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace MCP.Actions
{
	public class ExecuteUIAction : IEditorAction
	{
		public string ActionName => "executeUIEvent";

		public object Execute(EditorActionPayload payload)
		{
			var uiEventPayload = payload.payload.ToObject<ExecuteUIEventPayload>();
			if (uiEventPayload == null)
				return ActionResponse.Error("INVALID_PAYLOAD", "Invalid payload for executeUIEvent.");

			var targetObject = MCPUtils.FindGameObjectByPath(uiEventPayload.targetPath);
			if (targetObject == null)
				return ActionResponse.Error("OBJECT_NOT_FOUND", $"GameObject not found at path: {uiEventPayload.targetPath}", new { targetPath = uiEventPayload.targetPath });

			// Optional highlight before executing UI event (read-ish utility)
			bool? highlightOverride = null;
			bool? frameOverride = null;
			try
			{
				var h = payload.payload?["highlight"];
				if (h != null && h.Type != JTokenType.Null) highlightOverride = h.Type == JTokenType.Boolean ? h.Value<bool>() : (bool?)null;
				var hf = payload.payload?["highlightFrame"];
				if (hf != null && hf.Type != JTokenType.Null) frameOverride = hf.Type == JTokenType.Boolean ? hf.Value<bool>() : (bool?)null;
			}
			catch { }
			try
			{
				bool doHl = MCPUtils.ShouldHighlight(highlightOverride, true);
				if (doHl)
				{
					bool frame = MCPUtils.GetFrameSceneViewOverride(frameOverride);
					MCPUtils.Highlight(targetObject, frame);
				}
			}
			catch { }

			switch (uiEventPayload.eventType.ToLower())
			{
				case "click":
					ExecuteEvents.Execute(targetObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
					break;
				default:
					return ActionResponse.Error("UNSUPPORTED_EVENT_TYPE", $"Unsupported UI event type: {uiEventPayload.eventType}", new { eventType = uiEventPayload.eventType });
			}

			return ActionResponse.Ok(new { status = "OK", message = $"Executed '{uiEventPayload.eventType}' on '{uiEventPayload.targetPath}'", primaryTargetPath = uiEventPayload.targetPath });
		}
	}
}


