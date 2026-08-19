namespace Rhombus.WinFormsMcp.Server.Runtime;

internal sealed class RuntimeBridgeException : Exception {
    public RuntimeBridgeException(string code, string message, bool retryable = true, Exception? innerException = null)
        : base(message, innerException) {
        Code = code;
        Retryable = retryable;
    }

    public string Code { get; }
    public bool Retryable { get; }
}