using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

/// <summary>ZC_SET_HOTKEY_INVENTORY_RECV (ZONE.h:1129-1138, 28-byte payload), no padding.</summary>
public class ZcSetHotkeyInventoryRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(28, AutoHuntHotkeyRebindResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.AutoHuntHotkeyRebind, AutoHuntHotkeyRebindResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[AutoHuntHotkeyRebindResponse.PayloadSize];
        value.Write(actual);

        var expected = new byte[28];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static AutoHuntHotkeyRebindResponse CreatePopulated()
    {
        return new AutoHuntHotkeyRebindResponse
        {
            Page1 = 11,
            Index1 = 22,
            Page2 = 33,
            Index2 = 44,
            Value0 = 55,
            Value1 = 66,
            Value2 = 77
        };
    }

    private static void EncodeGolden(Span<byte> destination, AutoHuntHotkeyRebindResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Page1);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.Index1);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], value.Page2);
        BinaryPrimitives.WriteInt32LittleEndian(destination[12..], value.Index2);
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], value.Value0);
        BinaryPrimitives.WriteInt32LittleEndian(destination[20..], value.Value1);
        BinaryPrimitives.WriteInt32LittleEndian(destination[24..], value.Value2);
    }
}
