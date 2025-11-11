using MCP;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCP.Actions
{
	public class GetPrefabHierarchyAction : IEditorAction
	{
		public string ActionName => "getPrefabHierarchy";

		private class Payload
		{
			[JsonProperty("assetPath")]
			public string AssetPath { get; set; } = string.Empty;
		}

		public object Execute(EditorActionPayload payload)
		{
			Payload p = null;
			try
			{
				if (payload.payload != null)
					p = (payload.payload as JToken)?.ToObject<Payload>();
			}
			catch { }

			if (p == null || string.IsNullOrEmpty(p.AssetPath))
				return ActionResponse.Error("INVALID_PAYLOAD", "assetPath is required for getPrefabHierarchy");

			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.AssetPath);
			if (prefab == null)
				return ActionResponse.Error("PREFAB_NOT_FOUND", $"Prefab not found at {p.AssetPath}", new { assetPath = p.AssetPath });

			var root = BuildNode(prefab.transform);
			return ActionResponse.Ok(new { prefab.name, path = p.AssetPath, hierarchy = root });
		}

		private Dictionary<string, object> BuildNode(Transform t)
		{
			var node = new Dictionary<string, object>
			{
				["name"] = t.gameObject.name,
				["components"] = SerializeComponents(t.gameObject)
			};

			var children = new List<object>();
			foreach (Transform child in t)
			{
				children.Add(BuildNode(child));
			}
			node["children"] = children;
			return node;
		}

		private List<object> SerializeComponents(GameObject go)
		{
			var list = new List<object>();
			foreach (var c in go.GetComponents<Component>())
			{
				if (c == null) continue;
				list.Add(MCPUtils.SerializeComponent(c));
			}
			return list;
		}
	}
}


