using System.Globalization;
using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.Gm;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class LocalChatService(
    YangGokPvpDropEventState yangGokDropEvent,
    LabyrinthOperatorGate labyrinthGate,
    IWorldNoticeService worldNotice,
    ILogger<LocalChatService> logger) : ILocalChatService
{
    private const string SystemSenderName = "SYSTEM";

    private static readonly ItemLinkInfo EmptyLink = new() { Index = 0, Activity = 0, Value = 0, Socket = new int[3] };

    public bool TryPostChat(Zone zone, IZoneSession zoneSession, PlayerRuntimeState sender, string content,
        ItemLinkInfo link)
    {
        if (sender.IsMuted)
            return false;

        if (zoneSession.IsGm && LocalChatGmCommandParser.TryParse(content, out var command))
        {
            HandleGmCommand(zone, zoneSession, sender, command);
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

    private void HandleGmCommand(Zone zone, IZoneSession zoneSession, PlayerRuntimeState sender,
        LocalChatGmCommand command)
    {
        if (!zoneSession.MeetsGmTier(command.RequiredTier))
        {
            SendSystemChat(sender, "You do not have permission to use this command.");
            return;
        }

        switch (command.Kind)
        {
            case LocalChatGmCommandKind.Where:
                SendSystemChat(sender,
                    $"zone {sender.MapId} ({(int)sender.PosX}, {(int)sender.PosY}, {(int)sender.PosZ})");
                return;

            case LocalChatGmCommandKind.YgDrop:
                HandleYgDrop(sender, command.Argument);
                return;

            case LocalChatGmCommandKind.Boss:
                HandleBoss(zone, sender, command.Argument);
                return;

            case LocalChatGmCommandKind.Kill200:
                HandleKill200(sender);
                return;

            case LocalChatGmCommandKind.Lab:
                HandleLab(sender, command.Argument);
                return;

            case LocalChatGmCommandKind.ClearInventory:
                logger.LogWarning(
                    "Character {CharacterId} ({Name}) invoked GM '?clear' -- inventory-wipe needs a durable stored-proc path and a persistence-authority decision; no effect (see workstream report)",
                    sender.CharacterId, sender.Name);
                return;

            default:
                logger.LogError(
                    "Character {CharacterId}: unhandled LocalChat GM command kind {Kind} -- consumed, no effect",
                    sender.CharacterId, command.Kind);
                return;
        }
    }

    private void HandleYgDrop(PlayerRuntimeState sender, string? argument)
    {
        switch (argument)
        {
            case "on":
                yangGokDropEvent.Enable();
                SendSystemChat(sender,
                    $"YangGok PvP drop event: ON {YangGokPvpDropEventState.EnabledDropRatePercent}%");
                return;

            case "off":
                yangGokDropEvent.Disable();
                SendSystemChat(sender, "YangGok PvP drop event: OFF");
                return;

            case "status":
                SendSystemChat(sender, yangGokDropEvent.Enabled
                    ? $"YangGok PvP drop event: ON {yangGokDropEvent.DropRatePercent}%"
                    : "YangGok PvP drop event: OFF");
                return;

            default:
                SendSystemChat(sender, "Usage: ygdrop on|off|status");
                return;
        }
    }

    private void HandleLab(PlayerRuntimeState sender, string? argument)
    {
        switch (argument)
        {
            case "on":
                labyrinthGate.Enable();
                SendSystemChat(sender, "Labyrinth R0-R12: ON");
                return;

            case "off":
                labyrinthGate.Disable();
                SendSystemChat(sender, "Labyrinth R0-R12: OFF");
                return;

            case "status":
                SendSystemChat(sender, FormatLabStatus(labyrinthGate.Enabled));
                return;

            default:
                SendSystemChat(sender, "Usage: lab on|off|status");
                return;
        }
    }

    private static string FormatLabStatus(bool enabled)
    {
        var cellValue = enabled ? 1 : 0;
        return $"Labyrinth R0-R12: ND={cellValue} RS={cellValue} GT={cellValue} NG={cellValue}";
    }

    private void HandleBoss(Zone zone, PlayerRuntimeState sender, string? argument)
    {
        if (!int.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out var monsterId) ||
            monsterId < 1)
        {
            SendSystemChat(sender, "Usage: boss <monster id>");
            return;
        }

        if (!zone.PostTribeProgressCommand(
                new TribeProgressZoneCommand(sender.CharacterId, GmSummonMonsterTemplateId: monsterId)))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped GM 'boss' spawn for character {CharacterId} (monster {MonsterId})",
                zone.MapId, sender.CharacterId, monsterId);

        worldNotice.Broadcast($"A boss (id {monsterId}) has been summoned.");
    }

    private void HandleKill200(PlayerRuntimeState sender)
    {
        SendChatAs(sender, sender.Name, "kill200");
        logger.LogWarning(
            "Character {CharacterId} ({Name}) invoked GM 'kill200' -- the zone-200 battle-result reset is an unmodeled subsystem in Fenrir; only the self-echo was applied",
            sender.CharacterId, sender.Name);
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
}
