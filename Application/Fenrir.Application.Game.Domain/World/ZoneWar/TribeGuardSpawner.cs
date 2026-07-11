using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class TribeGuardOptions
{
    public bool FourthTribeGuardPostsEnabled { get; init; } = true;
}

internal sealed class GuardSlotRuntimeState
{
    public bool Armed;
    public int RespawnTicksRemaining;
}

internal sealed class GuardZoneState
{
    public readonly Dictionary<int, GuardSlotRuntimeState> Slots = new();
    public bool BootPassDone;
    public int ForceOrdinaryPending;
    public int ForceZone038WinnerPending;
    public int TicksSinceFullScan;
}

public sealed class TribeGuardSpawner(
    WorldDataCache worldData,
    GuardPostCatalog catalog,
    WorldStateService? worldState = null,
    TribeGuardOptions? options = null) : ISimulationSystem
{
    public const short Zone038MapId = 38;

    private const int FullEvaluationCadenceLegacyTicks = 20;

    private const float GuardLeashRadius = 15f;

    private const int OrdinaryPoolServerIndexBase = 1_000_000;
    private const int Zone038WinnerPoolServerIndexBase = 1_001_000;

    public static readonly IReadOnlySet<short> OrdinaryEligibleMapIds =
        new HashSet<short>([38, 2, 3, 4, 7, 8, 9, 12, 13, 14, 141, 142, 143]);

    private readonly TribeGuardOptions _options = options ?? new TribeGuardOptions();
    private readonly ConcurrentDictionary<short, GuardZoneState> _stateByZone = new();

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var state = _stateByZone.GetOrAdd(zone.MapId, static _ => new GuardZoneState());

        foreach (var slotState in state.Slots.Values)
            if (slotState.RespawnTicksRemaining > 0)
                slotState.RespawnTicksRemaining = Math.Max(0, slotState.RespawnTicksRemaining - legacyTicksElapsed);

        if (!state.BootPassDone)
        {
            state.BootPassDone = true;
            EvaluateOrdinaryPool(zone, state, true);
            EvaluateZone038WinnerPool(zone, state, true);
        }

        state.TicksSinceFullScan += legacyTicksElapsed;
        var forceOrdinary = Interlocked.Exchange(ref state.ForceOrdinaryPending, 0) != 0;
        var forceZone038Winner = Interlocked.Exchange(ref state.ForceZone038WinnerPending, 0) != 0;

        var due = state.TicksSinceFullScan >= FullEvaluationCadenceLegacyTicks;
        if (!due && !forceOrdinary && !forceZone038Winner)
            return;

        state.TicksSinceFullScan = 0;
        EvaluateOrdinaryPool(zone, state, forceOrdinary);
        EvaluateZone038WinnerPool(zone, state, forceZone038Winner);
    }

    public void ForceOrdinaryResummon(Zone zone)
    {
        var state = _stateByZone.GetOrAdd(zone.MapId, static _ => new GuardZoneState());
        Interlocked.Exchange(ref state.ForceOrdinaryPending, 1);
    }

    public void ForceZone038WinnerResummon(Zone zone)
    {
        var state = _stateByZone.GetOrAdd(zone.MapId, static _ => new GuardZoneState());
        Interlocked.Exchange(ref state.ForceZone038WinnerPending, 1);
    }

    private void EvaluateOrdinaryPool(Zone zone, GuardZoneState state, bool forceFirstPass)
    {
        if (!OrdinaryEligibleMapIds.Contains(zone.MapId))
            return;

        if (forceFirstPass)
            TribeGuardForceResetSweep.Wipe(zone, OrdinaryPoolServerIndexBase);

        foreach (var post in catalog.OrdinaryPosts)
        {
            if (post.MapId != zone.MapId)
                continue;

            if (post.TribeId == 3 && !_options.FourthTribeGuardPostsEnabled)
                continue;

            if (post.RequiresTribeSymbolOwnedBy is { } ownerTribeId &&
                !(worldState is not null && worldState.GetTribe(ownerTribeId).HasSymbol))
                continue;

            EvaluatePost(zone, state, post, OrdinaryPoolServerIndexBase, forceFirstPass);
        }
    }

    private void EvaluateZone038WinnerPool(Zone zone, GuardZoneState state, bool forceFirstPass)
    {
        if (zone.MapId != Zone038MapId)
            return;

        if (worldState is null || worldState.World.Zone038WinTribe is not { } winningTribe)
            return;

        if (forceFirstPass)
            TribeGuardForceResetSweep.Wipe(zone, Zone038WinnerPoolServerIndexBase);

        foreach (var post in catalog.Zone038WinnerPosts)
        {
            if (post.MapId != zone.MapId || post.TribeId != winningTribe)
                continue;

            EvaluatePost(zone, state, post, Zone038WinnerPoolServerIndexBase, forceFirstPass);
        }
    }

    private void EvaluatePost(Zone zone, GuardZoneState state, GuardPostDefinition post, int poolServerIndexBase,
        bool forceFirstPass)
    {
        if (!TryFindTemplate(post.MonsterMainType, post.MonsterSpecialType, out var template))
            return;

        foreach (var slot in post.Slots)
        {
            var serverIndex = poolServerIndexBase + slot.ReservedSlotIndex;

            if (zone.TryGetMonster(serverIndex, out _))
                continue;

            if (!forceFirstPass)
            {
                var slotState = GetOrAddSlot(state, serverIndex);
                if (!slotState.Armed)
                {
                    slotState.Armed = true;
                    slotState.RespawnTicksRemaining =
                        SimulationClock.ToWholeLegacyTicks(TimeSpan.FromSeconds(template.SummonTime1));
                }

                if (slotState.RespawnTicksRemaining > 0)
                    continue;
            }

            SpawnGuard(zone, serverIndex, slot, template);

            if (state.Slots.TryGetValue(serverIndex, out var spawned))
                spawned.Armed = false;
        }
    }

    private static GuardSlotRuntimeState GetOrAddSlot(GuardZoneState state, int serverIndex)
    {
        if (!state.Slots.TryGetValue(serverIndex, out var slotState))
        {
            slotState = new GuardSlotRuntimeState();
            state.Slots[serverIndex] = slotState;
        }

        return slotState;
    }

    private bool TryFindTemplate(byte mainType, byte specialType, [NotNullWhen(true)] out MonsterRowDto? template)
    {
        foreach (var definition in worldData.MonstersById.Values)
            if (definition.Monster.Type == mainType && definition.Monster.SpecialType == specialType)
            {
                template = definition.Monster;
                return true;
            }

        template = null;
        return false;
    }

    private static void SpawnGuard(Zone zone, int serverIndex, GuardSlotCoordinate slot, MonsterRowDto template)
    {
        var entity = MonsterEntity.Create(serverIndex, zone.NextMonsterUniqueNumber(), template, serverIndex,
            slot.X, slot.Y, slot.Z, GuardLeashRadius);
        zone.SpawnMonster(entity);
    }
}
