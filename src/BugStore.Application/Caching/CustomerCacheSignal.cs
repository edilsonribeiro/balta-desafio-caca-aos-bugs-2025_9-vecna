using Microsoft.Extensions.Primitives;

namespace BugStore.Application.Caching;

public interface ICustomerCacheSignal
{
    IChangeToken CreateToken();
    void SignalChange();
}

public sealed class CustomerCacheSignal : ICustomerCacheSignal
{
    private readonly object _syncRoot = new();
    private CancellationTokenSource _cancellationTokenSource = new();

    public IChangeToken CreateToken()
    {
        lock (_syncRoot)
        {
            return new CancellationChangeToken(_cancellationTokenSource.Token);
        }
    }

    public void SignalChange()
    {
        CancellationTokenSource previous;

        lock (_syncRoot)
        {
            previous = _cancellationTokenSource;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        try
        {
            previous.Cancel();
        }
        finally
        {
            previous.Dispose();
        }
    }
}
