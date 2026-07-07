using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Tribes;

/// <summary>See <see cref="ITribeMigrationService" />.</summary>
/// <remarks>
///     Réf. "Fourth-tribe (Fujin) conversion and return" behavior contract (legacy-behavior-translator),
///     itself citing Server/ts25zone/S04_MyWork02.cpp:7565-7758. Orchestrates
///     <see cref="Tribes.TribeMigrationGate" /> (every character-specific precondition), the shared global
///     quota (checked LAST -- see <see cref="TribeMigrationOutcome.QuotaExhausted" />'s own remarks for the
///     deliberate hardening reorder), and the atomic tribe+quest-state commit
///     (<see cref="ICharacterRepository.ApplyTribeFourConversionAsync" />) plus its
///     <see cref="PlayerRuntimeState" /> mirror. The legacy's own cross-server "joined/returned" broadcast
///     (S04_MyWork02.cpp:7748, S04_MyWork02.cpp's return-branch equivalent) is a confirmed structural no-op on
///     the legacy central process (empty case arms, Server/ts25center/S04_MyWork02.cpp:474,:991-992) and is
///     deliberately not reproduced here -- the behavior contract leaves whether Fenrir should implement a real
///     announcement as an open product question, not a legacy behavior to carry forward.
/// </remarks>
public sealed class TribeMigrationService(
    ICharacterRepository characters,
    ITribeFourQuotaRepository quota,
    WorldStateService worldState,
    QuestCatalog questCatalog,
    IOptions<GameServerOptions> options,
    TimeProvider timeProvider,
    ILogger<TribeMigrationService> logger) : ITribeMigrationService
{
    public async ValueTask<TribeMigrationOutcome> ConvertAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        var oldTribe = state.Tribe;

        var tribePoints = new int[WorldStateService.TribeCount];
        foreach (var tribe in worldState.GetAllTribes())
            tribePoints[tribe.TribeId] = tribe.Points;

        var context = new TribeMigrationEligibilityContext(
            options.Value.TribeFourConversionEnabled,
            // "The current wall-clock time as read by the zone process's own local clock" (contract) --
            // TimeProvider.GetLocalNow() rather than a bare DateTime.Now call, so this is deterministically
            // testable (see TribeMigrationServiceTests' own fixed-Saturday fake).
            timeProvider.GetLocalNow().DateTime,
            oldTribe,
            state.PreviousTribe,
            state.Level,
            state.TribeRole,
            state.GuildId,
            state.TeacherCharacterId,
            state.StudentCharacterId,
            !state.Friends.IsEmpty,
            state.TribeFourReturnAllowance,
            tribePoints,
            worldState.GetAllyOf);

        var outcome = TribeMigrationGate.Evaluate(context);
        if (outcome != TribeMigrationOutcome.Success)
            return outcome;

        // Hardened ordering (deliberate deviation from the legacy's own quota-first bug): every
        // character-specific gate above already passed, so this attempt is the only kind allowed to spend the
        // shared quota -- see TribeMigrationOutcome.QuotaExhausted's own remarks.
        if (!await quota.TryConsumeAsync(ct).ConfigureAwait(false))
            return TribeMigrationOutcome.QuotaExhausted;

        var result = TribeMigrationConversion.Resolve(oldTribe, state.PreviousTribe, questCatalog);
        var isReturnBranch = oldTribe == TribeMigrationGate.TribeFour;

        await characters.ApplyTribeFourConversionAsync(characterId, result.NewTribe,
            result.NewQuestProgress.StepPermanent, result.NewQuestProgress.ActiveFlag, result.NewQuestProgress.QSort,
            result.NewQuestProgress.TargetPhase, result.NewQuestProgress.KillCounter, ct).ConfigureAwait(false);

        var command = new TribeProgressZoneCommand(characterId,
            Tribe: result.NewTribe,
            QuestProgress: result.NewQuestProgress,
            TribeFourReturnAllowance: isReturnBranch ? state.TribeFourReturnAllowance - 1 : null);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(command, ct).ConfigureAwait(false))
            logger.LogError(
                "Zone {MapId} tribe inbox full: dropped fourth-tribe conversion mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} fourth-tribe conversion: tribe {OldTribe} -> {NewTribe} ({Branch})",
            characterId, oldTribe, result.NewTribe, isReturnBranch ? "return" : "outbound");

        return TribeMigrationOutcome.Success;
    }
}
