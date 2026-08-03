using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Fenrir.Security.RateLimiting;

public readonly record struct LoginFailureSnapshot(
    int SourceAccountFailureCount,
    int SourceTotalFailureCount,
    int DistinctOtherSourceCount);

public sealed class LoginIpRateLimiter
{
    private const int Capacity = 5;
    private const double TokensPerSecond = 1d / 10d;

    private const int MaxTrackedFailureKeys = 50_000;
    private const int MaxTrackedContendedAccounts = 50_000;

    private const int MaxCountedFailures = 64;
    private const int MaxContentionSlots = 4;

    private const double FailureDecaySeconds = 120d;

    private static readonly long PurgeIntervalTicks = Stopwatch.Frequency * 5;

    private static readonly long IdleTicksBeforePurge = Stopwatch.Frequency * 600;

    private static readonly long ContentionTtlTicks = Stopwatch.Frequency * 900;

    private static readonly long ReportIntervalTicks = Stopwatch.Frequency * 900;

    private static readonly double FailureDecayTicks = Stopwatch.Frequency * FailureDecaySeconds;

    private readonly ConcurrentDictionary<AccountSourceKey, DecayingFailureCounter> _accountFailures = new();

    private readonly ConcurrentDictionary<int, ContentionLedger> _contention = new();

    private readonly ConcurrentDictionary<string, SourceEntry> _sources = new();

    private long _nextPurgeTimestamp;

    public bool TryConsume(IPEndPoint? remoteEndPoint)
    {
        if (remoteEndPoint is null)
            return true;

        PurgeIfDue();

        var entry = GetOrAddSource(NormalizeSource(remoteEndPoint.Address));
        entry.Touch();

        return entry.Bucket.TryConsume();
    }

    public LoginFailureSnapshot Snapshot(IPEndPoint? remoteEndPoint, int accountId)
    {
        if (remoteEndPoint is null)
            return default;

        var now = Stopwatch.GetTimestamp();
        var source = NormalizeSource(remoteEndPoint.Address);

        var sourceTotal = _sources.TryGetValue(source, out var entry) ? entry.Failures.Count(now) : 0;

        var sourceAccount = accountId > 0 &&
                            _accountFailures.TryGetValue(new AccountSourceKey(source, accountId), out var ledger)
            ? ledger.Count(now)
            : 0;

        var distinctOtherSources = accountId > 0 && _contention.TryGetValue(accountId, out var contention)
            ? contention.DistinctOtherSourceCount(source, now)
            : 0;

        return new LoginFailureSnapshot(sourceAccount, sourceTotal, distinctOtherSources);
    }

    public void RecordUnknownAccountFailure(IPEndPoint? remoteEndPoint)
    {
        if (remoteEndPoint is not null)
            GetOrAddSource(NormalizeSource(remoteEndPoint.Address)).Failures.Increment(Stopwatch.GetTimestamp());
    }

    public void RecordFailure(IPEndPoint? remoteEndPoint, int accountId)
    {
        if (remoteEndPoint is null)
            return;

        var now = Stopwatch.GetTimestamp();
        var source = NormalizeSource(remoteEndPoint.Address);

        GetOrAddSource(source).Failures.Increment(now);

        if (accountId <= 0)
            return;

        var key = new AccountSourceKey(source, accountId);

        if (_accountFailures.TryGetValue(key, out var ledger))
            ledger.Increment(now);
        else if (HasRoomFor(_accountFailures, MaxTrackedFailureKeys))
            _accountFailures.GetOrAdd(key, static _ => new DecayingFailureCounter()).Increment(now);

        if (_contention.TryGetValue(accountId, out var contention))
            contention.Observe(source, now);
        else if (HasRoomFor(_contention, MaxTrackedContendedAccounts))
            _contention.GetOrAdd(accountId, static _ => new ContentionLedger()).Observe(source, now);
    }

    public void ClearFailures(IPEndPoint? remoteEndPoint, int accountId)
    {
        if (remoteEndPoint is null || accountId <= 0)
            return;

        var now = Stopwatch.GetTimestamp();
        var source = NormalizeSource(remoteEndPoint.Address);

        if (_accountFailures.TryRemove(new AccountSourceKey(source, accountId), out var ledger) &&
            _sources.TryGetValue(source, out var entry))
            entry.Failures.Subtract(ledger.Count(now), now);

        _contention.TryRemove(accountId, out _);
    }

    public bool TryClaimThrottleReport(IPEndPoint? remoteEndPoint, int accountId)
    {
        if (remoteEndPoint is null)
            return false;

        var now = Stopwatch.GetTimestamp();
        var source = NormalizeSource(remoteEndPoint.Address);

        if (accountId > 0 && _accountFailures.TryGetValue(new AccountSourceKey(source, accountId), out var ledger))
            return ledger.TryClaimReport(now);

        return _sources.TryGetValue(source, out var entry) && entry.Failures.TryClaimReport(now);
    }

    public bool TryTakeRetrySlot(IPEndPoint? remoteEndPoint, double delaySeconds)
    {
        if (remoteEndPoint is null)
            return true;

        var entry = GetOrAddSource(NormalizeSource(remoteEndPoint.Address));

        return entry.Failures.TryTakeRetrySlot(Stopwatch.GetTimestamp(), (long)(delaySeconds * Stopwatch.Frequency));
    }

    private SourceEntry GetOrAddSource(string source)
    {
        return _sources.GetOrAdd(source, static _ => new SourceEntry(new TokenBucket(Capacity, TokensPerSecond)));
    }

    private bool HasRoomFor<TKey, TValue>(ConcurrentDictionary<TKey, TValue> map, int cap) where TKey : notnull
    {
        if (map.Count < cap)
            return true;

        PurgeIfDue();
        return map.Count < cap;
    }

    private static string NormalizeSource(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return address.ToString();

        Span<byte> octets = stackalloc byte[16];

        if (!address.TryWriteBytes(octets, out var written) || written != octets.Length)
            return address.ToString();

        octets[8..].Clear();
        return new IPAddress(octets).ToString();
    }

    private void PurgeIfDue()
    {
        var now = Stopwatch.GetTimestamp();
        var due = Interlocked.Read(ref _nextPurgeTimestamp);

        if (now < due || Interlocked.CompareExchange(ref _nextPurgeTimestamp, now + PurgeIntervalTicks, due) != due)
            return;

        foreach (var (source, entry) in _sources)
            if (now - Interlocked.Read(ref entry.LastAccessTimestamp) > IdleTicksBeforePurge &&
                entry.Failures.IsIdle(now))
                _sources.TryRemove(source, out _);

        foreach (var (key, ledger) in _accountFailures)
            if (ledger.IsIdle(now))
                _accountFailures.TryRemove(key, out _);

        foreach (var (accountId, ledger) in _contention)
            if (ledger.IsIdle(now))
                _contention.TryRemove(accountId, out _);
    }

    private readonly record struct AccountSourceKey(string Source, int AccountId);

    private sealed class SourceEntry(TokenBucket bucket)
    {
        public readonly TokenBucket Bucket = bucket;
        public readonly DecayingFailureCounter Failures = new();
        public long LastAccessTimestamp = Stopwatch.GetTimestamp();

        public void Touch()
        {
            Interlocked.Exchange(ref LastAccessTimestamp, Stopwatch.GetTimestamp());
        }
    }

    private sealed class DecayingFailureCounter
    {
        private readonly Lock _gate = new();
        private long _nextReportTimestamp;
        private long _nextRetryTimestamp;
        private long _updatedTimestamp = Stopwatch.GetTimestamp();
        private double _value;
        private long _zeroTimestamp = Stopwatch.GetTimestamp();

        public bool IsIdle(long now)
        {
            return now >= Interlocked.Read(ref _zeroTimestamp);
        }

        public int Count(long now)
        {
            if (IsIdle(now))
                return 0;

            lock (_gate)
                return (int)DecayedValue(now);
        }

        public void Increment(long now)
        {
            lock (_gate)
                Set(Math.Min(DecayedValue(now) + 1d, MaxCountedFailures), now);
        }

        public void Subtract(int amount, long now)
        {
            if (amount <= 0)
                return;

            lock (_gate)
                Set(Math.Max(DecayedValue(now) - amount, 0d), now);
        }

        public bool TryClaimReport(long now)
        {
            lock (_gate)
            {
                if (now < _nextReportTimestamp)
                    return false;

                _nextReportTimestamp = now + ReportIntervalTicks;
                return true;
            }
        }

        public bool TryTakeRetrySlot(long now, long delayTicks)
        {
            lock (_gate)
            {
                if (now < _nextRetryTimestamp)
                    return false;

                _nextRetryTimestamp = now + delayTicks;
                return true;
            }
        }

        private double DecayedValue(long now)
        {
            var elapsed = now - _updatedTimestamp;
            return elapsed <= 0 ? _value : Math.Max(_value - elapsed / FailureDecayTicks, 0d);
        }

        private void Set(double value, long now)
        {
            _value = value;
            _updatedTimestamp = now;
            Interlocked.Exchange(ref _zeroTimestamp, now + (long)(value * FailureDecayTicks));
        }
    }

    private sealed class ContentionLedger
    {
        private readonly Lock _gate = new();
        private readonly long[] _seen = new long[MaxContentionSlots];
        private readonly string?[] _slots = new string?[MaxContentionSlots];
        private long _newestTimestamp = Stopwatch.GetTimestamp();

        public bool IsIdle(long now)
        {
            return now - Interlocked.Read(ref _newestTimestamp) > ContentionTtlTicks;
        }

        public void Observe(string source, long now)
        {
            lock (_gate)
            {
                var free = -1;
                var oldest = 0;

                for (var i = 0; i < _slots.Length; i++)
                {
                    if (string.Equals(_slots[i], source, StringComparison.Ordinal))
                    {
                        _seen[i] = now;
                        Interlocked.Exchange(ref _newestTimestamp, now);
                        return;
                    }

                    if (_slots[i] is null || now - _seen[i] > ContentionTtlTicks)
                        free = i;
                    else if (_seen[i] < _seen[oldest])
                        oldest = i;
                }

                var slot = free >= 0 ? free : oldest;
                _slots[slot] = source;
                _seen[slot] = now;
                Interlocked.Exchange(ref _newestTimestamp, now);
            }
        }

        public int DistinctOtherSourceCount(string excludedSource, long now)
        {
            var count = 0;

            lock (_gate)
            {
                for (var i = 0; i < _slots.Length; i++)
                    if (_slots[i] is not null && now - _seen[i] <= ContentionTtlTicks &&
                        !string.Equals(_slots[i], excludedSource, StringComparison.Ordinal))
                        count++;
            }

            return count;
        }
    }
}
