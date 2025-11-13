using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using MCP.InternalPayloads;
using MCP.Payloads;

namespace MCP.Actions
{
	[MCPAction(Description = "Apply multiple scene operations in one call. Supports: createGameObject, addComponent, setProperty, setTransform, instantiatePrefab, deleteGameObject")]
	[MCPPayloadSchema(typeof(ApplySceneOperationsPayload))]
	public class ApplySceneOperationsAction : IEditorAction
	{
		public string ActionName => "applySceneOperations";

		public object Execute(EditorActionPayload payload)
		{
			var results = new List<object>();
			try
			{
				var dto = payload.payload?.ToObject<ApplySceneOperationsPayload>();
				if (dto == null || dto.operations == null || dto.operations.Count == 0)
				{
					return ActionResponse.Error("INVALID_PAYLOAD", "Missing 'operations'");
				}

				foreach (var op in dto.operations)
				{
					try
					{
						object r = ExecuteSingle(op);
						results.Add(r);
						var okProp = r.GetType().GetProperty("ok");
						if (okProp != null && okProp.GetValue(r) is bool ok && !ok && dto.stopOnError) break;
					}
					catch (System.Exception exStep)
					{
						var err = ActionResponse.Error("STEP_ERROR", exStep.Message, new { exception = exStep.ToString(), op = op?.op, targetPath = op?.targetPath });
						results.Add(err);
						if (dto.stopOnError) break;
					}
				}

				return ActionResponse.Ok(new { steps = results });
			}
			catch (System.Exception ex)
			{
				return ActionResponse.Error("EXECUTION_ERROR", ex.Message, new { exception = ex.ToString() });
			}
		}

		private object ExecuteSingle(SceneOperation op)
		{
			switch (op.op)
			{
				case "createGameObject":
					return new CreateGameObjectAction().Execute(new EditorActionPayload
					{
						action = "createGameObject",
						payload = Newtonsoft.Json.Linq.JObject.FromObject(new CreateGameObjectPayload
						{
							name = op.name,
							parentPath = op.parentPath,
							transform = op.transform,
							layerName = op.layerName,
							tag = op.tag
						})
					});
				case "addComponent":
					return new AddComponentAction().Execute(new EditorActionPayload
					{
						action = "addComponent",
						payload = Newtonsoft.Json.Linq.JObject.FromObject(new AddRemoveComponentPayload
						{
							targetPath = op.targetPath,
							componentName = op.componentName
						})
					});
				case "setProperty":
					return new SetPropertyAction().Execute(new EditorActionPayload
					{
						action = "setProperty",
						payload = Newtonsoft.Json.Linq.JObject.FromObject(new UniversalSetPropertyPayload
						{
							targetPath = op.targetPath,
							componentName = op.componentName,
							propertyName = op.propertyName,
							propertyValue = op.propertyValue
						})
					});
				case "setTransform":
					return new SetTransformAction().Execute(new EditorActionPayload
					{
						action = "setTransform",
						payload = Newtonsoft.Json.Linq.JObject.FromObject(new SetTransformPayload
						{
							targetPath = op.targetPath,
							transform = op.transform,
							relative = op.relative
						})
					});
				case "instantiatePrefab":
					return new InstantiatePrefabAction().Execute(new EditorActionPayload
					{
						action = "instantiatePrefab",
						payload = Newtonsoft.Json.Linq.JObject.FromObject(new InstantiatePrefabPayload
						{
							assetPath = op.assetPath,
							parentPath = op.parentPath,
							transform = op.transform
						})
					});
				case "deleteGameObject":
					return new DeleteGameObjectAction().Execute(new EditorActionPayload
					{
						action = "deleteGameObject",
						payload = Newtonsoft.Json.Linq.JObject.FromObject(new DeleteGameObjectPayload
						{
							targetPath = op.targetPath
						})
					});
			}
			return ActionResponse.Error("UNKNOWN_OPERATION", $"Unknown op '{op.op}'");
		}
	}
}


