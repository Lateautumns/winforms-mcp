using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;

using FlaUI.Core;
using FlaUI.Core.AutomationElements;

namespace Rhombus.WinFormsMcp.Server.Automation;

/// <summary>
/// Captures UIA elements, process windows, or the visible desktop.
/// </summary>
internal sealed class ScreenshotService {
    private readonly AutomationRuntimeContext _context;
    private readonly HeadlessDesktopService _desktopService;

    public ScreenshotService(
        AutomationRuntimeContext context,
        HeadlessDesktopService desktopService) {
        _context = context;
        _desktopService = desktopService;
    }

    public void TakeScreenshot(string outputPath, AutomationElement? element = null, int? pid = null) {
        try {
            Bitmap? bitmap = pid == null ? null : CaptureProcessWindow(pid.Value);

            if (bitmap == null && element != null) {
                try {
                    bitmap = element.Capture();
                }
                catch {
                    var processId = element.Properties.ProcessId.ValueOrDefault;
                    if (processId > 0)
                        bitmap = CaptureProcessWindow(processId);
                }
            }

            if (bitmap == null && _context.AutomationOrNull != null)
                bitmap = _context.Automation.GetDesktop().Capture();

            if (bitmap == null)
                return;

            using (bitmap)
                bitmap.Save(outputPath, ImageFormat.Png);
        }
        catch (Exception exception) {
            throw new InvalidOperationException(
                $"Failed to take screenshot: {exception.Message}",
                exception);
        }
    }

    private Bitmap? CaptureProcessWindow(int pid) {
        IntPtr hwnd;
        if (_desktopService.IsOnHiddenDesktop(pid)) {
            hwnd = _desktopService.GetOrFindWindowHandle(pid);
        }
        else {
            try {
                hwnd = Process.GetProcessById(pid).MainWindowHandle;
            }
            catch {
                return null;
            }
        }

        return NativeMethods.CaptureWindow(hwnd);
    }
}