using System.Globalization;
using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Gm;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class LocalChatService(
    YangGokPvpDropEventState yangGokDropEvent,
    LabyrinthOperatorGate labyrinthGate,
    IWorldNoticeService worldNotice,
    IEventLogRepository eventLog,
    ISessionRateLimiter rateLimiter,
    ILogger<LocalChatService> logger) : ILocalChatService
{
    private const string SystemSenderName = "SYSTEM";

    private static readonly ItemLinkInfo EmptyLink = new() { Index = 0, Activity = 0, Value = 0, Socket = new int[3] };

    public async ValueTask<bool> TryPostChatAsync(Zone zone, IZoneSession zoneSession, PlayerRuntimeState sender,
        string content, ItemLinkInfo link, CancellationToken cancellationToken)
    {
        if (sender.IsMuted)
            return false;

        if (zoneSession.IsGm && LocalChatGmCommandParser.TryParse(content, out var command))
        {
            await HandleGmCommandAsync(zone, zoneSession, sender, command, cancellationToken);
            return true;
        }

        zone.PostChatCommand(new ChatZoneCommand
        {
            SenderCharacterId = sender.CharacterId,
            Kind = ChatBroadcastKind.Local,
            Content = content,
            Link = link
        });

        return true;
    }

    private async ValueTask HandleGmCommandAsync(Zone zone, IZoneSession zoneSession, PlayerRuntimeState sender,
        LocalChatGmCommand command, CancellationToken cancellationToken)
    {
        if (!rateLimiter.TryConsumeGmCommand(zoneSession.SessionId))
        {
            logger.LogWarning(
                "Character {CharacterId} ({Name}) exceeded the GM chat-command rate limit -- disconnecting",
                sender.CharacterId, sender.Name);
            await AuditAsync(command.Kind, zoneSession, GmCommandCatalog.OutcomeRejected,
                $"Argument={command.Argument};Reason=RateLimited", cancellationToken);
            zoneSession.Abort(DisconnectReason.RateLimited);
            return;
        }

        if (!zoneSession.MeetsGmTier(command.RequiredTier))
        {
            SendSystemChat(sender, "You do not have permission to use this command.");
            await AuditAsync(command.Kind, zoneSession, GmCommandCatalog.OutcomeDenied,
                $"RequiredTier={(short)command.RequiredTier}", cancellationToken);
            return;
        }

        switch (command.Kind)
        {
            case LocalChatGmCommandKind.Where:
                SendSystemChat(sender,
                    $"zone {sender.MapId} ({(int)sender.PosX}, {(int)sender.PosY}, {(int)sender.PosZ})");
                await AuditAsync(command.Kind, zoneSession, GmCommandCatalog.OutcomeExecuted, null,
                    cancellationToken);
                return;

            case LocalChatGmCommandKind.YgDrop:
                await HandleYgDropAsync(zoneSession, sender, command.Argument, cancellationToken);
                return;

            case LocalChatGmCommandKind.Boss:
                await HandleBossAsync(zone, zoneSession, sender, command.Argument, cancellationToken);
                return;

            case LocalChatGmCommandKind.Kill200:
                await HandleKill200Async(zoneSession, sender, cancellationToken);
                return;

            case LocalChatGmCommandKind.Lab:
                await HandleLabAsync(zoneSession, sender, command.Argument, cancellationToken);
                return;

            case LocalChatGmCommandKind.ClearInventory:
                logger.LogWarning(
                    "Character {CharacterId} ({Name}) invoked GM '?clear' -- inventory-wipe needs a durable stored-proc path and a persistence-authority decision; no effect (see workstream report)",
                    sender.CharacterId, sender.Name);
                await AuditAsync(command.Kind, zoneSession, GmCommandCatalog.OutcomeRejected,
                    "NotImplemented", cancellationToken);
                return;

            default:
                logger.LogError(
                    "Character {CharacterId}: unhandled LocalChat GM command kind {Kind} -- consumed, no effect",
                    sender.CharacterId, command.Kind);
                return;
        }
    }

    private async ValueTask HandleYgDropAsync(IZoneSession zoneSession, PlayerRuntimeState sender, string? argument,
        CancellationToken cancellationToken)
    {
        switch (argument)
        {
            case "on":
                yangGokDropEvent.Enable();
                SendSystemChat(sender,
                    $"YangGok PvP drop event: ON {YangGokPvpDropEventState.EnabledDropRatePercent}%");
                await AuditAsync(LocalChatGmCommandKind.YgDrop, zoneSession, GmCommandCatalog.OutcomeExecuted,
                    "Argument=on", cancellationToken);
                return;

            case "off":
                yangGokDropEvent.Disable();
                SendSystemChat(sender, "YangGok PvP drop event: OFF");
                await AuditAsync(LocalChatGmCommandKind.YgDrop, zoneSession, GmCommandCatalog.OutcomeExecuted,
                    "Argument=off", cancellationToken);
                return;

            case "status":
                SendSystemChat(sender, yangGokDropEvent.Enabled
                    ? $"YangGok PvP drop event: ON {yangGokDropEvent.DropRatePercent}%"
                    : "YangGok PvP drop event: OFF");
                await AuditAsync(LocalChatGmCommandKind.YgDrop, zoneSession, GmCommandCatalog.OutcomeExecuted,
                    "Argument=status", cancellationToken);
                return;

            default:
                SendSystemChat(sender, "Usage: ?ygdrop on|off|status");
                await AuditAsync(LocalChatGmCommandKind.YgDrop, zoneSession, GmCommandCatalog.OutcomeRejected,
                    $"Argument={argument}", cancellationToken);
                return;
        }
    }

    private async ValueTask HandleLabAsync(IZoneSession zoneSession, PlayerRuntimeState sender, string? argument,
        CancellationToken cancellationToken)
    {
        switch (argument)
        {
            case "on":
                labyrinthGate.Enable();
                SendSystemChat(sender, "Labyrinth R0-R12: ON");
                await AuditAsync(LocalChatGmCommandKind.Lab, zoneSession, GmCommandCatalog.OutcomeExecuted,
                    "Argument=on", cancellationToken);
                return;

            case "off":
                labyrinthGate.Disable();
                SendSystemChat(sender, "Labyrinth R0-R12: OFF");
                await AuditAsync(LocalChatGmCommandKind.Lab, zoneSession, GmCommandCatalog.OutcomeExecuted,
                    "Argument=off", cancellationToken);
                return;

            case "status":
                SendSystemChat(sender, FormatLabStatus(labyrinthGate.Enabled));
                await AuditAsync(LocalChatGmCommandKind.Lab, zoneSession, GmCommandCatalog.OutcomeExecuted,
                    "Argument=status", cancellationToken);
                return;

            default:
                SendSystemChat(sender, "Usage: ?lab on|off|status");
                await AuditAsync(LocalChatGmCommandKind.Lab, zoneSession, GmCommandCatalog.OutcomeRejected,
                    $"Argument={argument}", cancellationToken);
                return;
        }
    }

    private static string FormatLabStatus(bool enabled)
    {
        var cellValue = enabled ? 1 : 0;
        return $"Labyrinth R0-R12: ND={cellValue} RS={cellValue} GT={cellValue} NG={cellValue}";
    }

    private async ValueTask HandleBossAsync(Zone zone, IZoneSession zoneSession, PlayerRuntimeState sender,
        string? argument, CancellationToken cancellationToken)
    {
        if (!int.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out var monsterId) ||
            monsterId < 1)
        {
            SendSystemChat(sender, "Usage: boss <monster id>");
            await AuditAsync(LocalChatGmCommandKind.Boss, zoneSession, GmCommandCatalog.OutcomeRejected,
                $"Argument={argument}", cancellationToken);
            return;
        }

        if (!zone.PostTribeProgressCommand(
                new TribeProgressZoneCommand(sender.CharacterId, GmSummonMonsterTemplateId: monsterId)))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped GM 'boss' spawn for character {CharacterId} (monster {MonsterId})",
                zone.MapId, sender.CharacterId, monsterId);

        worldNotice.Broadcast($"A boss (id {monsterId}) has been summoned.");

        await AuditAsync(LocalChatGmCommandKind.Boss, zoneSession, GmCommandCatalog.OutcomeExecuted,
            $"MonsterId={monsterId}", cancellationToken);
    }

    private async ValueTask HandleKill200Async(IZoneSession zoneSession, PlayerRuntimeState sender,
        CancellationToken cancellationToken)
    {
        SendChatAs(sender, sender.Name, "kill200");
        logger.LogWarning(
            "Character {CharacterId} ({Name}) invoked GM 'kill200' -- the zone-200 battle-result reset is an unmodeled subsystem in Fenrir; only the self-echo was applied",
            sender.CharacterId, sender.Name);

        await AuditAsync(LocalChatGmCommandKind.Kill200, zoneSession, GmCommandCatalog.OutcomeExecuted, null,
            cancellationToken);
    }

    private static void SendSystemChat(PlayerRuntimeState recipient, string content)
    {
        SendChatAs(recipient, SystemSenderName, content);
    }

    private static void SendChatAs(PlayerRuntimeState recipient, string avatarName, string content)
    {
        recipient.Session.Send(new LocalChatResponse
        {
            AvatarName = avatarName,
            Content = content,
            Link = EmptyLink
        });
    }

    private ValueTask AuditAsync(LocalChatGmCommandKind kind, IZoneSession zoneSession, byte outcome,
        string? detail, CancellationToken cancellationToken)
    {
        var payload = detail is null ? $"Command={kind}" : $"Command={kind};{detail}";

        return eventLog.LogAsync(LocalChatGmCommandAudit.EventCodeFor(kind), EventLogCategory.GmAction,
            zoneSession.AccountId, zoneSession.CharacterId, null, null, null, null, null, null, null, outcome,
            payload, cancellationToken);
    }
}
