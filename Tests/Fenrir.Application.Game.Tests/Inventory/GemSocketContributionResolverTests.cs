using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Tests.Inventory;

public class GemSocketContributionResolverTests
{
    /// <summary>Packs SOCKET_GEM_V2's 12 bytes (unused, num, t1,g1..t5,g5) into 3 little-endian ints.</summary>
    private static (int P1, int P2, int P3) Pack(byte num, params (byte Type, byte Tier)[] sockets)
    {
        Span<byte> bytes = stackalloc byte[12];
        bytes[1] = num;
        for (var i = 0; i < sockets.Length && i < 5; i++)
        {
            bytes[2 + i * 2] = sockets[i].Type;
            bytes[3 + i * 2] = sockets[i].Tier;
        }

        var p1 = BitConverter.ToInt32(bytes[..4]);
        var p2 = BitConverter.ToInt32(bytes.Slice(4, 4));
        var p3 = BitConverter.ToInt32(bytes.Slice(8, 4));
        return (p1, p2, p3);
    }

    [Fact]
    public void UnpackActiveSockets_ZeroActiveCount_ReturnsEmpty()
    {
        var (p1, p2, p3) = Pack(0, (5, 3));

        var result = GemSocketContributionResolver.UnpackActiveSockets(p1, p2, p3);

        Assert.Empty(result);
    }

    [Fact]
    public void UnpackActiveSockets_RoundTripsTypeAndTierPerSocket()
    {
        var (p1, p2, p3) = Pack(3, (10, 1), (20, 2), (30, 3));

        var result = GemSocketContributionResolver.UnpackActiveSockets(p1, p2, p3);

        Assert.Equal(3, result.Length);
        Assert.Equal(new GemSocketContributionResolver.SocketEntry(10, 1), result[0]);
        Assert.Equal(new GemSocketContributionResolver.SocketEntry(20, 2), result[1]);
        Assert.Equal(new GemSocketContributionResolver.SocketEntry(30, 3), result[2]);
    }

    [Fact]
    public void UnpackActiveSockets_OnlyConsultsLeadingActiveCountEntries()
    {
        // 5 socket slots populated, but active count says only 2 -- trailing 3 must never surface.
        var (p1, p2, p3) = Pack(2, (10, 1), (20, 2), (99, 9), (98, 8), (97, 7));

        var result = GemSocketContributionResolver.UnpackActiveSockets(p1, p2, p3);

        Assert.Equal(2, result.Length);
    }

    [Fact]
    public void UnpackActiveSockets_ActiveCountAboveFive_ClampsToFive()
    {
        var (p1, p2, p3) = Pack(255, (1, 1), (2, 2), (3, 3), (4, 4), (5, 5));

        var result = GemSocketContributionResolver.UnpackActiveSockets(p1, p2, p3);

        Assert.Equal(GemSocketContributionResolver.MaxSocketsPerItem, result.Length);
    }

    [Fact]
    public void GetSocketInfo_SumsLookupAcrossActiveSockets()
    {
        var (p1, p2, p3) = Pack(2, (10, 1), (20, 2));

        var total = GemSocketContributionResolver.GetSocketInfo(GemSocketStatRequest.AttackPower, p1, p2, p3,
            (_, entry) => entry.GemType + entry.GemTier);

        Assert.Equal(10 + 1 + 20 + 2, total);
    }

    [Fact]
    public void GetSocketInfo_ZeroActiveSockets_ReturnsZeroWithoutInvokingLookup()
    {
        var (p1, p2, p3) = Pack(0);
        var invoked = false;

        var total = GemSocketContributionResolver.GetSocketInfo(GemSocketStatRequest.AttackPower, p1, p2, p3,
            (_, _) =>
            {
                invoked = true;
                return 999;
            });

        Assert.Equal(0, total);
        Assert.False(invoked);
    }

    [Fact]
    public void GetSocketInfo_SocketWithZeroGemType_SkippedWithoutInvokingLookup()
    {
        var (p1, p2, p3) = Pack(2, (0, 5), (10, 1));
        var invocationCount = 0;

        var total = GemSocketContributionResolver.GetSocketInfo(GemSocketStatRequest.AttackPower, p1, p2, p3,
            (_, entry) =>
            {
                invocationCount++;
                return entry.GemType;
            });

        Assert.Equal(1, invocationCount);
        Assert.Equal(10, total);
    }

    [Fact]
    public void GetSocketInfo_PassesRequestedStatTypeThrough()
    {
        var (p1, p2, p3) = Pack(1, (10, 1));
        GemSocketStatRequest? observed = null;

        GemSocketContributionResolver.GetSocketInfo(GemSocketStatRequest.ElementalDefense, p1, p2, p3,
            (statType, _) =>
            {
                observed = statType;
                return 0;
            });

        Assert.Equal(GemSocketStatRequest.ElementalDefense, observed);
    }

    [Theory]
    [InlineData(GemSocketStatRequest.AttackPower, true)]
    [InlineData(GemSocketStatRequest.Defense, false)]
    [InlineData(GemSocketStatRequest.Life, false)]
    [InlineData(GemSocketStatRequest.Mana, false)]
    [InlineData(GemSocketStatRequest.AttackSuccess, false)]
    [InlineData(GemSocketStatRequest.AttackBlock, false)]
    [InlineData(GemSocketStatRequest.ElementalAttack, false)]
    [InlineData(GemSocketStatRequest.ElementalDefense, false)]
    public void IsLiveInProduction_OnlyAttackPowerIsLive(GemSocketStatRequest statType, bool expectedLive)
    {
        Assert.Equal(expectedLive, GemSocketContributionResolver.IsLiveInProduction(statType));
    }
}
