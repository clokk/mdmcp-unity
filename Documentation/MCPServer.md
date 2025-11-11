# Unity Editor MCP Server & Workflow Guide

This document provides context and instructions for interacting with a More Context Protocol (MCP) server running inside the Unity Editor. The server allows for programmatic control over the Unity Editor, enabling automation of common tasks and in‑editor testing.

---

## 1. Server Overview

The MCP server runs locally at `http://localhost:43210`. It acts as a bridge for tools and scripts to interact with the Unity Editor by sending JSON commands via POST requests. All actions are registered with Unity's Undo system, making them safe to use.

The server's internal architecture is modular and extensible. Each action is implemented in its own class that inherits from the `IEditorAction` interface. On startup, the server uses reflection to automatically discover and register available actions across all loaded editor assemblies. This makes it easy to add functionality: simply create a new class that implements `IEditorAction`, and the server will handle discovery.

### Response Format

All actions return responses in a consistent envelope format:

Success Response:
```json
{
  "ok": true,
  "result": { /* action-specific result data */ },
  "warnings": [],
  "requestId": "optional-request-id"
}
```

Error Response:
```json
{
  "ok": false,
  "error": {
    "code": "ERROR_CODE",
    "message": "Human-readable error message",
    "details": { /* additional error context */ }
  },
  "requestId": "optional-request-id"
}
```

Legacy actions that don't use the `ActionResponse` helpers are automatically wrapped in this format.

For implementation details, see the scripts in `Packages/com.clokk.mdmcp-unity/Editor`.

> Sync vs Async
>
> Most write actions run asynchronously by default. You can opt into synchronous execution by adding `"sync": true` inside the `payload`. When `sync` is true, the server returns the action's real result (including errors). The latest action envelope is exposed via `getContext` under `lastActionResult`.

> Note: If you change MCP server scripts, restart the server via `Markdown > Stop MCP Server` and `Markdown > Start MCP Server` in the Unity Editor.

---

## 2. Architectural Components

- `MDMCPServerUnity.cs`: Core of the server, runs in the background, listens for HTTP requests, parses JSON payloads with `Newtonsoft.Json`, and delegates to actions.

- `IEditorAction.cs`: Contract for actions. Implement:
  - `ActionName` (string): name used in JSON `action`.
  - `Execute(EditorActionPayload payload)`: action logic; returns any serializable object (wrapped into the envelope).

- Actions: Concrete implementations of `IEditorAction`. These may live in this package or in your project’s own editor assemblies. The server discovers all loaded action types at runtime via reflection.

- `EditorActionPayload.cs`: Flexible `payload` container (JSON token).

- `MCPUtils.cs`: Common utilities for property setting, path resolution, type finding, object serialization, etc.

---

## 3. Core Interaction Workflow: Inspect and Edit

A practical workflow is to programmatically inspect the scene to gather context and then edit objects based on that context.

### Step 0: Get Server Context with `getContext`

```bash
curl -X POST http://localhost:43210 -d '{"action": "getContext"}'
```

Returns editor state, active scene, selections, server readiness, and last action result.

### Step 1: Discover with `getSceneHierarchy` or `findGameObjects`

Get full scene hierarchy:
```bash
curl -X POST http://localhost:43210 -d '{"action": "getSceneHierarchy"}'
```

Find objects by component:
```bash
curl -X POST http://localhost:43210 -d '{
  "action": "findGameObjects",
  "payload": { "componentType": "UnityEngine.UI.Button", "activeOnly": true }
}'
```

### Step 2: Inspect with `getGameObjectDetails` or `getPrefabDetails`

Get details for a scene object:
```bash
curl -X POST http://localhost:43210 -d '{
  "action": "getGameObjectDetails",
  "payload": { "targetPath": "/Canvas/MainMenu/PlayButton" }
}'
```

Get details for a prefab asset:
```bash
curl -X POST http://localhost:43210 -d '{
  "action": "getPrefabDetails",
  "payload": { "assetPath": "Assets/Prefabs/MyUIPanel.prefab" }
}'
```

### Step 3: Edit with `setProperty` or `setSerializedProperty`

Set a public property:
```bash
curl -X POST http://localhost:43210 -d '{
  "action": "setProperty",
  "payload": {
    "targetPath": "/Player",
    "componentName": "PlayerController",
    "propertyName": "speed",
    "propertyValue": "10.5"
  }
}'
```

Set a serialized property:
```bash
curl -X POST http://localhost:43210 -d '{
  "action": "setSerializedProperty",
  "payload": {
    "targetPath": "/Player",
    "componentName": "PlayerController",
    "propertyPath": "m_Health",
    "propertyValue": "100"
  }
}'
```

Note on Path Resolution: Robust hierarchical scene paths like `/Root/Child/Sub` are supported.

---

## 4. In-Editor Testing Workflow

Manual testing policy: A developer/tester performs manual playtesting; this tooling supports in‑editor automation.

Start Play Mode:
```bash
curl -X POST http://localhost:43210 -d '{"action": "setPlayMode", "payload": true}'
```

Wait for initialization:
```bash
curl -X POST http://localhost:43210 -d '{"action": "wait", "payload": 1.5}'
```

Simulate a UI click:
```bash
curl -X POST http://localhost:43210 -d '{
  "action": "executeUIEvent",
  "payload": { "targetPath": "/Canvas/MainMenu/PlayButton", "eventType": "click" }
}'
```

Inspect runtime state:
```bash
curl -X POST http://localhost:43210 -d '{
  "action": "getGameObjectDetails", 
  "payload": { "targetPath": "/Player" }
}'
```

---

## 5. Supported Actions (API Reference)

All actions are invoked by sending a POST with a JSON body: `{ "action": "name", "payload": ... }`.

Baseline shipped actions in this package:

### Context & Discovery
- `getContext`
- `listActions`

### Editor & Testing
- `setPlayMode` (enter/exit play mode)
- `wait` (seconds)
- `executeUIEvent` (e.g., button click)

### Scene & Hierarchy
- `getSceneHierarchy`
- `findGameObjects`
- `getGameObjectDetails`

### Prefab & Assets
- `getPrefabDetails`
- `getPrefabHierarchy`
- `modifyPrefab`
- `openScene`
- `selectInProjectWindow`
- `setProperty`
- `setSerializedProperty`
- `setSortingLayer`

Synchronous `modifyPrefab` example:
```json
{
  "action": "modifyPrefab",
  "payload": {
    "sync": true,
    "prefabPath": "Assets/Prefabs/MyUIPanel.prefab",
    "modifications": [
      { "operation": "addComponent", "componentName": "UnityEngine.UI.LayoutElement" },
      { "operation": "setProperty", "componentName": "UnityEngine.UI.LayoutElement", "propertyName": "preferredHeight", "propertyValue": "240" }
    ]
  }
}
```

Note: This package ships only the above baseline actions. Projects are expected to add more actions (e.g., add/remove component, asset creation/duplication, asset references, list/array utilities, component property getters) in their own Editor assemblies. The server auto‑discovers all `IEditorAction` implementations in loaded editor assemblies.

---

## 6. Complete End-to-End Test Workflow

```bash
# 1. Check initial state
curl -X POST http://localhost:43210 -d '{"action": "getContext"}'

# 2. Enter play mode
curl -X POST http://localhost:43210 -d '{"action": "setPlayMode", "payload": true}'

# 3. Wait for play mode initialization
sleep 3
curl -X POST http://localhost:43210 -d '{"action": "wait", "payload": 2.0}'

# 4. Verify play mode state
curl -X POST http://localhost:43210 -d '{"action": "getContext"}' | jq .result.editorState

# 5. Find UI buttons using component filter
curl -X POST http://localhost:43210 -d '{
  "action": "findGameObjects",
  "payload": { "componentType": "UnityEngine.UI.Button", "activeOnly": true }
}'

# 6. Click a button to trigger scene transition
curl -X POST http://localhost:43210 -d '{
  "action": "executeUIEvent",
  "payload": { "targetPath": "/Canvas/MainMenu/PlayButton", "eventType": "click" }
}'

# 7. Wait for scene load
curl -X POST http://localhost:43210 -d '{"action": "wait", "payload": 5.0}'

# 8. Verify scene transition
curl -X POST http://localhost:43210 -d '{"action": "getContext"}' | jq .result.activeScene

# 9. Get scene hierarchy of new scene
curl -X POST http://localhost:43210 -d '{"action": "getSceneHierarchy"}'

# 10. Find game objects by name pattern
curl -X POST http://localhost:43210 -d '{
  "action": "findGameObjects",
  "payload": { "namePattern": "GameManager|GolfBall", "activeOnly": true }
}'

# 11. Inspect runtime GameObject details in play mode
curl -X POST http://localhost:43210 -d '{
  "action": "getGameObjectDetails",
  "payload": { "targetPath": "/Canvas/MainHUD" }
}'
```

Key validation points:
- Response envelope format is consistent.
- Path resolution supports both Edit and Play modes.
- Play mode transitions are stable.
- Runtime inspection reads live values.
- Component filtering works.
- UI interaction works.
- Scene discovery works.

---

## 7. Automation & Payload Tips

Prefer writing complex payloads to a temporary file and use `curl --data-binary @file.json` to avoid quoting issues.

Pretty‑print JSON responses using `jq` or `python -m json.tool`.

### Troubleshooting JSON payloads
- Send exactly one JSON object per request.
- Use file-based payloads to avoid shell quoting pitfalls.
- Always set `-H "Content-Type: application/json"`.
- Avoid concatenating multiple JSON objects or adding non‑JSON text into the body.

---

## 8. Developer Notes & Debugging

- Auto‑start: The server auto‑starts on domain reloads (`[InitializeOnLoad]` in `MDMCPServerUnity.cs`). Use `Markdown > Start MCP Server` and `Markdown > Stop MCP Server` to control manually.
- Logs & filtering: All server logs are prefixed with `[MDMCP]`.
- Breakpoints: Set breakpoints in `Packages/com.clokk.mdmcp-unity/Editor/MDMCPServerUnity.cs` or in any of your project’s extension actions (implementations of `IEditorAction`).
- Working directory: Examples referencing `.cursor/temp_payload.json` assume commands are run from the project root. Use absolute paths otherwise.


