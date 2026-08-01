using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class WorldClockPushSystem(TimeProvider? timeProvider = null) : ISimulationSystem
{
    public const int NowTimeClassificationSort = 98;

    public const int ThrottleStateUnset = 0;

    public const int ThrottleStateRearmPending = 1;

    public const int ThrottleStateBlocked = 2;

    public const int ThrottleStateJustSent = 3;

    private const int RearmSecondThreshold = 2;

    private const int BlockSecondThreshold = 3;

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var now = _timeProvider.GetUtcNow();
        var secondOfMinute = now.Second;
        var packet = BuildPacket(now);

        foreach (var state in zone.Players)
        {
            var throttleState = state.WorldClockPushThrottleState;

            if (throttleState == ThrottleStateBlocked && secondOfMinute < RearmSecondThreshold)
                throttleState = ThrottleStateRearmPending;

            if (secondOfMinute >= BlockSecondThreshold)
                throttleState = ThrottleStateBlocked;

            if (throttleState is ThrottleStateBlocked or ThrottleStateJustSent)
            {
                state.WorldClockPushThrottleState = throttleState;
                continue;
            }

            state.WorldClockPushThrottleState = ThrottleStateJustSent;
            state.Session.Send(packet);
        }
    }

    public void SendForced(PlayerRuntimeState state)
    {
        state.WorldClockPushThrottleState = ThrottleStateJustSent;
        state.Session.Send(BuildPacket(_timeProvider.GetUtcNow()));
    }

    public static int EncodePackedTime(DateTimeOffset now)
    {
        var monthZeroBased = now.Month - 1;
        return monthZeroBased * 10_000_000 + now.Day * 100_000 + now.Hour * 1_000 + now.Minute * 10 +
               (int)now.DayOfWeek;
    }

    public static AvatarStatUpdateResponse BuildPacket(DateTimeOffset now)
    {
        return new AvatarStatUpdateResponse
        {
            Sort = NowTimeClassificationSort,
            Value = EncodePackedTime(now),
            Value2 = 0
        };
    }
}
