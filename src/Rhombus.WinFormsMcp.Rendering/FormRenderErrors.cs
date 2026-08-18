namespace Rhombus.WinFormsMcp.Rendering;

/// <summary>
/// Attaches a stable renderer stage code without changing the renderer's
/// established InvalidOperationException contract.
/// </summary>
public static class FormRenderErrors {
    private const string DataKey = "WinFormsMcp.RenderErrorCode";

    public static InvalidOperationException Create(
        string code,
        string message,
        Exception? innerException = null) {
        var exception = new InvalidOperationException(message, innerException);
        exception.Data[DataKey] = code;
        return exception;
    }

    public static string? GetCode(Exception exception) =>
        exception.Data[DataKey] as string;
}