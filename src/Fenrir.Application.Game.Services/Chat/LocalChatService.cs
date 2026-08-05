using System.Globalization;
using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Gm;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class LocalChatService(
    IWorldNoticeService worldNotice,
    IEventLogRepository eventLog,
    ISessionRateLimiter rateLimiter,
    IGameDataReloadService gameDataReload,
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
            SendSystemChat(sender,
                $"GM permission denied: account grade {zoneSession.AccountGrade}; required {(short)command.RequiredTier}.");
            await AuditAsync(command.Kind, zoneSession, GmCommandCatalog.OutcomeDenied,
                $"AccountGrade={zoneSession.AccountGrade};RequiredTier={(short)command.RequiredTier}", cancellationToken);
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

            case LocalChatGmCommandKind.ReloadAll:
                await HandleReloadAsync(GameDataReloadScope.All, zoneSession, sender, command.Kind,
                    cancellationToken);
                return;

            case LocalChatGmCommandKind.ReloadMonsters:
                await HandleReloadAsync(GameDataReloadScope.Monsters, zoneSession, sender, command.Kind,
                    cancellationToken);
                return;

            case LocalChatGmCommandKind.ReloadItems:
                await HandleReloadAsync(GameDataReloadScope.Items, zoneSession, sender, command.Kind,
                    cancellationToken);
                return;

            case LocalChatGmCommandKind.ReloadQuests:
                await HandleReloadAsync(GameDataReloadScope.Quests, zoneSession, sender, command.Kind,
                    cancellationToken);
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
            case "off":
                SendSystemChat(sender,
                    "YangGok PvP drop event is unavailable: no durable world authority is configured.");
                await AuditAsync(LocalChatGmCommandKind.YgDrop, zoneSession, GmCommandCatalog.OutcomeRejected,
                    $"Argument={argument};Reason=ProcessLocalState", cancellationToken);
                return;

            case "status":
                SendSystemChat(sender,
                    "YangGok PvP drop event status is unavailable: it is not globally authoritative.");
                await AuditAsync(LocalChatGmCommandKind.YgDrop, zoneSession, GmCommandCatalog.OutcomeRejected,
                    "Argument=status;Reason=ProcessLocalState", cancellationToken);
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
            case "off":
                SendSystemChat(sender,
                    "Labyrinth control is unavailable: no durable world authority is configured.");
                await AuditAsync(LocalChatGmCommandKind.Lab, zoneSession, GmCommandCatalog.OutcomeRejected,
                    $"Argument={argument};Reason=ProcessLocalState", cancellationToken);
                return;

            case "status":
                SendSystemChat(sender,
                    "Labyrinth control status is unavailable: it is not globally authoritative.");
                await AuditAsync(LocalChatGmCommandKind.Lab, zoneSession, GmCommandCatalog.OutcomeRejected,
                    "Argument=status;Reason=ProcessLocalState", cancellationToken);
                return;

            default:
                SendSystemChat(sender, "Usage: ?lab on|off|status");
                await AuditAsync(LocalChatGmCommandKind.Lab, zoneSession, GmCommandCatalog.OutcomeRejected,
                    $"Argument={argument}", cancellationToken);
                return;
        }
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

        var result = await zone.PostTribeProgressCommandAndWaitForResultAsync(
            new TribeProgressZoneCommand(sender.CharacterId, GmSummonMonsterTemplateId: monsterId), cancellationToken);
        if (result.Kind != ZoneCommandResultKind.Applied)
        {
            logger.LogWarning(
                "Zone {MapId} rejected GM 'boss' spawn for character {CharacterId} (monster {MonsterId}, result {Result}, cause {Cause})",
                zone.MapId, sender.CharacterId, monsterId, result.Kind, result.Cause);
            SendSystemChat(sender, "Boss summon was not applied.");
            await AuditAsync(LocalChatGmCommandKind.Boss, zoneSession, GmCommandCatalog.OutcomeRejected,
                $"MonsterId={monsterId};Result={result.Kind};Cause={result.Cause}", cancellationToken);
            return;
        }

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

    private async ValueTask HandleReloadAsync(GameDataReloadScope scope, IZoneSession zoneSession,
        PlayerRuntimeState sender, LocalChatGmCommandKind commandKind, CancellationToken cancellationToken)
    {
        SendSystemChat(sender, $"Reloading game-data snapshot for {scope.ToString().ToLowerInvariant()}...");
        var outcome = await gameDataReload.ReloadAsync(scope, cancellationToken).ConfigureAwait(false);
        if (!outcome.Succeeded)
        {
            SendSystemChat(sender, "Reload failed. The previous game-data snapshot remains active.");
            await AuditAsync(commandKind, zoneSession, GmCommandCatalog.OutcomeRejected,
                $"Scope={scope};Reason={outcome.Failure}", cancellationToken);
            return;
        }

        SendSystemChat(sender,
            $"Reload complete: {outcome.ItemCount} items, {outcome.MonsterCount} monsters, {outcome.QuestCount} quests ({outcome.Elapsed.TotalMilliseconds:F0} ms).");
        await AuditAsync(commandKind, zoneSession, GmCommandCatalog.OutcomeExecuted,
            $"Scope={scope};Items={outcome.ItemCount};Monsters={outcome.MonsterCount};Quests={outcome.QuestCount};ElapsedMs={(long)outcome.Elapsed.TotalMilliseconds}",
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
