using MCP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace MCP.Actions
{
	public class GetContextAction : IEditorAction
	{
		public string ActionName => "getContext";

		public object Execute(EditorActionPayload payload)
		{
			string requestId = null;
			if (payload.payload != null)
			{
				try
				{
					var dict = payload.payload.ToObject<Dictionary<string, object>>();
					if (dict != null && dict.ContainsKey("requestId"))
					{
						requestId = dict["requestId"]?.ToString();
					}
				}
				catch { }
			}

			var contextData = new Dictionary<string, object>();
			string editorState = "Editing";
			if (EditorApplication.isPlaying) editorState = "Playing";
			if (EditorApplication.isPaused) editorState = "Paused";
			contextData["editorState"] = editorState;
			// Editor status flags for bridges to poll readiness
			try { contextData["editorCompiling"] = EditorApplication.isCompiling; } catch { contextData["editorCompiling"] = false; }
			try { contextData["editorUpdating"] = EditorApplication.isUpdating; } catch { contextData["editorUpdating"] = false; }
			
			var activeScene = EditorSceneManager.GetActiveScene();
			contextData["activeScene"] = activeScene.IsValid() ? activeScene.name : "No Active Scene";
			contextData["serverReady"] = true;
			contextData["timestamp"] = DateTime.UtcNow.ToString("o");
			
			var selectionsList = new List<object>();
			foreach (var selectedObject in Selection.objects)
			{
				if (selectedObject is GameObject go)
				{
					selectionsList.Add(MCPUtils.GenerateContextForGameObject(go));
				}
				else if (AssetDatabase.Contains(selectedObject))
				{
					selectionsList.Add(MCPUtils.GenerateContextForAsset(selectedObject));
				}
			}
			contextData["selections"] = selectionsList;

			return ActionResponse.Ok(contextData, null, requestId);
		}
	}
}


