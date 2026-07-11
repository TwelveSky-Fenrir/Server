using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Gm;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.Chat;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Chat;

// LocalChat (CZ_GENERAL_CHAT_SEND, opcode 38) embedded GM sub-commands -- workstream A14. Covers the six
// command words LocalChatGmCommandParser recognizes and the per-command tier gate:
//   where   (Basic)    -- self-location echo (regression).
//   ygdrop  (Elevated) -- YangGokPvpDropEventState toggle + status; fully wired here.
//   boss    (Elevated) -- monster spawn at sender position + shard-wide notice; fully wired here.
//   kill200 (Basic)    -- self-echo only (the zone-200 counter reset is an unmodeled gap).
//   lab / ?clear       -- deliberately DEFERRED (center relay / durable inventory-wipe stored proc); asserted
//                         as consumed-with-no-effect, never leaked as chat.
// The command actions live in Fenrir.Application.Game.Services.Chat.LocalChatService; the tier thresholds are
// Server/ts25zone/S04_MyWork02.cpp:7798 (any GM for the whole block), :7810/:7853/:7908 (Elevated for
// ygdrop/lab/boss), :7800/:7933/:7940 (Basic for where/kill200/?clear).
public class LocalChatServiceGmCommandTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const short MapId = 1;
    private const string HeroName = "Hero";
    private const int KnownMonsterId = 900;

    private static ItemLinkInfo Link()
    {
        return new ItemLinkInfo { Index = 0, Activity = 0, Value = 0, Socket = new int[3] };
    }

    private static LocalChatResponse SystemChat(string content)
    {
        return new LocalChatResponse { AvatarName = "SYSTEM", Content = content, Link = Link() };
    }

    private static (LocalChatService Service, YangGokPvpDropEventState DropEvent, FakeWorldNoticeService Notice)
        BuildService()
    {
        var dropEvent = new YangGokPvpDropEventState();
        var notice = new FakeWorldNoticeService();
        var service = new LocalChatService(dropEvent, notice, NullLogger<LocalChatService>.Instance);
        return (service, dropEvent, notice);
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State) SetUp(
        short accountGrade)
    {
        var monstersById = new Dictionary<int, MonsterDefinition>
        {
            [KnownMonsterId] = new(WorldDataTestRows.Monster(KnownMonsterId), null, [], [], [], null)
        }.ToFrozenDictionary();

        var zone = ZoneTestKit.CreateZone(MapId, worldData: ZoneTestKit.EmptyWorldData(monstersById: monstersById));
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId, null, accountGrade);
        session.MarkRegistering();
        session.MarkInWorld();
        session.CurrentZone = zone;

        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, MapId, HeroName)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        return (session, pipe, zone, state!);
    }

    [Fact]
    public async Task Where_BasicGm_EchoesOwnLocationToSenderOnly()
    {
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Basic);
        var (service, _, _) = BuildService();

        var handled = service.TryPostChat(zone, session, state, "where", Link());

        Assert.True(handled);
        await PacketAssert.AssertSentAsync(pipe, SystemChat("zone 1 (100, 0, 100)"));
    }

    [Fact]
    public async Task Ygdrop_On_ElevatedGm_EnablesFlagAndRate_AndConfirms()
    {
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Elevated);
        var (service, dropEvent, _) = BuildService();

        service.TryPostChat(zone, session, state, "ygdrop on", Link());

        Assert.True(dropEvent.Enabled);
        Assert.Equal(35, dropEvent.DropRatePercent);
        await PacketAssert.AssertSentAsync(pipe, SystemChat("YangGok PvP drop event: ON 35%"));
    }

    [Fact]
    public async Task Ygdrop_Off_ClearsFlag_ButLeavesRate()
    {
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Elevated);
        var (service, dropEvent, _) = BuildService();

        service.TryPostChat(zone, session, state, "ygdrop on", Link());
        ZoneTestKit.DrainOutbound(pipe);
        service.TryPostChat(zone, session, state, "ygdrop off", Link());

        Assert.False(dropEvent.Enabled);
        // Legacy's `off` branch touches only the flag; the rate stays at its last-installed value.
        Assert.Equal(35, dropEvent.DropRatePercent);
        await PacketAssert.AssertSentAsync(pipe, SystemChat("YangGok PvP drop event: OFF"));
    }

    [Fact]
    public async Task Ygdrop_Status_Enabled_ReportsOnWithRate()
    {
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Elevated);
        var (service, dropEvent, _) = BuildService();
        dropEvent.Enable();

        service.TryPostChat(zone, session, state, "ygdrop status", Link());

        await PacketAssert.AssertSentAsync(pipe, SystemChat("YangGok PvP drop event: ON 35%"));
    }

    [Fact]
    public async Task Ygdrop_Status_Disabled_ReportsOff()
    {
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Elevated);
        var (service, _, _) = BuildService();

        service.TryPostChat(zone, session, state, "ygdrop status", Link());

        await PacketAssert.AssertSentAsync(pipe, SystemChat("YangGok PvP drop event: OFF"));
    }

    [Fact]
    public async Task Ygdrop_UnknownSubform_SendsUsage_AndLeavesStateUnchanged()
    {
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Elevated);
        var (service, dropEvent, _) = BuildService();

        service.TryPostChat(zone, session, state, "ygdrop foo", Link());

        Assert.False(dropEvent.Enabled);
        await PacketAssert.AssertSentAsync(pipe, SystemChat("Usage: ygdrop on|off|status"));
    }

    [Fact]
    public async Task Ygdrop_BelowElevatedTier_DeniedNoPermission_AndNoStateChange()
    {
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Basic);
        var (service, dropEvent, _) = BuildService();

        var handled = service.TryPostChat(zone, session, state, "ygdrop on", Link());

        Assert.True(handled); // consumed, never leaked as chat.
        Assert.False(dropEvent.Enabled);
        Assert.Null(session.DisconnectReason); // under-tier is a denial, not a disconnect.
        await PacketAssert.AssertSentAsync(pipe,
            SystemChat("You do not have permission to use this command."));
    }

    [Fact]
    public void Boss_ValidId_ElevatedGm_SpawnsMonster_AndRaisesShardWideNotice()
    {
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Elevated);
        var (service, _, notice) = BuildService();

        service.TryPostChat(zone, session, state, $"boss {KnownMonsterId}", Link());
        zone.Tick(TimeSpan.FromMilliseconds(50)); // drain the tribe-progress mirror -> spawn.
        ZoneTestKit.DrainOutbound(pipe); // tolerate the AOI monster-creation broadcast.

        Assert.Equal(1, zone.MonsterCount);
        var raised = Assert.Single(notice.Broadcasts);
        Assert.Equal($"A boss (id {KnownMonsterId}) has been summoned.", raised);
    }

    [Fact]
    public async Task Boss_MalformedId_SendsUsage_NoSpawn_NoNotice()
    {
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Elevated);
        var (service, _, notice) = BuildService();

        service.TryPostChat(zone, session, state, "boss notanumber", Link());
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, zone.MonsterCount);
        Assert.Empty(notice.Broadcasts);
        await PacketAssert.AssertSentAsync(pipe, SystemChat("Usage: boss <monster id>"));
    }

    [Fact]
    public async Task Boss_IdBelowOne_SendsUsage_NoSpawn()
    {
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Elevated);
        var (service, _, notice) = BuildService();

        service.TryPostChat(zone, session, state, "boss 0", Link());
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, zone.MonsterCount);
        Assert.Empty(notice.Broadcasts);
        await PacketAssert.AssertSentAsync(pipe, SystemChat("Usage: boss <monster id>"));
    }

    [Fact]
    public async Task Boss_BelowElevatedTier_DeniedNoPermission_NoSpawn()
    {
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Basic);
        var (service, _, notice) = BuildService();

        service.TryPostChat(zone, session, state, $"boss {KnownMonsterId}", Link());
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, zone.MonsterCount);
        Assert.Empty(notice.Broadcasts);
        await PacketAssert.AssertSentAsync(pipe,
            SystemChat("You do not have permission to use this command."));
    }

    [Fact]
    public void Boss_ValidButUncataloguedId_TrustedVerbatim_NoticeRaised_ButNoMonsterSpawns()
    {
        // Hardening surface (S04_MyWork02.cpp:7915-7925): legacy applies NO upper bound, NO allow-list, and NO
        // existence check to the boss id -- any value >= 1 is trusted verbatim into the summon routine. Fenrir
        // reproduces the "trusted verbatim" part (the command is posted and the shard-wide notice is raised
        // unconditionally) but the underlying summon primitive silently no-ops an unknown template id, so the
        // only real-world effect of a bogus id is a phantom notice with no monster -- a strictly safer outcome
        // than legacy, and the correct place for a future allow-list to tighten this (see LocalChatService's
        // HandleBoss remarks). 424242 is >= 1 and absent from the zone's monster catalog.
        const int uncataloguedMonsterId = 424242;
        var (session, _, zone, state) = SetUp((short)GmCommandTier.Elevated);
        var (service, _, notice) = BuildService();

        service.TryPostChat(zone, session, state, $"boss {uncataloguedMonsterId}", Link());
        zone.Tick(TimeSpan.FromMilliseconds(50)); // drain the tribe-progress mirror -> summon lookup (miss).

        Assert.Equal(0, zone.MonsterCount); // unknown template id: the summon primitive's silent no-op safety net.
        var raised = Assert.Single(notice.Broadcasts); // the notice is raised regardless (legacy-faithful).
        Assert.Equal($"A boss (id {uncataloguedMonsterId}) has been summoned.", raised);
    }

    [Fact]
    public async Task Kill200_BasicGm_EchoesOwnCommandTextBackFromOwnName()
    {
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Basic);
        var (service, _, _) = BuildService();

        service.TryPostChat(zone, session, state, "kill200", Link());

        // The self-echo carries the sender's OWN name, not "SYSTEM" -- it is the sender's own text echoed back.
        await PacketAssert.AssertSentAsync(pipe,
            new LocalChatResponse { AvatarName = HeroName, Content = "kill200", Link = Link() });
    }

    [Fact]
    public void Lab_ElevatedGm_ConsumedWithNoEffect_NeverLeakedAsChat()
    {
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Elevated);
        var (service, _, notice) = BuildService();

        var handled = service.TryPostChat(zone, session, state, "lab on", Link());

        Assert.True(handled); // consumed, never posted as ordinary chat.
        Assert.Empty(notice.Broadcasts);
        PacketAssert.AssertNothingSent(pipe); // deferred: no reply, no state change.
    }

    [Fact]
    public async Task Lab_BelowElevatedTier_DeniedNoPermission_NeverLeakedAsChat()
    {
        // The tier gate (Elevated, S04_MyWork02.cpp:7853) fires for `lab` even though the command's own action is
        // DEFERRED -- proving the gate is already correct so a future center-relay implementation inherits it.
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Basic);
        var (service, _, notice) = BuildService();

        var handled = service.TryPostChat(zone, session, state, "lab on", Link());

        Assert.True(handled); // consumed, never posted as ordinary chat.
        Assert.Empty(notice.Broadcasts);
        Assert.Null(session.DisconnectReason); // under-tier is a denial, not a disconnect (chat-path semantics).
        await PacketAssert.AssertSentAsync(pipe,
            SystemChat("You do not have permission to use this command."));
    }

    [Fact]
    public void ClearInventory_BasicGm_ConsumedWithNoEffect_NeverLeakedAsChat()
    {
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Basic);
        var (service, _, _) = BuildService();

        var handled = service.TryPostChat(zone, session, state, "?clear", Link());

        Assert.True(handled);
        PacketAssert.AssertNothingSent(pipe); // deferred pending a durable stored-proc wipe path.
    }

    [Fact]
    public void NonGm_YgdropText_NotIntercepted_NoStateChange()
    {
        var (session, pipe, zone, state) = SetUp(0); // grade 0 -> not a GM at all.
        var (service, dropEvent, _) = BuildService();

        var handled = service.TryPostChat(zone, session, state, "ygdrop on", Link());

        // Falls through to ordinary local chat (posted, not intercepted); the GM flag is never touched, and
        // nothing is sent synchronously (the chat broadcast only happens on a later tick, not driven here).
        Assert.True(handled);
        Assert.False(dropEvent.Enabled);
        PacketAssert.AssertNothingSent(pipe);
    }
}
