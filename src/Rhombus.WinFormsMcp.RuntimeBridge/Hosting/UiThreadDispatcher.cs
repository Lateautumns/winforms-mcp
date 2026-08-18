using System.Threading;
using System.Windows.Forms;

namespace Rhombus.WinFormsMcp.RuntimeBridge.Hosting;

/// <summary>
/// Executes all WinForms object access on the target application's UI thread.
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

        if (_invoker is null && _context is null)
            return Task.FromResult(callback());

        if (_invoker is not null && !_invoker.InvokeRequired)
            return Task.FromResult(callback());

        if (_context is not null && ReferenceEquals(SynchronizationContext.Current, _context))
            return Task.FromResult(callback());

        var completion = new TaskCompletionSource<T>();
        CancellationTokenRegistration registration = default;
        registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));

        void Run() {
            try {
                if (!completion.Task.IsCanceled)
                    completion.TrySetResult(callback());
            }
            catch (Exception ex) {
                completion.TrySetException(ex);
            }
            finally {
                registration.Dispose();
            }
        }

        try {
            if (_invoker is not null && !_invoker.IsDisposed)
                _invoker.BeginInvoke((Action)Run);
            else if (_context is not null)
                _context.Post(_ => Run(), null);
            else
                Run();
        }
        catch (Exception ex) {
            registration.Dispose();
            completion.TrySetException(ex);
        }

        return completion.Task;
    }
}
