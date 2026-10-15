using System.Numerics;

namespace AstroJint.LoadTest;

/// <summary>Counts latencies in microseconds: exact below one millisecond, within 1.6% above it. Recording allocates nothing.</summary>
internal sealed class LatencyHistogram
{
    private const int LinearLimit = 1024;
    private const int SubBucketBits = 6;
    private const int SubBuckets = 1 << SubBucketBits;
    private const int FirstExponent = 10;
    private const int Buckets = LinearLimit + ((32 - FirstExponent) * SubBuckets);

    private readonly long[] _counts = new long[Buckets];

    public long Count { get; private set; }

    public long TotalMicroseconds { get; private set; }

    public long MaxMicroseconds { get; private set; }

    public void Record(long microseconds)
    {
        var value = Math.Clamp(microseconds, 0, uint.MaxValue);
        _counts[IndexOf((ulong)value)]++;
        Count++;
        TotalMicroseconds += value;
        if (value > MaxMicroseconds)
        {
            MaxMicroseconds = value;
        }
    }

    public void Add(LatencyHistogram other)
    {
        for (var i = 0; i < _counts.Length; i++)
        {
            _counts[i] += other._counts[i];
        }

        Count += other.Count;
        TotalMicroseconds += other.TotalMicroseconds;
        MaxMicroseconds = Math.Max(MaxMicroseconds, other.MaxMicroseconds);
    }

    /// <summary>The latency that <paramref name="percent"/> of the requests stayed within, in microseconds.</summary>
    public long Percentile(double percent)
    {
        if (Count == 0)
        {
            return 0;
        }

        var rank = Math.Max(1, (long)Math.Ceiling(Count * percent / 100));
        long seen = 0;
        for (var i = 0; i < _counts.Length; i++)
        {
            seen += _counts[i];
            if (seen >= rank)
            {
                return Math.Min(UpperBound(i), MaxMicroseconds);
            }
        }

        return MaxMicroseconds;
    }

    private static int IndexOf(ulong value)
    {
        if (value < LinearLimit)
        {
            return (int)value;
        }

        var exponent = BitOperations.Log2(value);
        var sub = (int)((value >> (exponent - SubBucketBits)) & (SubBuckets - 1));
        return LinearLimit + ((exponent - FirstExponent) * SubBuckets) + sub;
    }

    private static long UpperBound(int index)
    {
        if (index < LinearLimit)
        {
            return index;
        }

        var offset = index - LinearLimit;
        var shift = (offset / SubBuckets) + FirstExponent - SubBucketBits;
        var sub = offset % SubBuckets;
        return ((long)(SubBuckets + sub) << shift) + (1L << shift) - 1;
    }
}
