using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MCP.Actions
{
	public class ListActionsAction : MCP.IEditorAction
	{
		public string ActionName => "listActions";

		public object Execute(MCP.EditorActionPayload payload)
		{
			var actions = new List<object>();

			var actionTypes = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(s => s.GetTypes())
				.Where(p => ImplementsIEditorAction(p) && !p.IsInterface && !p.IsAbstract);

			// Deduplicate by ActionName, preferring project implementations over package ones
			var selectedByName = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
			foreach (var type in actionTypes)
			{
				try
				{
					var temp = Activator.CreateInstance(type);
					var name = type.GetProperty("ActionName")?.GetValue(temp) as string;
					if (string.IsNullOrEmpty(name)) continue;

					if (selectedByName.TryGetValue(name, out var existingType))
					{
						bool existingIsPackage = IsPackageAssembly(existingType.Assembly);
						bool incomingIsPackage = IsPackageAssembly(type.Assembly);
						// Prefer project (non-package) over package
						if (existingIsPackage && !incomingIsPackage)
						{
							selectedByName[name] = type;
						}
						// else keep existing
					}
					else
					{
						selectedByName[name] = type;
					}
				}
				catch { /* ignore */ }
			}

			foreach (var kv in selectedByName)
			{
				var actionType = kv.Value;
				try
				{
					var instance = Activator.CreateInstance(actionType);
					var actionName = actionType.GetProperty("ActionName")?.GetValue(instance) as string;
					if (string.IsNullOrEmpty(actionName)) continue;

					var actionInfo = new Dictionary<string, object>
					{
						["name"] = actionName,
						["type"] = actionType.Name,
						["category"] = IsPackageAssembly(actionType.Assembly) ? "Core" : "Project"
					};

					// Try to read MCPActionAttribute reflectively if present (avoid compile-time dependency)
					var customAttrs = actionType.GetCustomAttributes(false);
					var mcpAttr = customAttrs.FirstOrDefault(a => string.Equals(a.GetType().Name, "MCPActionAttribute", StringComparison.Ordinal));
					if (mcpAttr != null)
					{
						try
						{
							var categoryProp = mcpAttr.GetType().GetProperty("Category");
							var descProp = mcpAttr.GetType().GetProperty("Description");
							var exampleProp = mcpAttr.GetType().GetProperty("ExamplePayload");
							var cat = categoryProp?.GetValue(mcpAttr) as string;
							if (!string.IsNullOrEmpty(cat)) actionInfo["category"] = cat;
							var desc = descProp?.GetValue(mcpAttr) as string;
							if (!string.IsNullOrEmpty(desc)) actionInfo["description"] = desc;
							var example = exampleProp?.GetValue(mcpAttr) as string;
							if (!string.IsNullOrEmpty(example))
							{
								try
								{
									actionInfo["examplePayload"] = Newtonsoft.Json.JsonConvert.DeserializeObject(example);
								}
								catch
								{
									actionInfo["examplePayload"] = example;
								}
							}
						}
						catch { /* ignore attribute parse errors */ }
					}

					if (!actionInfo.ContainsKey("description"))
					{
						// Fall back to assembly heuristic if attribute absent
						actionInfo["category"] = IsPackageAssembly(actionType.Assembly) ? "Core" : "Project";
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

			// Return plain object; server will wrap into envelope
			return new
			{
				totalCount = actions.Count,
				coreCount = actions.Count(a => (a as Dictionary<string, object>)?["category"]?.ToString() == "Core"),
				projectCount = actions.Count(a => (a as Dictionary<string, object>)?["category"]?.ToString() == "Project"),
				actions = actions
			};
		}

		private static bool IsPackageAssembly(Assembly assembly)
		{
			try
			{
				var name = assembly?.GetName()?.Name;
				return string.Equals(name, "Clokk.MDMCP.Editor", StringComparison.Ordinal);
			}
			catch { return false; }
		}

		private static bool ImplementsIEditorAction(Type t)
		{
			try
			{
				return t.GetInterfaces().Any(i => string.Equals(i.FullName, "MCP.IEditorAction", StringComparison.Ordinal));
			}
			catch { return false; }
		}
	}
}


