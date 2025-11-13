using MCP;
using MCP.Payloads;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace MCP.Actions
{
	public class FindGameObjectsAction : IEditorAction
	{
		public string ActionName => "findGameObjects";

		public object Execute(EditorActionPayload payload)
		{
			var p = payload.payload.ToObject<FindGameObjectsPayload>();
			if (p == null)
				return ActionResponse.Error("INVALID_PAYLOAD", "Invalid payload for findGameObjects. Expected FindGameObjectsPayload.");

			var scene = EditorSceneManager.GetActiveScene();
			if (!scene.IsValid())
				return ActionResponse.Error("NO_ACTIVE_SCENE", "No active scene to search in.");

			var allObjects = new List<GameObject>();
			foreach (var root in scene.GetRootGameObjects())
			{
				CollectAllGameObjects(root, allObjects);
			}

			var filtered = allObjects.AsEnumerable();

			if (!string.IsNullOrEmpty(p.namePattern))
			{
				try
				{
					var regex = new Regex(p.namePattern, RegexOptions.IgnoreCase);
					filtered = filtered.Where(go => regex.IsMatch(go.name));
				}
				catch
				{
					return ActionResponse.Error("INVALID_REGEX", $"Invalid regex pattern: {p.namePattern}", new { namePattern = p.namePattern });
				}
			}

			if (!string.IsNullOrEmpty(p.tag))
			{
				filtered = filtered.Where(go => go.CompareTag(p.tag));
			}

			if (p.layer >= 0)
			{
				filtered = filtered.Where(go => go.layer == p.layer);
			}

			if (!string.IsNullOrEmpty(p.componentType))
			{
				var componentType = MCPUtils.FindType(p.componentType);
				if (componentType == null)
					return ActionResponse.Error("COMPONENT_TYPE_NOT_FOUND", $"Component type '{p.componentType}' not found.", new { componentType = p.componentType });
				filtered = filtered.Where(go => go.GetComponent(componentType) != null);
			}

			if (p.activeOnly)
			{
				filtered = filtered.Where(go => go.activeInHierarchy);
			}

			var results = filtered.Select(go => new
			{
				name = go.name,
				path = MCPUtils.GetGameObjectPath(go.transform),
				instanceID = go.GetInstanceID(),
				tag = go.tag,
				layer = go.layer,
				layerName = LayerMask.LayerToName(go.layer),
				activeSelf = go.activeSelf,
				activeInHierarchy = go.activeInHierarchy
			}).ToList();

			// Optional highlight for find (default OFF; multi-select policy in settings)
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
				bool defaultOn = false;
				try { defaultOn = EditorPrefs.GetBool("MDMCP.AutoHighlightFindActions", false); } catch { defaultOn = false; }
				bool doHl = highlightOverride ?? defaultOn;
				if (doHl && results.Count > 0)
				{
					bool frame = MCPUtils.GetFrameSceneViewOverride(frameOverride);
					var (firstOnly, limit) = MCPUtils.GetMultiSelectPolicy();
					var objs = filtered.Select(go => (UnityEngine.Object)go).ToList();
					MCPUtils.HighlightMany(objs, frame, limit, firstOnly);
				}
			}
			catch { }

			return ActionResponse.Ok(new { count = results.Count, results, targetPaths = results.Select(r => r.path).ToArray() });
		}

		private void CollectAllGameObjects(GameObject obj, List<GameObject> collection)
		{
			collection.Add(obj);
			foreach (Transform child in obj.transform)
			{
				CollectAllGameObjects(child.gameObject, collection);
			}
		}
	}
}


