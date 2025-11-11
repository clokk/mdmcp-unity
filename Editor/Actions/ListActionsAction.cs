using MCP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MCP.Actions
{
	public class ListActionsAction : IEditorAction
	{
		public string ActionName => "listActions";

		public object Execute(EditorActionPayload payload)
		{
			var actions = new List<object>();

			var actionTypes = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(s => s.GetTypes())
				.Where(p => typeof(IEditorAction).IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);

			foreach (var actionType in actionTypes)
			{
				try
				{
					var instance = (IEditorAction)Activator.CreateInstance(actionType);
					var actionName = instance.ActionName;
					if (string.IsNullOrEmpty(actionName)) continue;

					var actionInfo = new Dictionary<string, object>
					{
						["name"] = actionName,
						["type"] = actionType.Name,
						["category"] = "Core"
					};

					var attr = actionType.GetCustomAttribute<MCPActionAttribute>();
					if (attr != null)
					{
						actionInfo["category"] = attr.Category ?? "Core";
						if (!string.IsNullOrEmpty(attr.Description))
							actionInfo["description"] = attr.Description;
						if (!string.IsNullOrEmpty(attr.ExamplePayload))
						{
							try
							{
								actionInfo["examplePayload"] = Newtonsoft.Json.JsonConvert.DeserializeObject(attr.ExamplePayload);
							}
							catch
							{
								actionInfo["examplePayload"] = attr.ExamplePayload;
							}
						}
					}

					if (!actionInfo.ContainsKey("description"))
					{
						if (actionType.Namespace?.Contains("Project") == true || actionType.Name.Contains("Badge") || actionType.Name.Contains("Project"))
							actionInfo["category"] = "Project";
						else
							actionInfo["category"] = "Core";
					}

					actions.Add(actionInfo);
				}
				catch (Exception ex)
				{
					UnityEngine.Debug.LogWarning($"[ListActions] Failed to get info for action type '{actionType.Name}': {ex.Message}");
				}
			}

			actions = actions.OrderBy(a =>
			{
				var dict = a as Dictionary<string, object>;
				return $"{dict?["category"]}_{dict?["name"]}";
			}).ToList();

			return ActionResponse.Ok(new
			{
				totalCount = actions.Count,
				coreCount = actions.Count(a => (a as Dictionary<string, object>)?["category"]?.ToString() == "Core"),
				projectCount = actions.Count(a => (a as Dictionary<string, object>)?["category"]?.ToString() == "Project"),
				actions = actions
			});
		}
	}
}


