using System;
using System.Collections.Generic;

namespace MCP
{
	[Serializable]
	public class SceneOperation
	{
		public string op; // createGameObject, addComponent, setProperty, setTransform, instantiatePrefab, deleteGameObject

		// Common fields
		public string targetPath;

		// Create
		public string name;
		public string parentPath;
		public TransformDto transform;
		public string layerName;
		public string tag;

		// Add component
		public string componentName;

		// Set property
		public string propertyName;
		public string propertyValue;

		// Instantiate prefab
		public string assetPath;

		// Transform relative
		public bool relative;
	}

	[Serializable]
	public class ApplySceneOperationsPayload
	{
		public List<SceneOperation> operations = new List<SceneOperation>();
		public bool stopOnError = true;
	}
}


