using Microsoft.Extensions.Logging.Abstractions;

namespace AstroJint.AspNetCore.Tests;

public class EnginePoolTests
{
    private sealed class Slot(int id) : IDisposable
    {
        public int Id { get; } = id;

        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    private sealed class Factory(Func<int, bool>? fails = null)
    {
        private int _created;

        /// <summary>Calls to the factory, including the ones that failed.</summary>
        public int Calls => Volatile.Read(ref _created);

        public Task<Slot> CreateAsync()
        {
            var call = Interlocked.Increment(ref _created);
            return fails?.Invoke(call) == true
                ? Task.FromException<Slot>(new InvalidOperationException($"Creation {call} failed."))
                : Task.FromResult(new Slot(call));
        }
    }

    private static EnginePool<Slot> CreatePool(int size, Factory factory) => new(size, factory.CreateAsync, NullLogger.Instance);

    private static async Task<T> WithTimeout<T>(Task<T> task)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await task.WaitAsync(cancellation.Token);
    }

    [Fact]
    public async Task Rent_CreatesSlotsOnDemandUpToTheSize()
    {
        var factory = new Factory();
        await using var pool = CreatePool(2, factory);

        var first = await pool.RentAsync(CancellationToken.None);
        var second = await pool.RentAsync(CancellationToken.None);

        Assert.NotSame(first, second);
        Assert.Equal(2, factory.Calls);
        Assert.Equal(2, pool.Reserved);
        Assert.Equal(0, pool.Idle);
    }

    [Fact]
    public async Task Rent_ReusesReturnedSlots()
    {
        var factory = new Factory();
        await using var pool = CreatePool(1, factory);

        var first = await pool.RentAsync(CancellationToken.None);
        pool.Return(first, discard: false);
        var second = await pool.RentAsync(CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal(1, factory.Calls);
    }

    [Fact]
    public async Task Rent_WaitsWhenEverySlotIsInUse()
    {
        await using var pool = CreatePool(1, new Factory());
        var held = await pool.RentAsync(CancellationToken.None);

        var waiting = pool.RentAsync(CancellationToken.None).AsTask();
        await Task.Delay(100);
        Assert.False(waiting.IsCompleted);

        pool.Return(held, discard: false);

        Assert.Same(held, await WithTimeout(waiting));
    }

    [Fact]
    public async Task Rent_StopsWaitingWhenCancelled()
    {
        await using var pool = CreatePool(1, new Factory());
        await pool.RentAsync(CancellationToken.None);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pool.RentAsync(cancellation.Token).AsTask());
    }

    [Fact]
    public async Task Return_WithDiscard_DisposesTheSlotAndCreatesAReplacement()
    {
        var factory = new Factory();
        await using var pool = CreatePool(1, factory);
        var first = await pool.RentAsync(CancellationToken.None);

        pool.Return(first, discard: true);
        var second = await WithTimeout(pool.RentAsync(CancellationToken.None).AsTask());

        Assert.True(first.Disposed);
        Assert.NotSame(first, second);
        Assert.False(second.Disposed);
        Assert.Equal(1, pool.Reserved);
    }

    [Fact]
    public async Task Return_WithDiscard_WakesWaitersWhenTheReplacementFails()
    {
        // Call 1 is the first slot, call 2 the background replacement, call 3 the waiter's own attempt.
        var factory = new Factory(call => call == 2);
        await using var pool = CreatePool(1, factory);
        var first = await pool.RentAsync(CancellationToken.None);
        var waiting = pool.RentAsync(CancellationToken.None).AsTask();

        pool.Return(first, discard: true);
        var second = await WithTimeout(waiting);

        Assert.NotSame(first, second);
        Assert.Equal(3, factory.Calls);
        Assert.Equal(1, pool.Reserved);
    }

    [Fact]
    public async Task Rent_ReleasesItsReservationWhenCreationFails()
    {
        var factory = new Factory(call => call == 1);
        await using var pool = CreatePool(1, factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => pool.RentAsync(CancellationToken.None).AsTask());
        Assert.Equal(0, pool.Reserved);

        var slot = await pool.RentAsync(CancellationToken.None);

        Assert.Equal(2, slot.Id);
    }

    [Fact]
    public async Task WarmUp_FillsThePool()
    {
        var factory = new Factory();
        await using var pool = CreatePool(4, factory);

        await pool.WarmUpAsync();
        var rented = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => pool.RentAsync(CancellationToken.None).AsTask()));

        Assert.Equal(4, factory.Calls);
        Assert.Equal(4, rented.Distinct().Count());
    }

    [Fact]
    public async Task WarmUp_ToleratesFailuresAndLeavesTheRestToLaterRequests()
    {
        var factory = new Factory(call => call <= 2);
        await using var pool = CreatePool(3, factory);

        await pool.WarmUpAsync();

        Assert.Equal(1, pool.Idle);
        Assert.Equal(1, pool.Reserved);

        var rented = new List<Slot>();
        for (var i = 0; i < 3; i++)
        {
            rented.Add(await WithTimeout(pool.RentAsync(CancellationToken.None).AsTask()));
        }

        Assert.Equal(3, rented.Distinct().Count());
        Assert.Equal(3, pool.Reserved);
    }

    [Fact]
    public async Task DisposeAsync_DisposesIdleSlotsAndRejectsNewRentals()
    {
        var factory = new Factory();
        var pool = CreatePool(2, factory);
        await pool.WarmUpAsync();
        var held = await pool.RentAsync(CancellationToken.None);
        var idle = await pool.RentAsync(CancellationToken.None);
        pool.Return(idle, discard: false);

        await pool.DisposeAsync();

        Assert.True(idle.Disposed);
        Assert.False(held.Disposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => pool.RentAsync(CancellationToken.None).AsTask());

        pool.Return(held, discard: false);
        Assert.True(held.Disposed);
    }

    [Fact]
    public async Task DisposeAsync_WakesWaiters()
    {
        var pool = CreatePool(1, new Factory());
        await pool.RentAsync(CancellationToken.None);
        var waiting = pool.RentAsync(CancellationToken.None).AsTask();
        await Task.Delay(50);

        await pool.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => WithTimeout(waiting));
    }

    [Fact]
    public async Task ManyConcurrentRentals_NeverExceedTheSize()
    {
        var factory = new Factory();
        await using var pool = CreatePool(3, factory);
        var inUse = 0;
        var peak = 0;

        await Task.WhenAll(Enumerable.Range(0, 50).Select(async _ =>
        {
            var slot = await pool.RentAsync(CancellationToken.None);
            var now = Interlocked.Increment(ref inUse);
            InterlockedMax(ref peak, now);
            await Task.Delay(5);
            Interlocked.Decrement(ref inUse);
            pool.Return(slot, discard: false);
        }));

        Assert.InRange(peak, 1, 3);
        Assert.InRange(factory.Calls, 1, 3);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)))
        {
            if (Interlocked.CompareExchange(ref target, value, current) == current)
            {
                return;
            }
        }
    }
}
