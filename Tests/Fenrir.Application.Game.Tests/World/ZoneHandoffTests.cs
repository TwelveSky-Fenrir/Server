using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneHandoffTests
{
    [Fact]
    public void Leave_WithHandoffTarget_RemovesFromSourceImmediately_ButTargetOnlyLearnsOnItsOwnTick()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        source.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(source.TryGetPlayer(10, out _));

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(source.TryGetPlayer(10, out _));

        Assert.False(target.TryGetPlayer(10, out _));

        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out _));
    }

    [Fact]
    public void Leave_WithHandoffTarget_StateArrivesIntactAtTheTarget()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1,
            "Hero", 123f, 4f, 456f)));
        source.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(source.TryGetPlayer(10, out var before));

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var after));
        Assert.Equal((short)2, after!.MapId);
        Assert.Equal(before!.Name, after.Name);
        Assert.Equal(before.PosX, after.PosX);
        Assert.Equal(before.PosY, after.PosY);
        Assert.Equal(before.PosZ, after.PosZ);
        Assert.Equal(before.Heading, after.Heading);
        Assert.Equal(before.Life, after.Life);
        Assert.Equal(before.MaxLife, after.MaxLife);
        Assert.Equal(before.Mana, after.Mana);
        Assert.Equal(before.MaxMana, after.MaxMana);
        Assert.Equal(before.Tribe, after.Tribe);
        Assert.Equal(before.Gender, after.Gender);
        Assert.Equal(before.HeadType, after.HeadType);
        Assert.Equal(before.FaceType, after.FaceType);
        Assert.Equal(before.Level, after.Level);
        Assert.Same(session, after.Session);

        Assert.Equal(before.FlushSequence + 1, after.FlushSequence);
    }

    [Fact]
    public void Leave_WithHandoffTarget_CarriesTheRememberedCashCatalogVersionThrough()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        source.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(source.TryGetPlayer(10, out var before));
        before!.KnownCashCatalogVersion = 42;

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var after));
        Assert.Equal(42, after!.KnownCashCatalogVersion);
    }

    [Fact]
    public void Leave_WithHandoffTarget_CarriesTheLiveHeroRankPointsThrough_NotResetToStalePersistedValue()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        source.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(source.TryGetPlayer(10, out var before));
        before!.HeroRankPoints = 77;

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var after));
        Assert.Equal(77, after!.HeroRankPoints);
    }

    [Fact]
    public void Leave_WithHandoffTarget_CarriesTheLivePetBagDateThrough_NotResetToZero()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        source.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(source.TryGetPlayer(10, out var before));
        before!.PetBagDate = 20991231;

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var after));
        Assert.Equal(20991231, after!.PetBagDate);
    }

    [Fact]
    public void Leave_WithHandoffTarget_CarriesTheLiveDropItemTimeThrough_NotResetToZero()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        source.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(source.TryGetPlayer(10, out var before));
        before!.DropItemTime = 42;

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var after));
        Assert.Equal(42, after!.DropItemTime);
    }

    [Fact]
    public void Leave_WithHandoffTarget_CarriesTheLiveWarPointThrough_NotResetToZero()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        source.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(source.TryGetPlayer(10, out var before));
        before!.WarPoint = 1234;

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var after));
        Assert.Equal(1234, after!.WarPoint);
    }

    [Fact]
    public void Leave_WithHandoffTarget_CarriesTheLiveQuestProgressThrough_NotResetToDefault()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        source.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(source.TryGetPlayer(10, out var before));
        before!.QuestStepPermanent = 4;
        before.QuestActiveFlag = 1;
        before.QuestSort = 3;
        before.QuestTargetPhase = 7;
        before.QuestKillCounter = 9;

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var after));
        Assert.Equal(4, after!.QuestStepPermanent);
        Assert.Equal(1, after.QuestActiveFlag);
        Assert.Equal(3, after.QuestSort);
        Assert.Equal(7, after.QuestTargetPhase);
        Assert.Equal(9, after.QuestKillCounter);
    }

    [Fact]
    public void Leave_WithHandoffTarget_CarriesTheLiveDailyMissionCountersThrough_NotResetToZero()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        source.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(source.TryGetPlayer(10, out var before));
        before!.MissionJoinWar = 1;
        before.MissionKillOtherTribe = 12;
        before.MissionKillMonster = 34;
        before.MissionPlayTime = 56;

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var after));
        Assert.Equal(1, after!.MissionJoinWar);
        Assert.Equal(12, after.MissionKillOtherTribe);
        Assert.Equal(34, after.MissionKillMonster);
        Assert.Equal(56, after.MissionPlayTime);
    }

    [Fact]
    public void Leave_WithHandoffTarget_CarriesTheLiveAutoHuntConfigurationThrough_NotResetToDisabled()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        source.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(source.TryGetPlayer(10, out var before));
        before!.AutoHuntEnabled = true;
        var config = new AutoHunt
        {
            BuffType = 1,
            BuffStore = new int[16],
            HuntType = 2,
            AttackType = new int[4],
            MonNum = 3,
            ItemType = 4,
            InvenCmd = 5,
            DeathCmd = 6,
            AnimalPreyCmd = 7,
            AnimalFoodCmd = 8
        };
        before.AutoHuntConfig = config;
        before.AutoLifeRatio = 2;
        before.AutoManaRatio = 3;

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var after));
        Assert.True(after!.AutoHuntEnabled);
        Assert.Equal(config, after.AutoHuntConfig);
        Assert.Equal((byte)2, after.AutoLifeRatio);
        Assert.Equal((byte)3, after.AutoManaRatio);
    }

    [Fact]
    public void Leave_WithHandoffTarget_CarriesTheLivePetGrowthAndActivityThrough_NotResetToFreshlyHatched()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        source.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(source.TryGetPlayer(10, out var before));
        before!.PetGrowth = 250;
        before.PetActivity = 80;

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var after));
        Assert.Equal(250, after!.PetGrowth);
        Assert.Equal((byte)80, after.PetActivity);
    }

    [Fact]
    public void Leave_WithHandoffTarget_CarriesTheLiveRuneSocketsThrough_NotResetToEmpty()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        source.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(source.TryGetPlayer(10, out var before));
        before!.RuneSystem = before.RuneSystem.SetItem(0, 93514);
        before.RuneSystemStat = before.RuneSystemStat.SetItem(0, 12345);

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var after));
        Assert.Equal(93514, after!.RuneSystem[0]);
        Assert.Equal(12345, after.RuneSystemStat[0]);
        Assert.Equal(0, after.RuneSystem[1]);
        Assert.Equal(0, after.RuneSystem[2]);
        Assert.Equal(0, after.RuneSystem[3]);
    }

    [Fact]
    public void Enter_OnAFreshLogin_StartsAtTheCashCatalogVersionUnknownSentinel_NotZero()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        Assert.Equal(PlayerRuntimeState.CashCatalogVersionUnknown, state!.KnownCashCatalogVersion);
    }

    [Fact]
    public void Leave_WithHandoffTarget_RepointsTheSessionsCurrentZone()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        source.Tick(TimeSpan.FromMilliseconds(50));
        session.CurrentZone = source;

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Same(target, session.CurrentZone);
    }

    [Fact]
    public void Request_UsesZoneTransfer_ToPerformTheSameHandoff()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        source.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(ZoneTransfer.Request(source, target, 10));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(source.TryGetPlayer(10, out _));
        Assert.True(target.TryGetPlayer(10, out _));
    }

    [Fact]
    public void Leave_WithHandoffPosition_LandsAtTheOverriddenPosition_NotWhereverThePlayerWasStanding()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 10f, posY: 0f, posZ: 10f)));
        source.Tick(TimeSpan.FromMilliseconds(50));

        source.Post(ZoneCommand.Leave(10, target, (500f, 7f, 900f)));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var arrived));
        Assert.Equal(500f, arrived!.PosX);
        Assert.Equal(7f, arrived.PosY);
        Assert.Equal(900f, arrived.PosZ);
    }

    [Fact]
    public void Leave_WithoutHandoffTarget_IsAPlainDisconnect_PlayerEndsUpInNoZone()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Leave(10));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(zone.TryGetPlayer(10, out _));
        Assert.Equal(0, zone.PlayerCount);
    }

    [Fact]
    public void Leave_WithoutHandoffTarget_RemovesFromTheCrossShardLocationDirectory_ScopedToThisShard()
    {
        var characterShardLocations = new FakeCharacterShardLocationRepository();
        var options = ZoneTestKit.Options();
        options.ShardId = 7;
        var zone = ZoneTestKit.CreateZone(1, options, characterShardLocations: characterShardLocations);
        var (session, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Leave(10));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Contains(characterShardLocations.RemoveCalls, c => c.CharacterId == 10 && c.ShardId == 7);
    }

    [Fact]
    public void Leave_WithoutHandoffTarget_WhileMovingZone_DoesNotRemoveFromTheCrossShardLocationDirectory()
    {
        var characterShardLocations = new FakeCharacterShardLocationRepository();
        var options = ZoneTestKit.Options();
        options.ShardId = 7;
        var zone = ZoneTestKit.CreateZone(1, options, characterShardLocations: characterShardLocations);
        var (session, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.MarkZoneTransferPending(10));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Leave(10));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Empty(characterShardLocations.RemoveCalls);
    }

    [Fact]
    public void MarkZoneTransferPending_SetsIsMovingZoneAndStampsTheRegistrationTimestamp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var before));
        Assert.False(before!.IsMovingZone);
        Assert.Equal(default, before.ZoneTransferRegisteredAtUtc);

        var beforeStamp = DateTime.UtcNow;
        zone.Post(ZoneCommand.MarkZoneTransferPending(10));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var after));
        Assert.True(after!.IsMovingZone);
        Assert.True(after.ZoneTransferRegisteredAtUtc >= beforeStamp);
    }

    [Fact]
    public void Leave_WithHandoffTarget_NeverTouchesTheCrossShardLocationDirectory()
    {
        var characterShardLocations = new FakeCharacterShardLocationRepository();
        var source = ZoneTestKit.CreateZone(1, characterShardLocations: characterShardLocations);
        var target = ZoneTestKit.CreateZone(2);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        source.Tick(TimeSpan.FromMilliseconds(50));

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Empty(characterShardLocations.RemoveCalls);
    }

    [Fact]
    public void Leave_WithoutHandoffTarget_AndNoCharacterShardLocationRepositoryConfigured_NeverThrows()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Leave(10));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(zone.TryGetPlayer(10, out _));
    }

    [Fact]
    public void Leave_WithoutHandoffTarget_UnsticksTheAcceptedDuelPartner()
    {
        var duelRegistry = new DuelRegistry();
        var zone = ZoneTestKit.CreateZone(1, duelRegistry: duelRegistry);
        var (sessionA, _) = ZoneTestKit.CreateSession(1);
        var (sessionB, _) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1)));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionB, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        duelRegistry.TryAsk(10, 20, false);
        duelRegistry.TryAnswer(20, true, out _);
        Assert.True(duelRegistry.IsNegotiating(10));

        zone.Post(ZoneCommand.Leave(20));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(duelRegistry.IsNegotiating(10));
    }

    [Fact]
    public void Leave_WithoutHandoffTarget_UnsticksTheAcceptedFriendPartner()
    {
        var friendRegistry = new FriendRegistry();
        var zone = ZoneTestKit.CreateZone(1, friendRegistry: friendRegistry);
        var (sessionA, _) = ZoneTestKit.CreateSession(1);
        var (sessionB, _) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1)));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionB, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        friendRegistry.TryAsk(10, 20);
        friendRegistry.TryAnswer(20, true, out _);
        Assert.True(friendRegistry.IsNegotiating(10));

        zone.Post(ZoneCommand.Leave(20));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(friendRegistry.IsNegotiating(10));
    }
}
