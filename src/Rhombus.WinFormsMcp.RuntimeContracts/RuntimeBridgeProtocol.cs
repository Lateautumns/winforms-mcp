namespace Rhombus.WinFormsMcp.RuntimeContracts;

/// <summary>
/// Wire-level constants shared by the MCP server and the in-process bridge.
/// </summary>
public static class RuntimeBridgeProtocol {
    public const int Version = 1;
    public const string PipePrefix = "winforms-mcp-runtime-";

    public const string Hello = "hello";
    public const string GetStatus = "get_status";
    public const string GetControlTree = "get_control_tree";
    public const string InspectControl = "inspect_control";
    public const string GetAncestors = "get_ancestors";
    public const string GetWindowTree = "get_window_tree";
    public const string GetBindings = "get_bindings";

    public static string GetPipeName(int processId) => $"{PipePrefix}{processId}";
}
