using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class TimedBuffCountdownSystem : ISimulationSystem
{
    private const int MaxMountExp = 100000;

    internal static readonly FrozenSet<short> GroupAExcludedMaps =
        new short[] { 1, 6, 11, 140, 38, 37, 119, 124, 49, 51, 53, 194, 195, 267 }.ToFrozenSet();

    private static readonly FrozenSet<short> GroupBIncludedMaps =
        new short[] { 38, 2, 3, 4, 7, 8, 9, 12, 13, 14, 141, 142, 143, 49, 51, 53, 194, 195, 267 }.ToFrozenSet();

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var groupA = !GroupAExcludedMaps.Contains(zone.MapId);
        var groupB = GroupBIncludedMaps.Contains(zone.MapId);
        var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        List<PlayerRuntimeState>? toDisconnect = null;

        foreach (var state in zone.Players)
        {
            TickPlayer(zone, state, legacyTicksElapsed, groupA, groupB, nowUnixSeconds);

            if (state.PaidZoneEvictionPending)
                (toDisconnect ??= []).Add(state);
        }

        if (toDisconnect is null)
            return;

        foreach (var state in toDisconnect)
        {
            state.PaidZoneEvictionPending = false;
            state.Session.Abort(DisconnectReason.TimedZoneExpired);
        }
    }

    private static void TickPlayer(Zone zone, PlayerRuntimeState state, int legacyTicksElapsed, bool groupA,
        bool groupB, long nowUnixSeconds)
    {
        if (state.IsMovingZone)
            return;

        state.TimedBuffCountdownAccrualTicks += legacyTicksElapsed;
        var minutesElapsed = state.TimedBuffCountdownAccrualTicks / SimulationClock.PlayTimeAccrualLegacyTicks;
        if (minutesElapsed <= 0)
            return;

        state.TimedBuffCountdownAccrualTicks -= minutesElapsed * SimulationClock.PlayTimeAccrualLegacyTicks;

        if (groupA)
            TickGroupA(state, minutesElapsed);

        if (groupB)
            TickGroupB(zone, state, minutesElapsed);

        TickPaidZones(zone, state, minutesElapsed, nowUnixSeconds);
    }

    private static void TickGroupA(PlayerRuntimeState state, int minutesElapsed)
    {
        state.DropItemTime = TickTimer(state, 27, state.DropItemTime, minutesElapsed);
        state.FightingGodForDestroy = TickTimer(state, 20, state.FightingGodForDestroy, minutesElapsed);
        state.DoubleExpTime1 = TickTimer(state, 17, state.DoubleExpTime1, minutesElapsed);
        state.DoubleExpTime2 = TickTimer(state, 43, state.DoubleExpTime2, minutesElapsed);
    }

    private static void TickGroupB(Zone zone, PlayerRuntimeState state, int minutesElapsed)
    {
        if (IsMountActiveBelowMaxExp(state))
            state.AnimalDoubleExp = TickTimer(state, 75, state.AnimalDoubleExp, minutesElapsed);

        var statBoostExpiredToZero = false;

        state.DmgBoost = TickTimer(state, 46, state.DmgBoost, minutesElapsed, ref statBoostExpiredToZero);
        state.HPBoost = TickTimer(state, 47, state.HPBoost, minutesElapsed, ref statBoostExpiredToZero);
        state.CriBoost = TickTimer(state, 48, state.CriBoost, minutesElapsed, ref statBoostExpiredToZero);
        state.WarriorPill = TickTimer(state, 91, state.WarriorPill, minutesElapsed, ref statBoostExpiredToZero);
        state.WarriorScroll = TickTimer(state, 87, state.WarriorScroll, minutesElapsed);
        state.SilverTime = TickTimer(state, 90, state.SilverTime, minutesElapsed, ref statBoostExpiredToZero);
        state.GoldTime = TickTimer(state, 101, state.GoldTime, minutesElapsed, ref statBoostExpiredToZero);
        state.DoubleKillNumTime = TickTimer(state, 4, state.DoubleKillNumTime, minutesElapsed);
        state.DoubleKillExpTime = TickTimer(state, 5, state.DoubleKillExpTime, minutesElapsed);

        if (statBoostExpiredToZero)
        {
            var changedSlots = state.BuffChangeScratch;
            Array.Clear(changedSlots);
            zone.RecomputeStatsAndBroadcastBuffs(state, changedSlots);
        }
    }

    private static void TickPaidZones(Zone zone, PlayerRuntimeState state, int minutesElapsed, long nowUnixSeconds)
    {
        if (state.UserSort >= 1)
            return;

        switch (zone.MapId)
        {
            case 101 when state.Level2 > 0:
                if (TickPaidZoneTimer(state, 18, state.Zone101Time, minutesElapsed, out var zone101))
                    state.PaidZoneEvictionPending = true;
                else
                    state.Zone101Time = zone101;
                break;

            case 125:
                if (TickPaidZoneTimer(state, 21, state.TaiyanKeyTimer, minutesElapsed, out var zone125))
                    state.PaidZoneEvictionPending = true;
                else
                    state.TaiyanKeyTimer = zone125;
                break;

            case 126 when IsPremiumActive(state, nowUnixSeconds):
                break;

            case 126:
                if (TickPaidZoneTimer(state, 22, state.Zone126Time, minutesElapsed, out var zone126))
                    state.PaidZoneEvictionPending = true;
                else
                    state.Zone126Time = zone126;
                break;

            case 52:
                if (TickPaidZoneTimer(state, 65, state.Zone050Time2, minutesElapsed, out var zone52))
                    state.PaidZoneEvictionPending = true;
                else
                    state.Zone050Time2 = zone52;
                break;
        }
    }

    private static bool TickPaidZoneTimer(PlayerRuntimeState state, int subCode, int current, int minutesElapsed,
        out int newValue)
    {
        if (current > 0)
        {
            newValue = Math.Max(0, current - minutesElapsed);
            Broadcast(state, subCode, newValue);
            return false;
        }

        newValue = 0;
        return true;
    }

    private static int TickTimer(PlayerRuntimeState state, int subCode, int current, int minutesElapsed)
    {
        if (current <= 0)
            return current;

        var next = Math.Max(0, current - minutesElapsed);
        Broadcast(state, subCode, next);
        return next;
    }

    private static int TickTimer(PlayerRuntimeState state, int subCode, int current, int minutesElapsed,
        ref bool expiredToZero)
    {
        var next = TickTimer(state, subCode, current, minutesElapsed);
        if (current > 0 && next == 0)
            expiredToZero = true;

        return next;
    }

    private static bool IsMountActiveBelowMaxExp(PlayerRuntimeState state)
    {
        if (state.AnimalIndex is < 10 or > 19)
            return false;

        var slot = state.AnimalIndex - 10;
        return state.MountAccumulatedExp[slot] < MaxMountExp;
    }

    private static bool IsPremiumActive(PlayerRuntimeState state, long nowUnixSeconds)
    {
        return state.PremiumExpireUtc >= nowUnixSeconds;
    }

    private static void Broadcast(PlayerRuntimeState state, int sort, int value)
    {
        state.Session.Send(new AvatarStatUpdateResponse { Sort = sort, Value = value, Value2 = 0 });
    }
}
