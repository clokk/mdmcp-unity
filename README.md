# MDMCP Server for Unity

MDMCP (Markdown MCP) Server is an HTTP-based automation bridge for the Unity Editor. It lets you inspect, modify, and test your project via JSON requests, and is fully extensible via `IEditorAction` plugins.

## Install (UPM - Git URL)

Add this line to your project's `Packages/manifest.json`:

```json
"com.clokk.mdmcp-unity": "https://github.com/clokk/mdmcp-unity.git#v0.1.0"
```

Or use the Package Manager "Add package from Git URL…" with:

```
https://github.com/clokk/mdmcp-unity.git#v0.1.0
```

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

## License

MIT © clokk


