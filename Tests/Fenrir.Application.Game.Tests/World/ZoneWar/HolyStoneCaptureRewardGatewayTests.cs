using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.Progression;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class HolyStoneCaptureRewardGatewayTests
{
    private const byte HeroThresholdCombinedLevel = HolyStoneCaptureRewardGateway.HeroRankPointMinimumCombinedLevel;

    private static (HolyStoneCaptureRewardGateway Gateway, HeroRankPointAccumulator HeroRankPoints,
        DirtyTracker<int> DirtyTracker) CreateGateway()
    {
        var heroRankPoints = new HeroRankPointAccumulator();
        var dirtyTracker = new DirtyTracker<int>();
        var gateway = new HolyStoneCaptureRewardGateway(heroRankPoints, dirtyTracker,
            NullLogger<HolyStoneCaptureRewardGateway>.Instance);
        return (gateway, heroRankPoints, dirtyTracker);
    }

    private static (PlayerRuntimeState Player, FakeDuplexPipe Pipe) CreatePlayer(int characterId, short level,
        short level2, byte tribe = 1)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        var player = new PlayerRuntimeState
        {
            CharacterId = characterId,
            Session = session,
            Name = $"Hero{characterId}",
            Tribe = tribe,
            Gender = 0,
            HeadType = 0,
            FaceType = 0,
            Level = level,
            Level2 = level2
        };
        return (player, pipe);
    }

    private const int StatUpdateFrameSize = 13;

    private static (int Sort, int Value, int Value2) ReadStatUpdate(FakeDuplexPipe pipe)
    {
        return ParseStatUpdate(ZoneTestKit.DrainOutbound(pipe), 0);
    }

    private static (int Sort, int Value, int Value2)[] ReadStatUpdates(FakeDuplexPipe pipe, int count)
    {
        var bytes = ZoneTestKit.DrainOutbound(pipe);
        var updates = new (int Sort, int Value, int Value2)[count];
        for (var i = 0; i < count; i++)
            updates[i] = ParseStatUpdate(bytes, i);

        return updates;
    }

    private static (int Sort, int Value, int Value2) ParseStatUpdate(byte[] bytes, int frameIndex)
    {
        var payload = bytes.AsSpan(frameIndex * StatUpdateFrameSize + 1);
        return (BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload[4..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[8..]));
    }

    [Fact]
    public async Task GrantCaptureReward_BelowHeroRankThreshold_OnlyGrantsContributionPoints()
    {
        var (gateway, heroRankPoints, dirtyTracker) = CreateGateway();
        var (player, pipe) = CreatePlayer(10, HeroThresholdCombinedLevel - 1, 0);

        gateway.GrantCaptureReward(player);

        Assert.Equal(HolyStoneCaptureRewardGateway.CapturerContributionPoints, player.ContributionPoints);
        Assert.Equal(0, player.HeroRankPoints);
        Assert.Equal(1, dirtyTracker.Count);

        var (sort, value, value2) = ReadStatUpdate(pipe);
        Assert.Equal(3, sort);
        Assert.Equal(HolyStoneCaptureRewardGateway.CapturerContributionPoints, value);
        Assert.Equal(0, value2);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));

        var fakeRepository = new FakeHeroRankingRepository();
        await heroRankPoints.FlushDirtyAsync(fakeRepository, CancellationToken.None);
        Assert.Empty(fakeRepository.AddPointsCalls);
    }

    [Fact]
    public async Task GrantCaptureReward_AtHeroRankThreshold_GrantsBothContributionAndHeroRankPoints()
    {
        var (gateway, heroRankPoints, _) = CreateGateway();
        var (player, pipe) = CreatePlayer(11, HeroThresholdCombinedLevel, 0);

        gateway.GrantCaptureReward(player);

        Assert.Equal(HolyStoneCaptureRewardGateway.CapturerContributionPoints, player.ContributionPoints);
        Assert.Equal(HolyStoneCaptureRewardGateway.CapturerHeroRankPoints, player.HeroRankPoints);

        var updates = ReadStatUpdates(pipe, 2);
        Assert.Equal(3, updates[0].Sort);
        Assert.Equal(HolyStoneCaptureRewardGateway.CapturerContributionPoints, updates[0].Value);
        Assert.Equal(904, updates[1].Sort);
        Assert.Equal(HolyStoneCaptureRewardGateway.CapturerHeroRankPoints, updates[1].Value);

        var fakeRepository = new FakeHeroRankingRepository();
        await heroRankPoints.FlushDirtyAsync(fakeRepository, CancellationToken.None);
        var call = Assert.Single(fakeRepository.AddPointsCalls);
        Assert.Equal(11, call.CharacterId);
        Assert.Equal(HolyStoneCaptureRewardGateway.CapturerHeroRankPoints, call.Delta);
        Assert.Equal((byte?)player.Tribe, call.TribeId);
        Assert.Equal((int?)player.Level, call.Level);
    }

    [Fact]
    public void GrantParticipationReward_BelowHeroRankThreshold_OnlyGrantsContributionPoints()
    {
        var (gateway, _, _) = CreateGateway();
        var (player, pipe) = CreatePlayer(12, HeroThresholdCombinedLevel - 1, 0);

        gateway.GrantParticipationReward(player);

        Assert.Equal(HolyStoneCaptureRewardGateway.ParticipationContributionPoints, player.ContributionPoints);
        Assert.Equal(0, player.HeroRankPoints);

        var (sort, value, _) = ReadStatUpdate(pipe);
        Assert.Equal(3, sort);
        Assert.Equal(HolyStoneCaptureRewardGateway.ParticipationContributionPoints, value);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task GrantParticipationReward_AtHeroRankThreshold_GrantsBothSmallerAmounts()
    {
        var (gateway, heroRankPoints, _) = CreateGateway();
        var (player, _) = CreatePlayer(13, HeroThresholdCombinedLevel, 0);

        gateway.GrantParticipationReward(player);

        Assert.Equal(HolyStoneCaptureRewardGateway.ParticipationContributionPoints, player.ContributionPoints);
        Assert.Equal(HolyStoneCaptureRewardGateway.ParticipationHeroRankPoints, player.HeroRankPoints);

        var fakeRepository = new FakeHeroRankingRepository();
        await heroRankPoints.FlushDirtyAsync(fakeRepository, CancellationToken.None);
        var call = Assert.Single(fakeRepository.AddPointsCalls);
        Assert.Equal(HolyStoneCaptureRewardGateway.ParticipationHeroRankPoints, call.Delta);
    }
}
