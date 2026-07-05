using Fenrir.Application.Game.Handlers.Progression.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Progression;

/// <summary>
///     CZ_HEROREWARD_SEND (opcode 119). Unranked -&gt; no reply at all (the legacy's own search loop just
///     falls through without ever calling USEND). Already claimed -&gt; Result=3, every other field 0.
///     Otherwise credits <see cref="HeroRewardResolver.PointsByRank" /> as CP and marks the row claimed;
///     the real reward is CP -- ZC_HEROREWARD_RECV's item-drop fields are dead code in this build
///     (S04_MyWork02.cpp:14225-14243 is commented out) and are always sent as 0.
/// </summary>
public sealed class HeroRewardClaimHandler(IHeroRewardClaimService heroRewardClaimService)
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
            var result = await heroRewardClaimService.ClaimAsync(characterId, zone, state, cancellationToken);

            switch (result.Outcome)
            {
                case HeroRewardClaimOutcome.AlreadyClaimed:
                    session.Send(EmptyResponse(3));
                    break;
                case HeroRewardClaimOutcome.Claimed:
                    session.Send(EmptyResponse(1000));
                    break;
                case HeroRewardClaimOutcome.NotRanked:
                default:
                    break;
            }
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
