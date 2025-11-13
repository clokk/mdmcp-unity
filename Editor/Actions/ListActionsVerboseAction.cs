using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace MCP.Actions
{
	/// <summary>
	/// Provides a verbose listing of actions with metadata and a best-effort JSON Schema for payloads.
	/// </summary>
	public class ListActionsVerboseAction : MCP.IEditorAction
	{
		public string ActionName => "listActionsVerbose";

		public object Execute(MCP.EditorActionPayload payload)
		{
			var results = new List<object>();

			var actionTypes = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(s => s.GetTypes())
				.Where(p => ImplementsIEditorAction(p) && !p.IsInterface && !p.IsAbstract);

			// Deduplicate by ActionName, prefer project over package
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
						if (existingIsPackage && !incomingIsPackage)
						{
							selectedByName[name] = type;
						}
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

					var item = new Dictionary<string, object>();
					item["name"] = actionName;
					item["type"] = actionType.Name;
					item["category"] = IsPackageAssembly(actionType.Assembly) ? "Core" : "Project";
					item["supportsSync"] = true; // default; actual sync behavior is controlled by server logic

					// Attach MCPActionAttribute metadata when present
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
							if (!string.IsNullOrEmpty(cat)) item["category"] = cat;
							var desc = descProp?.GetValue(mcpAttr) as string;
							if (!string.IsNullOrEmpty(desc)) item["description"] = desc;
							var example = exampleProp?.GetValue(mcpAttr) as string;
							if (!string.IsNullOrEmpty(example))
							{
								try { item["examplePayload"] = Newtonsoft.Json.JsonConvert.DeserializeObject(example); }
								catch { item["examplePayload"] = example; }
							}
						}
						catch { /* ignore */ }
					}

					// Payload schema via MCPPayloadSchemaAttribute when present
					var schemaAttr = customAttrs.FirstOrDefault(a => string.Equals(a.GetType().Name, "MCPPayloadSchemaAttribute", StringComparison.Ordinal));
					if (schemaAttr != null)
					{
						try
						{
							var payloadType = schemaAttr.GetType().GetProperty("PayloadType")?.GetValue(schemaAttr) as Type;
							if (payloadType != null)
							{
								item["parametersSchema"] = BuildJsonSchemaForType(payloadType);
							}
						}
						catch { /* ignore */ }
					}

					// Fallback generic schema if none provided
					if (!item.ContainsKey("parametersSchema"))
					{
						item["parametersSchema"] = new Dictionary<string, object> {
							{ "type", "object" },
							{ "additionalProperties", true }
						};
					}

					results.Add(item);
				}
				catch (Exception ex)
				{
					UnityEngine.Debug.LogWarning($"[ListActionsVerbose] Failed to inspect '{actionType.Name}': {ex.Message}");
				}
			}

			results = results.OrderBy(a =>
			{
				var dict = a as Dictionary<string, object>;
				return $"{dict?["category"]}_{dict?["name"]}";
			}).ToList();

			return new
			{
				totalCount = results.Count,
				actions = results
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

		private static object BuildJsonSchemaForType(Type t)
		{
			if (t == null) return new Dictionary<string, object> { { "type", "object" } };

			// Primitives
			if (t == typeof(string)) return new Dictionary<string, object> { { "type", "string" } };
			if (t == typeof(bool)) return new Dictionary<string, object> { { "type", "boolean" } };
			if (t == typeof(int) || t == typeof(long)) return new Dictionary<string, object> { { "type", "integer" } };
			if (t == typeof(float) || t == typeof(double) || t == typeof(decimal)) return new Dictionary<string, object> { { "type", "number" } };

			// Arrays / lists
			if (t.IsArray)
			{
				var itemType = t.GetElementType();
				return new Dictionary<string, object> {
					{ "type", "array" },
					{ "items", BuildJsonSchemaForType(itemType) }
				};
			}
			if (t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(List<>) || t.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
			{
				var itemType = t.GetGenericArguments().FirstOrDefault();
				return new Dictionary<string, object> {
					{ "type", "array" },
					{ "items", BuildJsonSchemaForType(itemType) }
				};
			}

			// Objects: map public fields and properties (many DTOs use fields)
			var properties = new Dictionary<string, object>();
			var required = new List<string>();

			// Public instance fields
			var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public);
			foreach (var f in fields)
			{
				try
				{
					properties[f.Name] = BuildJsonSchemaForType(f.FieldType);
					// Heuristic: value types are non-nullable by default; strings/refs are optional
					if (IsNonNullable(f.FieldType))
					{
						required.Add(f.Name);
					}
				}
				catch { /* ignore */ }
			}

			// Public instance properties
			var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public);
			foreach (var p in props)
			{
				try
				{
					properties[p.Name] = BuildJsonSchemaForType(p.PropertyType);
					if (p.CanWrite && IsNonNullable(p.PropertyType))
					{
						required.Add(p.Name);
					}
				}
				catch { /* ignore */ }
			}

			var schema = new Dictionary<string, object> {
				{ "type", "object" },
				{ "properties", properties }
			};
			if (required.Count > 0) schema["required"] = required;
			return schema;
		}

		private static bool IsNonNullable(Type t)
		{
			if (!t.IsValueType) return false; // reference types are nullable by default
			return Nullable.GetUnderlyingType(t) == null;
		}
	}
}



