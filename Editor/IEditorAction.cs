namespace MCP
{
	public interface IEditorAction
	{
		string ActionName { get; }
		object Execute(EditorActionPayload payload);
	}
}


