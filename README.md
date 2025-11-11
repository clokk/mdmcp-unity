# MDMCP Server for Unity

[![version](https://img.shields.io/badge/version-v0.2.4-blue.svg)](https://github.com/clokk/mdmcp-unity/releases)
[![license](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![openupm](https://img.shields.io/badge/openupm-com.clokk.mdmcp--unity-blue.svg)](https://openupm.com/packages/com.clokk.mdmcp-unity/)

MDMCP (Markdown MCP) Server is an HTTP-based automation bridge for the Unity Editor. It lets you inspect, modify, and test your project via JSON requests, and is fully extensible via `IEditorAction` plugins.

## What's included

| Item | Description |
| --- | --- |
| Core server | Background HTTP listener with reflection-based action discovery |
| Baseline actions | Context, discovery, scene/prefab inspection, property setters, UI, wait/playmode, and more |
| New utilities | `ping`, `addComponent`, `removeComponent`, `setMultipleProperties` |
| Settings window | Auto-start toggle, port, log level; Start/Stop and Open Docs |
| Samples | Hello Action; Extensions Template (asmdef + sample action) |
| Docs | Full guide at `Packages/com.clokk.mdmcp-unity/Documentation/MDMCPServer.md` |

## Install (UPM - Git URL)

Add this line to your project's `Packages/manifest.json`:

```json
"com.clokk.mdmcp-unity": "https://github.com/clokk/mdmcp-unity.git#v0.2.4"
```

Or use the Package Manager "Add package from Git URL…" with:

```
https://github.com/clokk/mdmcp-unity.git#v0.2.4
```

GUI path:
- Window → Package Manager → + (Add) → Add package from Git URL…
- Paste: https://github.com/clokk/mdmcp-unity.git (or a pinned tag as above)

## Usage

- Start the server: `Markdown > Start MCP Server`
- Stop the server: `Markdown > Stop MCP Server`
- Default URL: `http://localhost:43210/`

### OpenUPM (scoped registry)

Option A: CLI

```bash
openupm add com.clokk.mdmcp-unity
```

Option B: Scoped registry (manifest.json)

1) Add registry:

```json
{
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [ "com.clokk" ]
    }
  ]
}
```

2) In Package Manager, switch to My Registries and install by name: `com.clokk.mdmcp-unity`

### Quickstart (curl)

```bash
curl -X POST http://localhost:43210 -d '{"action":"getContext"}'
```

### Why MDMCP?

Automate in-editor flows with simple HTTP calls. For example:

```bash
# Enter play mode, wait, click a UI button, and inspect context
curl -X POST http://localhost:43210 -H "Content-Type: application/json" -d '{"action":"setPlayMode","payload":true}'
curl -X POST http://localhost:43210 -H "Content-Type: application/json" -d '{"action":"wait","payload":2}'
curl -X POST http://localhost:43210 -H "Content-Type: application/json" -d '{"action":"executeUIEvent","payload":{"targetPath":"/Canvas/MainMenu/PlayButton","eventType":"click"}}'
curl -X POST http://localhost:43210 -H "Content-Type: application/json" -d '{"action":"getContext"}'
```

### Clients

Python (requests):
```python
import requests, json
url = "http://localhost:43210"
resp = requests.post(url, json={"action":"getContext"})
print(json.dumps(resp.json(), indent=2))
```

Node (axios):
```javascript
const axios = require("axios");
async function main() {
  const { data } = await axios.post("http://localhost:43210", { action: "listActions" });
  console.log(data);
}
main();
```

### Prompting in Cursor

- Give the agent concrete context and speak in natural language about what you want to automate with MCP actions.
- Always attach the package guide: `Packages/com.clokk.mdmcp-unity/Documentation/MDMCPServer.md` (or `MDMCPServer.md` if copied locally).
- Prompt naturally, e.g.: “Enter play mode, wait 2s, click /Canvas/MainMenu/PlayButton, then return getContext.”
- Ensure the Unity Editor is open, compiled (no pending compilation), and the Editor window is active before running MCP commands.

## Extend

Create a class that implements `MCP.IEditorAction` anywhere in an Editor assembly. The server discovers actions via reflection:

```csharp
using MCP;
using UnityEditor;

namespace MyCompany.MDMCP.Actions
{
    public class HelloAction : IEditorAction
    {
        public string ActionName => "hello";
        public object Execute(EditorActionPayload payload)
        {
            return ActionResponse.Ok(new { message = "Hello from MDMCP!" });
        }
    }
}
```

Project-specific extensions:
- Create an Editor asmdef (e.g., `YourCompany.MDMCP.Extensions.Editor`) and reference `Clokk.MDMCP.Editor`
- Put your `IEditorAction` classes and any payload DTOs there (e.g., `namespace MCP.Payloads`)
- Validate discovery via `listActions`

Agent context tip:
- Pass the package guide to your agent: `Packages/com.clokk.mdmcp-unity/Documentation/MDMCPServer.md`

## License

MIT © clokk


