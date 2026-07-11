using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Tribes;

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
        {
            logger.LogDebug(
                "Character {CharacterId} fourth-tribe conversion rejected: {Outcome} (tribe {OldTribe})",
                characterId, outcome, oldTribe);
            return outcome;
        }

        if (!await quota.TryConsumeAsync(ct).ConfigureAwait(false))
        {
            logger.LogInformation(
                "Character {CharacterId} fourth-tribe conversion rejected: shared daily quota exhausted",
                characterId);
            return TribeMigrationOutcome.QuotaExhausted;
        }

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
