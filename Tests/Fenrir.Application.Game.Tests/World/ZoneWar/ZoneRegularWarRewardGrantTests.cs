using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class ZoneRegularWarRewardGrantTests
{
    private static (Zone Zone, PlayerRuntimeState State, FakeDuplexPipe Pipe) SetUp(short mapId = 49,
        float posX = 10f, float posY = 0f, float posZ = 20f, short level = 100, short level2 = 0)
    {
        var zone = ZoneTestKit.CreateZone(mapId);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        var enterData = ZoneTestKit.EnterData(session, mapId, posX: posX, posY: posY, posZ: posZ, level: level) with
        {
            Level2 = level2
        };
        zone.Post(ZoneCommand.Enter(1, enterData));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var state));
        ZoneTestKit.DrainOutbound(pipe);
        return (zone, state!, pipe);
    }

    private static RegularWarRewardGrant Grant(
        int characterId = 1,
        bool? isWinningSide = true,
        long money = 0,
        int experience = 0,
        int cpBonus = 0,
        int heroRankPoints = 0,
        int leaderboardCp = 0,
        bool requestItemDrop = false)
    {
        return new RegularWarRewardGrant(characterId, isWinningSide, money, experience, cpBonus, heroRankPoints,
            leaderboardCp, requestItemDrop);
    }

    [Fact]
    public void CharacterNoLongerTracked_IsANoOp()
    {
        var zone = ZoneTestKit.CreateZone(49);

        zone.Post(ZoneCommand.ApplyRegularWarReward(Grant(999, money: 1000)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(zone.TryGetPlayer(999, out _));
    }

    [Fact]
    public void MidZoneTransfer_IsANoOp_NoMoneyQueued_NoContributionPointsGranted()
    {
        var (zone, state, _) = SetUp();
        state.IsMovingZone = true;

        zone.Post(ZoneCommand.ApplyRegularWarReward(Grant(money: 5000, cpBonus: 50)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Empty(zone.DrainPendingMoneyGrants());
        Assert.Equal(0, state.ContributionPoints);
    }

    [Fact]
    public void PositiveMoney_IsQueuedAsAWriteBehindGrant_AndNotifiesTheDelta()
    {
        var (zone, _, pipe) = SetUp();

        zone.Post(ZoneCommand.ApplyRegularWarReward(Grant(money: 12_000_000)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var grants = zone.DrainPendingMoneyGrants();
        Assert.Single(grants);
        Assert.Equal(1, grants[0].CharacterId);
        Assert.Equal(12_000_000, grants[0].Amount);

        var payload = ZoneTestKit.DrainOutbound(pipe).AsSpan(1);
        Assert.Equal(23, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(12_000_000, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
    }

    [Fact]
    public void ZeroMoney_QueuesNothing()
    {
        var (zone, _, _) = SetUp();

        zone.Post(ZoneCommand.ApplyRegularWarReward(Grant(money: 0)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Empty(zone.DrainPendingMoneyGrants());
    }

    [Fact]
    public void PositiveExperience_IsApplied_AndNotifiesTheDelta()
    {
        var (zone, state, pipe) = SetUp();
        var before = state.Experience;

        zone.Post(ZoneCommand.ApplyRegularWarReward(Grant(experience: 777)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(before + 777, state.Experience);

        var outbound = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(26, outbound.Length);
        var second = outbound.AsSpan(14);
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(second));
        Assert.Equal(777, BinaryPrimitives.ReadInt32LittleEndian(second[4..]));
    }

    [Fact]
    public void CpBonusOnly_IsGranted_AndNotifiesTheDelta()
    {
        var (zone, state, pipe) = SetUp();

        zone.Post(ZoneCommand.ApplyRegularWarReward(Grant(cpBonus: 50)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(50, state.ContributionPoints);

        var payload = ZoneTestKit.DrainOutbound(pipe).AsSpan(1);
        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(50, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
    }

    [Fact]
    public void LeaderboardCpOnly_IsGranted_AndNotifiesTheDelta()
    {
        var (zone, state, pipe) = SetUp();

        zone.Post(ZoneCommand.ApplyRegularWarReward(Grant(leaderboardCp: 100)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(100, state.ContributionPoints);

        var payload = ZoneTestKit.DrainOutbound(pipe).AsSpan(1);
        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(100, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
    }

    [Fact]
    public void CpBonusAndLeaderboardCp_TogetherSumOnTheCharacter()
    {
        var (zone, state, _) = SetUp();

        zone.Post(ZoneCommand.ApplyRegularWarReward(Grant(cpBonus: 50, leaderboardCp: 100)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(150, state.ContributionPoints);
    }

    [Fact]
    public void HeroRankPoints_BelowCombinedLevelGate_GrantsNothing()
    {
        var (zone, state, _) = SetUp(level: 100, level2: 0);

        zone.Post(ZoneCommand.ApplyRegularWarReward(Grant(heroRankPoints: 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, state.HeroRankPoints);
    }

    [Fact]
    public void HeroRankPoints_AtOrAboveCombinedLevelGate_IsGranted()
    {
        var (zone, state, _) = SetUp(level: 100, level2: 13);

        zone.Post(ZoneCommand.ApplyRegularWarReward(Grant(heroRankPoints: 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(10, state.HeroRankPoints);
    }

    [Fact]
    public void RequestItemDrop_True_DropsBothFixedGroundItems()
    {
        var (zone, _, _) = SetUp();

        zone.Post(ZoneCommand.ApplyRegularWarReward(Grant(requestItemDrop: true)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(2, zone.GroundItemCount);
    }

    [Fact]
    public void RequestItemDrop_False_DropsNothing()
    {
        var (zone, _, _) = SetUp();

        zone.Post(ZoneCommand.ApplyRegularWarReward(Grant(requestItemDrop: false)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, zone.GroundItemCount);
    }

    [Fact]
    public void DrawGrant_MoneyExperienceCpZero_ItemDropFalse_StillHonorsHeroRankPoints()
    {
        var (zone, state, _) = SetUp(level: 113, level2: 0);

        zone.Post(ZoneCommand.ApplyRegularWarReward(Grant(isWinningSide: null, heroRankPoints: 3,
            requestItemDrop: false)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(3, state.HeroRankPoints);
        Assert.Equal(0, zone.GroundItemCount);
        Assert.Empty(zone.DrainPendingMoneyGrants());
    }
}
