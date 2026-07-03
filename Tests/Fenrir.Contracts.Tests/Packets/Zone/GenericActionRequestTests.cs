using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>
///     CZ_PROCESS_DATA_SEND (CLIENT.h:203-207, 134-byte payload). Golden encoder built by hand,
///     independent of the generated <c>Write</c>.
/// </summary>
public class CzProcessDataSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(134, GenericActionRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GenericAction, GenericActionRequest.Opcode);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var value = CreatePopulated();
        var golden = new byte[134];
        EncodeGolden(golden, value);

        Assert.True(GenericActionRequest.TryRead(golden, out var decoded));
        Assert.Equal(value.Sort, decoded.Sort);
        Assert.True(value.Data.AsSpan().SequenceEqual(decoded.Data));
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GenericActionRequest.TryRead(new byte[133], out _));
    }

    private static GenericActionRequest CreatePopulated()
    {
        var data = new byte[130];
        for (var i = 0; i < data.Length; i++)
            data[i] = (byte)(i + 1);

        return new GenericActionRequest { Sort = 555, Data = data };
    }

    private static void EncodeGolden(Span<byte> destination, GenericActionRequest value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Sort);
        value.Data.AsSpan().CopyTo(destination[4..]);
    }
}
