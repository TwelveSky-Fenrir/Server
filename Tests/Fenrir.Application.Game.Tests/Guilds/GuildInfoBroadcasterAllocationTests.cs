using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.Guilds;

/// <summary>
///     GC-allocation regression guard for <c>GuildInfoBroadcaster.BroadcastGuildInfo</c>'s rent-once/write-once
///     idiom: before that fix, every matching guild member independently re-serialized the identical
///     <c>GuildActionResponse</c> via its own <c>Session.Send</c> call -- once per member. This test proves
///     per-broadcast allocated bytes stays flat as the number of MATCHING guild members grows, holding total
///     zone population fixed (so the incidental cost of walking the zone's own player set, e.g.
///     <c>ConcurrentDictionary&lt;,&gt;.Values</c>'s own per-access array snapshot, is identical between the two
///     scenarios below) -- a regression back to per-member re-serialization would show up as a jump proportional
///     to the extra matching members, which the assertions below catch.
/// </summary>
/// <remarks>
///     Every player pipe is drained between measured iterations (outside the counted before/after window) --
///     without that, <see cref="FakeDuplexPipe" />'s underlying <see cref="System.IO.Pipelines.Pipe" /> would
///     keep accumulating never-read bytes across all 200 iterations (each <c>GuildActionResponse</c> frame is
///     ~1.4 KB, dominated by the embedded fixed-size <c>GuildInfo</c>) and eventually need brand-new backing
///     segments once its current one fills -- an artifact of this in-memory test harness never actually reading
///     what it writes (a real session's socket-flush loop continuously drains), not of the broadcast code under
///     test -- and that artifact itself scales with matching-member count (more pipes being written to per
///     call), which would otherwise completely swamp the signal this test is actually after.
/// </remarks>
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

    /// <summary>
    ///     Warms up JIT/ArrayPool once (untracked), then measures bytes allocated across <paramref name="iterations" />
    ///     repeated broadcasts -- each iteration's own before/after snapshot excludes the subsequent pipe drain
    ///     (see class remarks), so only the broadcast call itself is charged.
    /// </summary>
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

        // Generous absolute ceiling: the pre-fix per-member Session.Send/FrameWriter.WriteFrame call, run 55
        // times per broadcast instead of once, would show up here even accounting for normal zone-population-walk
        // overhead (identical between both scenarios below since TotalPlayers is fixed).
        Assert.True(manyPerCall < 4096,
            $"Expected < 4096 bytes/call for 55 matching members, was {manyPerCall:F1} (3-member baseline: {fewPerCall:F1}).");

        // The direct scaling check: ~18x more matching members must not meaningfully increase per-call
        // allocation once the response frame is serialized exactly once per broadcast instead of once per
        // matching member.
        Assert.True(manyPerCall <= fewPerCall + 2048,
            $"Per-broadcast allocation scaled with matching-member count: {fewPerCall:F1} bytes/call at 3 members vs. {manyPerCall:F1} bytes/call at 55 members.");
    }
}
