using Fenrir.Application.Game.Progression;
using Fenrir.Application.Game.Tribes;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Progression;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Progression;

/// <summary>
///     CZ_HEROREWARD_SEND (opcode 119). Unranked -&gt; no reply at all (the legacy's own search loop just
///     falls through without ever calling USEND). Already claimed -&gt; Result=3, every other field 0.
///     Otherwise credits <see cref="HeroRewardResolver.PointsByRank" /> as CP and marks the row claimed;
///     the real reward is CP -- ZC_HEROREWARD_RECV's item-drop fields are dead code in this build
///     (S04_MyWork02.cpp:14225-14243 is commented out) and are always sent as 0.
/// </summary>
public sealed class HeroRewardClaimHandler(
    IHeroRankingRepository heroRankings,
    ILogger<HeroRewardClaimHandler> logger)
    : IAsyncPacketHandler<HeroRewardClaimRequest>
{
    public async ValueTask HandleAsync(HeroRewardClaimRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var rows = await heroRankings.GetByPeriodAsync(1, cancellationToken);
            var resolved = HeroRewardResolver.Resolve(rows, state.Tribe, characterId);

            if (resolved is not { Outcome: HeroRewardResolver.Outcome.Claim, Row: { } row })
            {
                if (resolved.Outcome == HeroRewardResolver.Outcome.AlreadyClaimed)
                    session.Send(EmptyResponse(3));
                return;
            }

            var points = HeroRewardResolver.PointsByRank[resolved.Rank];

            await heroRankings.MarkRewardClaimedAsync(characterId, 1, row.Points, row.TribeId, row.Level,
                cancellationToken);

            session.Send(EmptyResponse(1000));

            if (!await zone.PostTribeProgressCommandAndWaitAsync(
                    new TribeProgressZoneCommand(characterId, state.ContributionPoints + points), cancellationToken))
                logger.LogError(
                    "Zone {MapId} tribe-progress inbox full: dropped hero-reward CP mirror for character {CharacterId} -- unlike sibling handlers this is NOT self-healing, the DB reward-claim row is already committed",
                    zone.MapId, characterId);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private static HeroRewardClaimResponse EmptyResponse(int result)
    {
        return new HeroRewardClaimResponse
        {
            Result = result, Page = 0, Index1 = 0, Index2 = 0, Xy1 = 0, Xy2 = 0, ItemIndex = new int[8]
        };
    }
}
