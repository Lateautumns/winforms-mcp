namespace Rhombus.WinFormsMcp.Server.Tools;

internal sealed class ToolExecutionException : Exception {
    public ToolExecutionException(string code, string message, bool retryable = false, Exception? innerException = null)
        : base(message, innerException) {
        Code = code;
        Retryable = retryable;
    }

    public string Code { get; }
    public bool Retryable { get; }
}