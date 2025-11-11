# Extensions Template

This sample shows how to add project-specific MCP actions without forking the package.

## Steps
1. Import this sample into your project (Package Manager → MDMCP → Samples → Extensions Template → Import).
2. Open `YourCompany.MDMCP.Extensions.Editor.asmdef` and rename the assembly if desired.
3. Add your actions (implementing `MCP.IEditorAction`) and any DTOs under this assembly.
4. Validate discovery with:
   - `curl -X POST http://localhost:43210 -d '{"action":"listActions"}'`
5. Run your actions with minimal payloads and confirm an `ok:true` envelope.

Keep project-specific code in your own asmdef; do not fork the package.


