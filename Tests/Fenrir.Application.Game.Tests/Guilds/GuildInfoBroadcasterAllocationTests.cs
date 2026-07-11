using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.Guilds;

[Collection(AllocationRegressionCollection.Name)]
public class GuildInfoBroadcasterAllocationTests
{
    private const int GuildId = 77;
    private const int TotalPlayers = 60;

    private static (ZoneRegistry Zones, List<FakeDuplexPipe> Pipes) BuildWorld(int guildMemberCount)
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([1]);
        var zone = registry[1];
        var pipes = new List<FakeDuplexPipe>();

        for (var i = 0; i < TotalPlayers; i++)
        {
            var characterId = i + 1;
            var (session, pipe) = ZoneTestKit.CreateSession(characterId);
            pipes.Add(pipe);
            zone.Post(ZoneCommand.Enter(characterId,
                ZoneTestKit.EnterData(session, 1, $"P{characterId}", 100f + i * 3f)));
        }

        zone.Tick(TimeSpan.FromMilliseconds(50));
        foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);

        for (var i = 0; i < TotalPlayers; i++)
        {
            Assert.True(zone.TryGetPlayer(i + 1, out var state));
            state!.GuildId = i < guildMemberCount ? GuildId : null;
        }

        return (registry, pipes);
    }

    private static GuildInfo MinimalGuildInfo()
    {
        return new GuildInfo
        {
            Name = "Aesir",
            Grade = 1,
            Master = "Odin",
            SubMaster1 = "",
            SubMaster2 = "",
            MemberNames = CreateStringArray(50),
            MemberRoles = new int[50],
            MemberCallNames = CreateStringArray(50),
            Notices = CreateStringArray(4),
            Point = 0,
            BuffType = 0,
            BuffState = 0,
            BuffTime = 0,
            ChangeLeader = 0
        };
    }

    private static string[] CreateStringArray(int count)
    {
        var array = new string[count];
        Array.Fill(array, string.Empty);
        return array;
    }

        private static long MeasureAllocatedBytes(ZoneRegistry zones, IReadOnlyList<FakeDuplexPipe> pipes, int iterations)
    {
        var info = MinimalGuildInfo();

        GuildInfoBroadcaster.BroadcastGuildInfo(zones, GuildId, 1, info);
        foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);

        long total = 0;
        for (var i = 0; i < iterations; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            GuildInfoBroadcaster.BroadcastGuildInfo(zones, GuildId, 1, info);
            total += GC.GetAllocatedBytesForCurrentThread() - before;

            foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);
        }

        return total;
    }

    [Fact]
    public void BroadcastGuildInfo_PerCallAllocation_DoesNotScaleWithMatchingMemberCount()
    {
        const int iterations = 200;

        var (fewMembersWorld, fewPipes) = BuildWorld(3);
        var fewPerCall = MeasureAllocatedBytes(fewMembersWorld, fewPipes, iterations) / (double)iterations;

        var (manyMembersWorld, manyPipes) = BuildWorld(55);
        var manyPerCall = MeasureAllocatedBytes(manyMembersWorld, manyPipes, iterations) / (double)iterations;

        Assert.True(manyPerCall < 4096,
            $"Expected < 4096 bytes/call for 55 matching members, was {manyPerCall:F1} (3-member baseline: {fewPerCall:F1}).");

        Assert.True(manyPerCall <= fewPerCall + 2048,
            $"Per-broadcast allocation scaled with matching-member count: {fewPerCall:F1} bytes/call at 3 members vs. {manyPerCall:F1} bytes/call at 55 members.");
    }
}
