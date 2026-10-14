using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace AstroJint.AspNetCore;

/// <summary>
/// A fixed-size pool of expensive, single-user objects (JavaScript engines). Objects are created lazily or by
/// <see cref="WarmUpAsync"/>, handed out to one caller at a time and, when a caller reports them as unhealthy,
/// disposed of and replaced in the background.
/// </summary>
internal sealed partial class EnginePool<T> : IAsyncDisposable
    where T : class, IDisposable
{
    // A null entry is a wake-up call: a replacement failed, so a waiter should try to create one itself.
    private readonly Channel<T?> _idle = Channel.CreateUnbounded<T?>();
    private readonly Func<Task<T>> _create;
    private readonly ILogger _logger;
    private int _reserved;
    private int _idleCount;
    private volatile bool _disposed;

    public EnginePool(int size, Func<Task<T>> create, ILogger logger)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        Size = size;
        _create = create;
        _logger = logger;
    }

    public int Size { get; }

    /// <summary>Objects that exist, are being created, or are in use.</summary>
    public int Reserved => Volatile.Read(ref _reserved);

    /// <summary>Objects that exist and wait for a caller.</summary>
    public int Idle => Volatile.Read(ref _idleCount);

    /// <summary>Creates up to <see cref="Size"/> objects in parallel. Failures are logged and retried on demand.</summary>
    public Task WarmUpAsync() => Task.WhenAll(Enumerable.Range(0, Size).Select(_ => Task.Run(CreateIntoPoolAsync)));

    /// <summary>Waits for a free object, creating one when the pool has not reached its size yet.</summary>
    public async ValueTask<T> RentAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            while (_idle.Reader.TryRead(out var idle))
            {
                if (idle is not null)
                {
                    Interlocked.Decrement(ref _idleCount);
                    return idle;
                }
            }

            if (TryReserve())
            {
                try
                {
                    return await Task.Run(_create, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    Release();
                    _idle.Writer.TryWrite(null);
                    throw;
                }
            }

            T? slot;
            try
            {
                slot = await _idle.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            if (slot is not null)
            {
                Interlocked.Decrement(ref _idleCount);
                return slot;
            }
        }
    }

    /// <summary>Gives an object back. A discarded object is disposed of and replaced in the background.</summary>
    public void Return(T slot, bool discard)
    {
        if (!discard && TryEnqueue(slot))
        {
            return;
        }

        slot.Dispose();
        Release();

        if (!_disposed)
        {
            _ = Task.Run(CreateIntoPoolAsync);
        }
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _idle.Writer.TryComplete();

        while (_idle.Reader.TryRead(out var slot))
        {
            if (slot is not null)
            {
                Interlocked.Decrement(ref _idleCount);
                slot.Dispose();
            }
        }

        return ValueTask.CompletedTask;
    }

    private async Task CreateIntoPoolAsync()
    {
        if (!TryReserve())
        {
            return;
        }

        try
        {
            var slot = await _create().ConfigureAwait(false);
            if (!TryEnqueue(slot))
            {
                slot.Dispose();
                Release();
            }
        }
        catch (Exception ex)
        {
            Release();
            LogCreateFailed(_logger, ex);
            _idle.Writer.TryWrite(null);
        }
    }

    // Counting before writing keeps Idle from dipping below zero when a waiter takes the slot immediately.
    private bool TryEnqueue(T slot)
    {
        Interlocked.Increment(ref _idleCount);
        if (!_disposed && _idle.Writer.TryWrite(slot))
        {
            return true;
        }

        Interlocked.Decrement(ref _idleCount);
        return false;
    }

    private bool TryReserve()
    {
        while (true)
        {
            var current = Volatile.Read(ref _reserved);
            if (current >= Size)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _reserved, current + 1, current) == current)
            {
                return true;
            }
        }
    }

    private void Release() => Interlocked.Decrement(ref _reserved);

    [LoggerMessage(Level = LogLevel.Error, Message = "Creating a JavaScript engine failed. The next on-demand request will try again.")]
    private static partial void LogCreateFailed(ILogger logger, Exception exception);
}
