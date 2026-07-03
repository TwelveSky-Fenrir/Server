using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>
///     CZ_RUNE_SYSTEM_SEND (CLIENT.h:249-256, 20-byte payload): Sort/RuneIndex/ItemIndex/Page/Index. NOTE:
///     field order differs from the ZC 199 response — verified independently here.
/// </summary>
public class CzRuneSystemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, RuneSocketRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.RuneSocket, RuneSocketRequest.Opcode);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var golden = new byte[20];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 11);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 22);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8), 33);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(12), 44);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(16), 55);

        Assert.True(RuneSocketRequest.TryRead(golden, out var decoded));
        Assert.Equal(11, decoded.Sort);
        Assert.Equal(22, decoded.RuneIndex);
        Assert.Equal(33, decoded.ItemIndex);
        Assert.Equal(44, decoded.Page);
        Assert.Equal(55, decoded.Index);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(RuneSocketRequest.TryRead(new byte[19], out _));
    }
}
