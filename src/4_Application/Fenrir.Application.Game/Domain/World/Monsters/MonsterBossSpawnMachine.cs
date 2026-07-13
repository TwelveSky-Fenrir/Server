using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.Monsters;

public interface IMonsterBossSpawnSink
{
    public bool TryResolveCandidate(int monsterId);

    public bool IsSlotFree(int serverIndex);

    public void SpawnBoss(int serverIndex, in MonsterBossSummonCandidate candidate);
}

public sealed class MonsterBossSpawnZoneState
{
    public required ImmutableArray<MonsterBossSummonCandidate> Candidates { get; init; }

    public required int SlotBase { get; init; }

    public required Random Random { get; init; }

    public required IMonsterBossSpawnSink Sink { get; init; }

    public MonsterBossSummonState State { get; set; } = MonsterBossSummonState.Reload;

    public long ElapsedLegacyTicks { get; set; }

    public int TicksSinceLastInvocation { get; set; }

    public long AnchorTick { get; set; }
}

public static class MonsterBossSpawnMachine
{
    public const int InvocationIntervalLegacyTicks = 20;

    public const long WaitDurationLegacyTicks = 21_600;

    public const int MaxSpawnsPerReload = 5;

    public const int BossSlotWindowSize = 20;

    public const int DefaultBossSlotBase = 3800;

    public static void Advance(MonsterBossSpawnZoneState state, int legacyTicksElapsed)
    {
        if (legacyTicksElapsed < 0)
            legacyTicksElapsed = 0;

        state.ElapsedLegacyTicks += legacyTicksElapsed;
        state.TicksSinceLastInvocation += legacyTicksElapsed;

        if (state.TicksSinceLastInvocation < InvocationIntervalLegacyTicks)
            return;

        state.TicksSinceLastInvocation = 0;
        ProcessOneInvocation(state);
    }

    private static void ProcessOneInvocation(MonsterBossSpawnZoneState state)
    {
        if (state.Candidates.Length < 1)
            return;

        switch (state.State)
        {
            case MonsterBossSummonState.Reload:
                ProcessReload(state);
                break;

            case MonsterBossSummonState.Check:
                state.State = MonsterBossSummonState.Wait;
                state.AnchorTick = state.ElapsedLegacyTicks;
                break;

            case MonsterBossSummonState.Wait:
                if (state.ElapsedLegacyTicks - state.AnchorTick < WaitDurationLegacyTicks)
                    return;
                state.State = MonsterBossSummonState.Reload;
                break;

            default:
                state.State = MonsterBossSummonState.Reload;
                break;
        }
    }

    private static void ProcessReload(MonsterBossSpawnZoneState state)
    {
        var index = state.Random.Next(state.Candidates.Length);
        var candidate = state.Candidates[index];

        if (!state.Sink.TryResolveCandidate(candidate.MonsterId))
            return;

        var spawned = 0;
        for (var i = 0; i < BossSlotWindowSize && spawned < MaxSpawnsPerReload; i++)
        {
            var serverIndex = state.SlotBase + i;
            if (!state.Sink.IsSlotFree(serverIndex))
                continue;

            state.Sink.SpawnBoss(serverIndex, candidate);
            spawned++;
        }

        state.State = MonsterBossSummonState.Check;
        state.AnchorTick = state.ElapsedLegacyTicks;
    }
}
