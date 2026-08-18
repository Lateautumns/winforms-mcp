namespace Rhombus.WinFormsMcp.Server.Automation;

/// <summary>
/// Provides STA-safe clipboard access.
/// </summary>
internal sealed class ClipboardService {
    private static readonly TimeSpan ClipboardTimeout = TimeSpan.FromSeconds(3);

    public string? GetText() {
        string? result = null;
        RunOnStaThread(() => {
            if (System.Windows.Forms.Clipboard.ContainsText())
                result = System.Windows.Forms.Clipboard.GetText();
        });
        return result;
    }

    public void SetText(string text) =>
        RunOnStaThread(() => System.Windows.Forms.Clipboard.SetText(text));

    private static void RunOnStaThread(Action action) {
        var thread = new Thread(() => {
            try {
                action();
            }
            catch {
                // Clipboard can be temporarily locked by another process.
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(ClipboardTimeout);
    }
}