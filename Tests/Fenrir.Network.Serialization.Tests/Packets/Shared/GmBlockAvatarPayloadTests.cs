using System.Text;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

// CZ_PROCESS_DATA_SEND tSort 519, "[GM]-BLOCK" (Server/ts25zone/S04_MyWork04.cpp:1487-1515). Rides inside
// GenericActionRequest's (opcode 19) tData blob -- there is no dedicated legacy wire opcode for this command.
public class GmBlockAvatarPayloadTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(13, GmBlockAvatarPayload.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[GmBlockAvatarPayload.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(GmBlockAvatarPayload.WireSize, written);

        Assert.True(GmBlockAvatarPayload.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[13];
        value.Write(actual);

        var expected = new byte[13];
        Encoding.Latin1.GetBytes(value.AvatarName, expected);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GmBlockAvatarPayload.TryRead(new byte[12], out _));
    }

    [Fact]
    public void TryRead_DecodesFromFirst13BytesOfLargerBuffer()
    {
        // GenericActionHandler reads this out of the first 13 bytes of GenericActionRequest.Data (130 bytes),
        // not a dedicated 13-byte packet -- this exercises that "leading slice of a larger buffer" shape.
        var data = new byte[130];
        Encoding.Latin1.GetBytes("Griefer", data.AsSpan(0, 13));

        Assert.True(GmBlockAvatarPayload.TryRead(data, out var payload));
        Assert.Equal("Griefer", payload.AvatarName);
    }

    private static GmBlockAvatarPayload CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new GmBlockAvatarPayload { AvatarName = v.NextString(13) };
    }
}
