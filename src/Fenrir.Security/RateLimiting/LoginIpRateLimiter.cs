using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Fenrir.Security.RateLimiting;

public readonly record struct LoginFailureSnapshot(
    int SourceAccountFailureCount,
    int SourceSprayFailureCount,
    int DistinctOtherSourceCount);

public sealed class LoginIpRateLimiter
{
    private const int Capacity = 10;
    private const double TokensPerSecond = 0.25d;

    private const int MaxTrackedSources = 100_000;

    private const int OverflowStripeCount = 512;

    private const int MaxTrackedFailureKeys = 50_000;
    private const int MaxTrackedContendedAccounts = 50_000;

    private const int MaxCountedFailures = 64;
    private const int MaxContentionSlots = 4;

    private const int MaxKnownAccountSlots = 32;

    private const int MaxRefundableSourceContribution = 10;

    private const double FailureDecaySeconds = 120d;

    private static readonly long PurgeIntervalTicks = Stopwatch.Frequency * 5;

    private static readonly long IdleTicksBeforePurge = Stopwatch.Frequency * 600;

    private static readonly long ContentionTtlTicks = Stopwatch.Frequency * 900;

    private static readonly long KnownAccountTtlTicks = Stopwatch.Frequency * 21_600;

    private static readonly long ReportIntervalTicks = Stopwatch.Frequency * 900;

    private static readonly double FailureDecayTicks = Stopwatch.Frequency * FailureDecaySeconds;

    private readonly ConcurrentDictionary<AccountSourceKey, AccountFailureLedger> _accountFailures = new();

    private readonly ConcurrentDictionary<int, ContentionLedger> _contention = new();

    private readonly ConcurrentDictionary<string, SourceEntry> _sources = new();

    private long _nextPurgeTimestamp;

    private SourceEntry[]? _overflowStripes;

    private int _sourceCount;

    public bool TryConsume(IPEndPoint? remoteEndPoint)
    {
        if (remoteEndPoint is null)
            return true;

        PurgeIfDue();

        var entry = GetOrAddSource(NormalizeSource(remoteEndPoint.Address));
        entry.Touch();

        return entry.Bucket.TryConsume();
    }

    public void RefundUnverifiedAttempt(IPEndPoint? remoteEndPoint)
    {
        if (remoteEndPoint is null)
            return;

        TryGetSource(NormalizeSource(remoteEndPoint.Address))?.Bucket.Refund();
    }

    public LoginFailureSnapshot Snapshot(IPEndPoint? remoteEndPoint, int accountId)
    {
        if (remoteEndPoint is null)
            return default;

        var now = Stopwatch.GetTimestamp();
        var source = NormalizeSource(remoteEndPoint.Address);
        var entry = TryGetSource(source);

        var spray = entry is not null && (accountId <= 0 || !entry.IsKnownAccount(accountId, now))
            ? entry.Failures.Count(now)
            : 0;

        var sourceAccount = accountId > 0 &&
                            _accountFailures.TryGetValue(new AccountSourceKey(source, accountId), out var ledger)
            ? ledger.Failures.Count(now)
            : 0;

        var distinctOtherSources = accountId > 0 && _contention.TryGetValue(accountId, out var contention)
            ? contention.DistinctOtherSourceCount(source, now)
            : 0;

        return new LoginFailureSnapshot(sourceAccount, spray, distinctOtherSources);
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

        var appliedToSource = GetOrAddSource(source).Failures.Increment(now);

        if (accountId <= 0)
            return;

        if (GetOrAddAccountLedger(new AccountSourceKey(source, accountId)) is { } ledger)
        {
            ledger.Failures.Increment(now);
            ledger.SourceContribution.Add(appliedToSource, now);
        }

        if (_contention.TryGetValue(accountId, out var contention))
            contention.Observe(source, now);
        else if (HasRoomFor(_contention, MaxTrackedContendedAccounts))
            _contention.GetOrAdd(accountId, static _ => new ContentionLedger()).Observe(source, now);
    }

    public void RecordSuccess(IPEndPoint? remoteEndPoint, int accountId)
    {
        if (remoteEndPoint is null)
            return;

        var now = Stopwatch.GetTimestamp();
        var source = NormalizeSource(remoteEndPoint.Address);
        var entry = GetOrAddSource(source);

        entry.Bucket.Refund();

        if (accountId <= 0)
            return;

        entry.RememberAccount(accountId, now);

        if (_accountFailures.TryRemove(new AccountSourceKey(source, accountId), out var ledger))
            entry.Failures.Subtract(
                Math.Min(ledger.SourceContribution.Value(now), MaxRefundableSourceContribution), now);

        _contention.TryRemove(accountId, out _);
    }

    public bool TryClaimThrottleReport(IPEndPoint? remoteEndPoint, int accountId)
    {
        if (remoteEndPoint is null)
            return false;

        var now = Stopwatch.GetTimestamp();
        var source = NormalizeSource(remoteEndPoint.Address);

        if (accountId > 0 && _accountFailures.TryGetValue(new AccountSourceKey(source, accountId), out var ledger))
            return ledger.Failures.TryClaimReport(now);

        return TryGetSource(source) is { } entry && entry.Failures.TryClaimReport(now);
    }

    public bool TryTakeAccountRetrySlot(IPEndPoint? remoteEndPoint, int accountId, double delaySeconds)
    {
        if (remoteEndPoint is null)
            return true;

        var source = NormalizeSource(remoteEndPoint.Address);

        if (accountId > 0 && GetOrAddAccountLedger(new AccountSourceKey(source, accountId)) is { } ledger)
            return ledger.Failures.TryTakeRetrySlot(Stopwatch.GetTimestamp(), ToTicks(delaySeconds));

        return TryTakeSourceRetrySlot(remoteEndPoint, delaySeconds);
    }

    public bool TryTakeSourceRetrySlot(IPEndPoint? remoteEndPoint, double delaySeconds)
    {
        if (remoteEndPoint is null)
            return true;

        var entry = GetOrAddSource(NormalizeSource(remoteEndPoint.Address));

        return entry.Failures.TryTakeRetrySlot(Stopwatch.GetTimestamp(), ToTicks(delaySeconds));
    }

    private static long ToTicks(double seconds)
    {
        return (long)(Math.Max(seconds, 0d) * Stopwatch.Frequency);
    }

    private SourceEntry GetOrAddSource(string source)
    {
        while (true)
        {
            if (_sources.TryGetValue(source, out var existing))
                return existing;

            if (!HasSourceRoom())
                return OverflowStripeFor(source);

            var created = new SourceEntry(new TokenBucket(Capacity, TokensPerSecond));

            if (!_sources.TryAdd(source, created))
                continue;

            Interlocked.Increment(ref _sourceCount);
            return created;
        }
    }

    private SourceEntry? TryGetSource(string source)
    {
        if (_sources.TryGetValue(source, out var entry))
            return entry;

        return Volatile.Read(ref _overflowStripes) is { } stripes ? stripes[StripeIndexOf(source)] : null;
    }

    private bool HasSourceRoom()
    {
        if (Volatile.Read(ref _sourceCount) < MaxTrackedSources)
            return true;

        PurgeIfDue();
        return Volatile.Read(ref _sourceCount) < MaxTrackedSources;
    }

    private SourceEntry OverflowStripeFor(string source)
    {
        var stripes = Volatile.Read(ref _overflowStripes);

        if (stripes is null)
        {
            var fresh = new SourceEntry[OverflowStripeCount];

            for (var i = 0; i < fresh.Length; i++)
                fresh[i] = new SourceEntry(new TokenBucket(Capacity, TokensPerSecond), true);

            stripes = Interlocked.CompareExchange(ref _overflowStripes, fresh, null) ?? fresh;
        }

        return stripes[StripeIndexOf(source)];
    }

    private static int StripeIndexOf(string source)
    {
        return (int)((uint)source.GetHashCode() % OverflowStripeCount);
    }

    private AccountFailureLedger? GetOrAddAccountLedger(AccountSourceKey key)
    {
        if (_accountFailures.TryGetValue(key, out var ledger))
            return ledger;

        return HasRoomFor(_accountFailures, MaxTrackedFailureKeys)
            ? _accountFailures.GetOrAdd(key, static _ => new AccountFailureLedger())
            : null;
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

        foreach (var pair in _sources)
        {
            var entry = pair.Value;

            if (now - Interlocked.Read(ref entry.LastAccessTimestamp) > IdleTicksBeforePurge && entry.IsIdle(now) &&
                _sources.TryRemove(pair))
                Interlocked.Decrement(ref _sourceCount);
        }

        foreach (var (key, ledger) in _accountFailures)
            if (ledger.IsIdle(now))
                _accountFailures.TryRemove(key, out _);

        foreach (var (accountId, ledger) in _contention)
            if (ledger.IsIdle(now))
                _contention.TryRemove(accountId, out _);
    }

    private readonly record struct AccountSourceKey(string Source, int AccountId);

    private sealed class SourceEntry(TokenBucket bucket, bool striped = false)
    {
        public readonly TokenBucket Bucket = bucket;
        public readonly DecayingFailureCounter Failures = new();

        private KnownAccountLedger? _knownAccounts;
        public long LastAccessTimestamp = Stopwatch.GetTimestamp();

        public void Touch()
        {
            Interlocked.Exchange(ref LastAccessTimestamp, Stopwatch.GetTimestamp());
        }

        public bool IsIdle(long now)
        {
            return Failures.IsIdle(now);
        }

        public bool IsKnownAccount(int accountId, long now)
        {
            return !striped && Volatile.Read(ref _knownAccounts) is { } known && known.Contains(accountId, now);
        }

        public void RememberAccount(int accountId, long now)
        {
            if (striped)
                return;

            var known = Volatile.Read(ref _knownAccounts);

            if (known is null)
            {
                var fresh = new KnownAccountLedger();
                known = Interlocked.CompareExchange(ref _knownAccounts, fresh, null) ?? fresh;
            }

            known.Observe(accountId, now);
        }
    }

    private sealed class AccountFailureLedger
    {
        public readonly DecayingFailureCounter Failures = new();

        public readonly DecayingFailureCounter SourceContribution = new();

        public bool IsIdle(long now)
        {
            return Failures.IsIdle(now) && SourceContribution.IsIdle(now);
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
            return (int)Value(now);
        }

        public double Value(long now)
        {
            if (IsIdle(now))
                return 0d;

            lock (_gate)
            {
                return DecayedValue(now);
            }
        }

        public double Increment(long now)
        {
            return Add(1d, now);
        }

        public double Add(double amount, long now)
        {
            if (amount <= 0d)
                return 0d;

            lock (_gate)
            {
                var current = DecayedValue(now);
                var next = Math.Min(current + amount, MaxCountedFailures);
                Set(next, now);
                return next - current;
            }
        }

        public void Subtract(double amount, long now)
        {
            if (amount <= 0d)
                return;

            lock (_gate)
            {
                Set(Math.Max(DecayedValue(now) - amount, 0d), now);
            }
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

    private sealed class KnownAccountLedger
    {
        private readonly int[] _accounts = new int[MaxKnownAccountSlots];
        private readonly Lock _gate = new();
        private readonly long[] _seen = new long[MaxKnownAccountSlots];

        public bool Contains(int accountId, long now)
        {
            lock (_gate)
            {
                for (var i = 0; i < _accounts.Length; i++)
                    if (_accounts[i] == accountId && now - _seen[i] <= KnownAccountTtlTicks)
                        return true;
            }

            return false;
        }

        public void Observe(int accountId, long now)
        {
            lock (_gate)
            {
                var free = -1;
                var oldest = 0;

                for (var i = 0; i < _accounts.Length; i++)
                {
                    if (_accounts[i] == accountId)
                    {
                        _seen[i] = now;
                        return;
                    }

                    if (_accounts[i] == 0 || now - _seen[i] > KnownAccountTtlTicks)
                        free = i;
                    else if (_seen[i] < _seen[oldest])
                        oldest = i;
                }

                var slot = free >= 0 ? free : oldest;
                _accounts[slot] = accountId;
                _seen[slot] = now;
            }
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
