using System.Buffers.Binary;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

public class MissionDateTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(16, MissionDate.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var v = new SequentialValueFactory();
        var missionDate = new MissionDate
        {
            JoinWar = v.NextInt(),
            KillOtherTribe = v.NextInt(),
            KillMonster = v.NextInt(),
            PlayTime = v.NextInt()
        };

        var buffer = new byte[MissionDate.WireSize];
        var written = missionDate.Write(buffer);
        Assert.Equal(MissionDate.WireSize, written);

        Assert.True(MissionDate.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(missionDate, roundTripped);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var buffer = new byte[MissionDate.WireSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4, 4), 2);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8, 4), 3);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(12, 4), 4);

        var ok = MissionDate.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(1, packet.JoinWar);
        Assert.Equal(2, packet.KillOtherTribe);
        Assert.Equal(3, packet.KillMonster);
        Assert.Equal(4, packet.PlayTime);
    }
}
