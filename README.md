# MDMCP Server for Unity

MDMCP (Markdown MCP) Server is an HTTP-based automation bridge for the Unity Editor. It lets you inspect, modify, and test your project via JSON requests, and is fully extensible via `IEditorAction` plugins.

## Install (UPM - Git URL)

Add this line to your project's `Packages/manifest.json`:

```json
"com.clokk.mdmcp-unity": "https://github.com/clokk/mdmcp-unity.git#v0.2.1"
```

Or use the Package Manager "Add package from Git URL…" with:

```
https://github.com/clokk/mdmcp-unity.git#v0.2.1
```

GUI path:
- Window → Package Manager → + (Add) → Add package from Git URL…
- Paste: https://github.com/clokk/mdmcp-unity.git (or a pinned tag as above)

## Usage

- Start the server: `Markdown > Start MCP Server`
- Stop the server: `Markdown > Stop MCP Server`
- Default URL: `http://localhost:43210/`

### Quickstart (curl)

```bash
curl -X POST http://localhost:43210 -d '{"action":"getContext"}'
```

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


