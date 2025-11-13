using MCP;
using MCP.Payloads;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace MCP.Actions
{
	public class GetPrefabDetailsAction : IEditorAction
	{
		public string ActionName => "getPrefabDetails";

		public object Execute(EditorActionPayload payload)
		{
			GetPrefabDetailsPayload p = null;
			try { p = (payload.payload as JToken)?.ToObject<GetPrefabDetailsPayload>(); } catch { }
			if (p == null || string.IsNullOrEmpty(p.assetPath))
				return ActionResponse.Error("INVALID_PAYLOAD", "assetPath is required for getPrefabDetails", new { provided = p });

			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.assetPath);
			if (prefab == null)
				return ActionResponse.Error("PREFAB_NOT_FOUND", $"Prefab not found at {p.assetPath}", new { p.assetPath });

			// Optional highlight for read actions (asset)
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
					MCPUtils.Highlight(prefab, frame);
				}
			}
			catch { }

			return ActionResponse.Ok(MCPUtils.GenerateContextForGameObject(prefab));
		}
	}
}


