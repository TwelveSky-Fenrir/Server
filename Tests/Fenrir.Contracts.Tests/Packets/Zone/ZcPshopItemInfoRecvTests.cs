using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcPshopItemInfoRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(77, ZcPshopItemInfoRecv.PayloadSize);
        Assert.Equal(4 + 13 + 4 + 4 + 36 + 12 + 4, ZcPshopItemInfoRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.PshopItemInfoRecv, ZcPshopItemInfoRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var itemInfo = new int[9];
        for (var i = 0; i < itemInfo.Length; i++)
            itemInfo[i] = i < 5 ? (i + 1) * 100 : 0;
        var socketInfo = new[] { 1, 2, 3 };

        var packet = new ZcPshopItemInfoRecv
        {
            UniqueNumber = 0x1234_5678u,
            AvatarName = "Baldr",
            Page = 1,
            Index = 2,
            PshopItemInfo = itemInfo,
            SocketInfo = socketInfo,
            CycleTick = 999_888u
        };

        var actual = new byte[ZcPshopItemInfoRecv.PayloadSize];
        var written = packet.Write(actual);
        Assert.Equal(ZcPshopItemInfoRecv.PayloadSize, written);

        var expected = new byte[ZcPshopItemInfoRecv.PayloadSize];
        BinaryPrimitives.WriteUInt32LittleEndian(expected, 0x1234_5678u);
        WireTestKit.WriteFixedString(expected.AsSpan(4, 13), "Baldr");
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(17), 1);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(21), 2);
        for (var i = 0; i < itemInfo.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(25 + i * 4), itemInfo[i]);
        for (var i = 0; i < socketInfo.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(61 + i * 4), socketInfo[i]);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(73), 999_888u);

        Assert.Equal(expected, actual);
    }
}
