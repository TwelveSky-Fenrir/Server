using System.Collections.Concurrent;
using System.Collections.Immutable;
using Fenrir.Core.Opcodes;

namespace Fenrir.Application.Login.Sessions;

public sealed class LoginIdleClock
{
    private readonly ConcurrentDictionary<long, SessionClock> _clocks = new();

    public static bool Rearms(byte opcode)
    {
        return opcode switch
        {
            Opcodes.Login.Incoming.LoginKeepAlive => true,
            Opcodes.Login.Incoming.CreateMousePin => true,
            Opcodes.Login.Incoming.ChangeMousePin => true,
            Opcodes.Login.Incoming.VerifyMousePin => true,
            Opcodes.Login.Incoming.CreateAvatar => true,
            Opcodes.Login.Incoming.DeleteAvatar => true,
            Opcodes.Login.Incoming.RenameAvatar => true,
            Opcodes.Login.Incoming.ClaimGift => true,
            Opcodes.Login.Incoming.ZoneTransfer => true,
            Opcodes.Login.Incoming.ZoneTransferFailure => true,
            Opcodes.Login.Incoming.GiftList => true,
            _ => false
        };
    }

    public void Arm(LoginClientSession session, DateTimeOffset nowUtc)
    {
        _clocks[session.SessionId] = new SessionClock(session, nowUtc);
    }

    public void Rearm(long sessionId, DateTimeOffset nowUtc)
    {
        if (_clocks.TryGetValue(sessionId, out var clock))
            clock.Rearm(nowUtc);
    }

    public void Release(long sessionId)
    {
        _clocks.TryRemove(sessionId, out _);
    }

    public ImmutableArray<ExpiredLoginSession> SnapshotExpired(TimeSpan idleTimeout, DateTimeOffset nowUtc)
    {
        var builder = ImmutableArray.CreateBuilder<ExpiredLoginSession>();

        foreach (var clock in _clocks.Values)
        {
            var rearmedUtc = clock.RearmedUtc;

            if (nowUtc - rearmedUtc > idleTimeout)
                builder.Add(new ExpiredLoginSession(clock.Session, rearmedUtc));
        }

        return builder.ToImmutable();
    }

    public ImmutableArray<ExpiredLoginHandshake> SnapshotPreAuthenticationExpired(
        TimeSpan handshakeTimeout, DateTimeOffset nowUtc)
    {
        var builder = ImmutableArray.CreateBuilder<ExpiredLoginHandshake>();

        foreach (var clock in _clocks.Values)
            if (clock.Session.IsPreAuthentication && nowUtc - clock.ArmedUtc > handshakeTimeout)
                builder.Add(new ExpiredLoginHandshake(clock.Session, clock.ArmedUtc));

        return builder.ToImmutable();
    }

    private sealed class SessionClock(LoginClientSession session, DateTimeOffset armedUtc)
    {
        private long _rearmedUtcTicks = armedUtc.UtcTicks;

        public LoginClientSession Session { get; } = session;

        public DateTimeOffset ArmedUtc { get; } = armedUtc;

        public DateTimeOffset RearmedUtc => new(Volatile.Read(ref _rearmedUtcTicks), TimeSpan.Zero);

        public void Rearm(DateTimeOffset nowUtc)
        {
            Volatile.Write(ref _rearmedUtcTicks, nowUtc.UtcTicks);
        }
    }
}
