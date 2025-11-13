# MDMCP Server for Unity - Guide

This document provides context and instructions for interacting with a More Context Protocol (MCP) server running inside the Unity Editor. The server allows for programmatic control over the Unity Editor, enabling automation of common tasks and in‑editor testing.

---

## 1. Server Overview

The MCP server runs locally at `http://localhost:43210`. It acts as a bridge for tools and scripts to interact with the Unity Editor by sending JSON commands via POST requests. All actions are registered with Unity's Undo system, making them safe to use.
The MCP bridge runs in single‑instance mode and targets `http://localhost:$MDMCP_PORT/` (default `43210`). Set the `MDMCP_PORT` environment variable if you change the Unity server port.

### Install (UPM - Git URL)

Recommended GUI path:

- Window → Package Manager → + (Add) → Add package from Git URL…
- Paste: `https://github.com/clokk/mdmcp-unity.git`
- Optionally pin to a specific tag for reproducibility, e.g.: `https://github.com/clokk/mdmcp-unity.git#v0.2.1`

Quick URL: `https://github.com/clokk/mdmcp-unity.git`

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
> Write actions now run synchronously by default to support natural‑language flows in IDEs. You no longer need to set `payload.sync` for writes (it is still honored if provided). Read/inspect actions continue to run synchronously. The latest action envelope is exposed via `getContext` under `lastActionResult`.

> Note: If you change MCP server scripts, restart the server via `Markdown > Stop MCP Server` and `Markdown > Start MCP Server` in the Unity Editor.

### MCP Tool Payload Normalization (Bridge)
- MCP tools (in IDEs) call a Python bridge that forwards requests to Unity. The bridge normalizes several payload shapes into Unity’s `{ \"action\", \"payload\" }` format.
- Recommended tool payload shape: top‑level args (e.g., `{ \"name\": \"Player\" }`).
- Accepted normalized forms:
  - Top‑level dict: `{ \"name\": \"Player\" }`
  - Nested under `payload`: `{ \"payload\": { \"name\": \"Player\" }, \"sync\": true }` (sync is hoisted into inner)
  - JSON string under common keys: `{ \"kwargs\": \"{\\\"name\\\":\\\"Player\\\"}\" }`, `{ \"json\": \"{...}\" }`, `{ \"data\": \"{...}\" }`
  - Single‑key JSON string: `{ \"anything\": \"{...}\" }` (when only one key is present)
- Direct HTTP (curl, scripts) should always use `{ \"action\": \"...\", \"payload\": { ... } }`.
- Diagnostics:
  - Set `MDMCP_BRIDGE_LOG=1` to log action, keys, and payload size before POST on the bridge.
  - On invalid payloads, Unity logs a compact preview in the Console (e.g., `[MDMCP][createGameObject] Invalid payload: {...}`).
  - Focus Unity on first tool request: set `MDMCP_BRIDGE_FOCUS=1` (default). Set to `0` to disable. The bridge will bring Unity to the foreground once and call `getContext` to nudge a domain refresh.
 - Bridge aliases:
   - `createGameObject` supports `parent` as an alias for `parentPath`. It also supports `path` like `\"/Parent/Child\"` and derives `name=\"Child\"`, `parentPath=\"/Parent\"` when not otherwise provided.
   - `setParent` supports `gameObject` → `targetPath` and `parent` → `parentPath`.
   - `highlight` supports `gameObject` → `targetPath`.

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

## 2.1. AI Agent Context Recommendation

If you are using an AI assistant/agent to automate or test Editor tasks, include this guide file in the agent’s context so it understands the API and envelopes:

- `Packages/com.clokk.mdmcp-unity/Documentation/MDMCPServer.md`

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
Root handling: Passing `"/"` as `parentPath` means “place at scene root”.

Create a new GameObject:
```bash
# Using name and parentPath
curl -X POST http://localhost:43210 -d '{
  "action": "createGameObject",
  "payload": { "name": "HL_Child", "parentPath": "/HL_Root" }
}'

# Or using a single path (bridge derives name and parentPath)
curl -X POST http://localhost:43210 -d '{
  "action": "createGameObject",
  "payload": { "path": "/HL_Root/HL_Child" }
}'
```

Reparent an existing GameObject:
```bash
curl -X POST http://localhost:43210 -d '{
  "action": "setParent",
  "payload": { "targetPath": "/HL_Child", "parentPath": "/HL_Root", "keepWorldPosition": true }
}'
# Bridge also accepts aliases: {"gameObject":"...","parent":"..."}
```

Auto-highlight: By default, write actions will highlight their primary target to aid user visibility.
- `createGameObject`: highlights the newly created GameObject
- `instantiatePrefab`: highlights the instantiated scene instance
- `addComponent`: highlights the target GameObject before adding
You can override per-call with `payload.highlight=false`. Global defaults are configurable in `Markdown > MCP Settings…` (Auto-highlight write actions, Frame Scene view).
Additional highlight behaviors:
- Property/Transform changes highlight the target before applying.
- Duplicate highlights the duplicate after creation.
- Delete highlights the target before deleting (no selection after).
- RemoveComponent and SetSortingLayer highlight the target before changes.
- Read actions (e.g., getGameObjectDetails/getPrefabDetails/executeUIEvent) can auto-highlight when enabled.
- Find results can optionally highlight either the first match or multiple up to a limit.

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
# or
curl -X POST http://localhost:43210 -d '{"action":"wait","payload":{"seconds":1.5}}'
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

| Action | Purpose | Payload DTO |
| --- | --- | --- |
| `ping` | Health check, returns ok:true | — |
| `getContext` | Editor state and last envelope | — |
| `listActions` | Discover available actions | — |
| `setPlayMode` | Enter/exit play mode | `bool` |
| `wait` | Delay for seconds | `float` |
| `executeUIEvent` | Simulate UI event (e.g., click) | `ExecuteUIEventPayload` |
| `getSceneHierarchy` | Retrieve active scene hierarchy | — |
| `findGameObjects` | Find by component/name pattern | `FindGameObjectsPayload` |
| `getGameObjectDetails` | Inspect component/fields | — |
| `getPrefabDetails` | Inspect prefab asset | `GetPrefabDetailsPayload` |
| `getPrefabHierarchy` | Prefab hierarchy inspection | — |
| `modifyPrefab` | Synchronously modify prefab | `ModifyPrefabPayload` |
| `openScene` | Open a scene asset | `OpenScenePayload` |
| `selectInProjectWindow` | Select asset in Project | `string path` |
| `setProperty` | Set a public field/property | `UniversalSetPropertyPayload` |
| `setSerializedProperty` | SerializedProperty setter | `SetSerializedPropertyPayload` |
| `setSortingLayer` | Set sorting layer | `SetSortingLayerPayload` |
| `addComponent` | Add a component to a GameObject | `AddRemoveComponentPayload` |
| `removeComponent` | Remove component(s) from GameObject | `AddRemoveComponentPayload` (uses `all`) |
| `setMultipleProperties` | Batch set multiple properties | `SetMultiplePropertiesPayload` |
| `highlight` | Select and ping a target (scene object or asset) | `HighlightPayload` |
| `setParent` | Reparent a GameObject (supports scene root with `/`) | `SetParentPayload` |
| `createGameObject` | Create a new GameObject in scene | `CreateGameObjectPayload` |
| `instantiatePrefab` | Instantiate a prefab asset into scene | `InstantiatePrefabPayload` |
| `setTransform` | Set or adjust transform (move/rotate/scale) | `SetTransformPayload` |
| `duplicateGameObject` | Duplicate a GameObject | `DuplicateGameObjectPayload` |
| `deleteGameObject` | Delete a GameObject | `DeleteGameObjectPayload` |
| `applySceneOperations` | Apply multiple scene operations in one call | `ApplySceneOperationsPayload` |
| `createGameObject` | Create a new GameObject in scene | `CreateGameObjectPayload` |
| `instantiatePrefab` | Instantiate a prefab asset into scene | `InstantiatePrefabPayload` |
| `setTransform` | Set or adjust transform (move/rotate/scale) | `SetTransformPayload` |
| `duplicateGameObject` | Duplicate a GameObject | `DuplicateGameObjectPayload` |
| `deleteGameObject` | Delete a GameObject | `DeleteGameObjectPayload` |
| `applySceneOperations` | Apply multiple scene operations in one call | `ApplySceneOperationsPayload` |

See the end-to-end test workflow for usage context: Section 7.

### Context & Discovery
- `ping`
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
- `addComponent`
- `removeComponent`
- `setMultipleProperties`

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

## 6. Extending MDMCP in Your Project (Project-Specific Actions)

Add custom actions in your own project without forking the package.

### Recommended structure
1) Create an Editor assembly for extensions (example):
   - Folder: `Assets/Editor/MDMCPExtensions/`
   - Add an asmdef (e.g., `YourCompany.MDMCP.Extensions.Editor.asmdef`)
   - References: `Clokk.MDMCP.Editor` (from this package), `UnityEditor`, `UnityEngine.CoreModule`

2) Implement actions
   - Create classes implementing `MCP.IEditorAction` (no registration required; reflection-based discovery)
   - Example:
     ```csharp
     using MCP;
     namespace YourCompany.MDMCP.Actions
     {
         public class HelloAction : IEditorAction
         {
             public string ActionName => "hello";
             public object Execute(EditorActionPayload payload)
             {
                 return ActionResponse.Ok(new { message = "Hello from project!" });
             }
         }
     }
     ```

3) Define any payload DTOs used by your actions
   - Place DTOs in the same extensions assembly (e.g., `namespace MCP.Payloads`)
   - Deserialize with `payload.payload.ToObject<YourPayload>()`

4) Keep dependencies project-local
   - Avoid adding project-specific logic to the package
   - Keep game-specific editors/data and DTOs in your extension assembly

5) Test discovery
   - Use `listActions` to confirm your new actions appear
   - Run your action with a minimal payload and validate `ok:true` envelope

---

## 7. Complete End-to-End Test Workflow

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

## 8. Automation & Payload Tips

Prefer writing complex payloads to a temporary file and use `curl --data-binary @file.json` to avoid quoting issues.

Pretty‑print JSON responses using `jq` or `python -m json.tool`.

### Troubleshooting JSON payloads
- Send exactly one JSON object per request.
- Use file-based payloads to avoid shell quoting pitfalls.
- Always set `-H "Content-Type: application/json"`.
- Avoid concatenating multiple JSON objects or adding non‑JSON text into the body.

---

## 9. Developer Notes & Debugging

- Auto‑start: The server auto‑starts on domain reloads (`[InitializeOnLoad]` in `MDMCPServerUnity.cs`). Use `Markdown > Start MCP Server` and `Markdown > Stop MCP Server` to control manually.
- Logs & filtering: All server logs are prefixed with `[MDMCP]`.
- Breakpoints: Set breakpoints in `Packages/com.clokk.mdmcp-unity/Editor/MDMCPServerUnity.cs` or in any of your project’s extension actions (implementations of `IEditorAction`).
- Working directory: Examples referencing `.cursor/temp_payload.json` assume commands are run from the project root. Use absolute paths otherwise.

## 10. Safety & Operability

- Localhost-only: The server listens on `localhost` only. Exposing externally is not supported.
- Port configuration: Change the port via `Markdown > MCP Settings…` (default `43210`). The server restarts when you change the port if running.
- Auto-start toggle: Enable/disable in `Markdown > MCP Settings…`.
- First-request refresh: The server can force `AssetDatabase.Refresh()` and wait for compilation/imports on the first request after a domain reload. Configure in `Markdown > MCP Settings…` (Refresh assets and wait on first request; First-request wait seconds).
- Response envelopes: All responses are wrapped with `{ ok, result, error?, warnings?, requestId? }`.
- Sync by default for write actions: State‑changing actions (e.g., create/instantiate/set/duplicate/delete/applySceneOperations/add/remove component) execute synchronously by default to provide deterministic results to IDE clients. You may still include `payload.sync` (boolean) for compatibility.

## 11. Highlight Settings and Result Contract

Settings (Markdown > MCP Settings…):
- Auto-highlight write actions (default ON)
- Frame Scene view on highlight (default ON)
- Auto-highlight read actions (default ON)
- Auto-highlight find results (default OFF)
- Multi-select mode: FirstOnly | UpToLimit (default FirstOnly)
- Multi-select limit (default 8)
- Suppress highlight in Play Mode (default OFF)
- Highlight throttle (ms) (default 150)

Per-call overrides:
- `payload.highlight` (bool): opt-out or force highlight for an action
- `payload.highlightFrame` (bool): override framing behavior

Result contract additions:
- `primaryTargetPath` (string): scene path of main target, when applicable
- `assetPath` (string): asset path when action targets an asset
- `targetPaths` (string[]): for multi-target results (e.g., findGameObjects)

Bridge fallback (optional):
- If `MDMCP_BRIDGE_FOLLOW=1`, the bridge will call `highlight` with `primaryTargetPath` (or first `targetPath`) when the server doesn’t auto-highlight. Disabled by default.


