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

        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, MapId)));
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

        Assert.True(handled);
        Assert.False(dropEvent.Enabled);
        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe,
            SystemChat("You do not have permission to use this command."));
    }

    [Fact]
    public void Boss_ValidId_ElevatedGm_SpawnsMonster_AndRaisesShardWideNotice()
    {
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Elevated);
        var (service, _, notice) = BuildService();

        service.TryPostChat(zone, session, state, $"boss {KnownMonsterId}", Link());
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

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
        const int uncataloguedMonsterId = 424242;
        var (session, _, zone, state) = SetUp((short)GmCommandTier.Elevated);
        var (service, _, notice) = BuildService();

        service.TryPostChat(zone, session, state, $"boss {uncataloguedMonsterId}", Link());
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, zone.MonsterCount);
        var raised = Assert.Single(notice.Broadcasts);
        Assert.Equal($"A boss (id {uncataloguedMonsterId}) has been summoned.", raised);
    }

    [Fact]
    public async Task Kill200_BasicGm_EchoesOwnCommandTextBackFromOwnName()
    {
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Basic);
        var (service, _, _) = BuildService();

        service.TryPostChat(zone, session, state, "kill200", Link());

        await PacketAssert.AssertSentAsync(pipe,
            new LocalChatResponse { AvatarName = HeroName, Content = "kill200", Link = Link() });
    }

    [Fact]
    public void Lab_ElevatedGm_ConsumedWithNoEffect_NeverLeakedAsChat()
    {
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Elevated);
        var (service, _, notice) = BuildService();

        var handled = service.TryPostChat(zone, session, state, "lab on", Link());

        Assert.True(handled);
        Assert.Empty(notice.Broadcasts);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task Lab_BelowElevatedTier_DeniedNoPermission_NeverLeakedAsChat()
    {
        var (session, pipe, zone, state) = SetUp((short)GmCommandTier.Basic);
        var (service, _, notice) = BuildService();

        var handled = service.TryPostChat(zone, session, state, "lab on", Link());

        Assert.True(handled);
        Assert.Empty(notice.Broadcasts);
        Assert.Null(session.DisconnectReason);
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
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public void NonGm_YgdropText_NotIntercepted_NoStateChange()
    {
        var (session, pipe, zone, state) = SetUp(0);
        var (service, dropEvent, _) = BuildService();

        var handled = service.TryPostChat(zone, session, state, "ygdrop on", Link());

        Assert.True(handled);
        Assert.False(dropEvent.Enabled);
        PacketAssert.AssertNothingSent(pipe);
    }
}
