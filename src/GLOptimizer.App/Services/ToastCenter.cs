using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Notifications;

namespace GLOptimizer.App.Services;

public interface IToastCenter
{
    string? Current { get; }

    event EventHandler? Changed;

    void Show(string message);
}

public sealed class ToastCenter : IToastCenter
{
    private readonly ToastDeduper _deduper = new();
    private readonly IClock _clock;

    public ToastCenter(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public string? Current { get; private set; }

    public event EventHandler? Changed;

    public void Show(string message)
    {
        if (!_deduper.TryAccept(message, _clock.UtcNow))
        {
            return;
        }

        Current = message;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
