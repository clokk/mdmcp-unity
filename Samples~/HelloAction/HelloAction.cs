using MCP;
using UnityEditor;

namespace Samples.MDMCP
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


