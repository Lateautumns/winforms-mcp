using System.Threading;
using System.Windows.Forms;

namespace Rhombus.WinFormsMcp.RuntimeBridge.Hosting;

/// <summary>
/// Executes all WinForms object access on the target application's UI thread.
/// Requests never fall back to the calling (pipe) thread: when no UI dispatch
/// target is available, or the bound control has become invalid, the request
/// fails explicitly instead of touching controls cross-thread.
/// </summary>
internal sealed class UiThreadDispatcher {
    private readonly Control? _invoker;
    private readonly SynchronizationContext? _context;

    public UiThreadDispatcher(Control? invoker) {
        _invoker = invoker;
        _context = SynchronizationContext.Current;
    }

    public Task<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        if (_invoker is null) {
            if (_context is null) {
                return Task.FromException<T>(new InvalidOperationException(
                    "RuntimeBridge has no WinForms UI dispatch target. Start the bridge from the WinForms UI thread, or bind it explicitly: McpRuntimeBridge.StartForControl(form) in Form.Shown."));
            }

            if (ReferenceEquals(SynchronizationContext.Current, _context))
                return Task.FromResult(callback());
            return PostAsync(callback, cancellationToken);
        }

        if (_invoker.IsDisposed || _invoker.Disposing) {
            return Task.FromException<T>(new ObjectDisposedException(
                nameof(Control),
                "The control bound to RuntimeBridge is disposed or is being disposed; the request failed instead of accessing the control from the pipe thread."));
        }

        if (!_invoker.IsHandleCreated) {
            return Task.FromException<T>(new InvalidOperationException(
                "The control bound to RuntimeBridge no longer has a window handle; the request failed instead of accessing the control from the pipe thread. Bind the bridge again after the control handle is recreated."));
        }

        // Always marshal through the bound control, even when InvokeRequired reports
        // false. WinForms also returns false when no suitable handle can be found,
        // which must never cause RuntimeBridge to execute UI work on the pipe thread.
        return PostAsync(callback, cancellationToken);
    }

    private Task<T> PostAsync<T>(Func<T> callback, CancellationToken cancellationToken) {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration registration = default;
        registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));

        void Run() {
            try {
                if (completion.Task.IsCanceled)
                    return;

                if (_invoker is { IsDisposed: true } or { Disposing: true }) {
                    completion.TrySetException(new ObjectDisposedException(
                        nameof(Control),
                        "The control bound to RuntimeBridge was disposed before the UI callback could run."));
                }
                else if (_invoker is { IsHandleCreated: false }) {
                    completion.TrySetException(new InvalidOperationException(
                        "The control bound to RuntimeBridge lost its window handle before the UI callback could run."));
                }
                else {
                    completion.TrySetResult(callback());
                }
            }
            catch (Exception ex) {
                completion.TrySetException(ex);
            }
            finally {
                registration.Dispose();
            }
        }

        try {
            if (_invoker is not null)
                _invoker.BeginInvoke((Action)Run);
            else
                _context!.Post(_ => Run(), null);
        }
        catch (Exception ex) {
            registration.Dispose();
            completion.TrySetException(ex);
        }

        return completion.Task;
    }
}