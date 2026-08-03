using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Gm;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

public sealed class GmBlockAvatarService(
    ZoneRegistry zones,
    IBanRepository bans,
    IEventLogRepository eventLog,
    ILogger<GmBlockAvatarService> logger) : IGmBlockAvatarService
{
    private const int ResultTargetNotFound = 1;

    private const int GmBlockSort = 519;

    private static readonly byte[] EmptyGenericActionData = new byte[130];

    private static readonly TimeSpan BlockDuration = TimeSpan.FromDays(365 * 30);

    public async ValueTask HandleAsync(GmBlockAvatarPayload packet, IZoneSession zoneSession,
        CancellationToken cancellationToken)
    {
        if (!zoneSession.IsGm)
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Basic-tier GM BLOCK command (sort {Sort}) without sufficient privilege -- disconnecting, no reply",
                zoneSession.CharacterId, GmBlockSort);
            await eventLog.LogAsync(GmActionEventCodes.Block, EventLogCategory.GmAction, zoneSession.AccountId,
                zoneSession.CharacterId, null, null, null, null, null, null, null, GmCommandCatalog.OutcomeDenied,
                $"Command=BLOCK;Sort={GmBlockSort};RequiredTier={(short)GmCommandTier.Basic}", cancellationToken);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var callerCharacterId = zoneSession.CharacterId!.Value;

        var found = zones.TryGetPlayerAndZoneByName(packet.AvatarName, out var target, out _);
        if (!found || target!.CharacterId == callerCharacterId)
        {
            zoneSession.Send(new GenericActionResponse
            {
                Result = ResultTargetNotFound, Sort = GmBlockSort, Data = EmptyGenericActionData, RuneValue = 0
            });
            return;
        }

        var targetAccountId = ((IZoneSession)target.Session).AccountId;

        await eventLog.LogAsync(GmActionEventCodes.Block, EventLogCategory.GmAction, zoneSession.AccountId,
            callerCharacterId, targetAccountId, target.CharacterId, null, null, null, null, null, 1,
            $"TargetName={target.Name}", cancellationToken);

        await bans.CreateAsync(targetAccountId, target.CharacterId, BanReason.GmManualBlock,
            DateTime.UtcNow.Add(BlockDuration), cancellationToken);

        logger.LogWarning(
            "GM character {GmCharacterId} blocked avatar {TargetCharacterId} ({TargetName})",
            callerCharacterId, target.CharacterId, target.Name);

        ((IZoneSession)target.Session).Abort(DisconnectReason.Banned);
    }
}
