using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>
///     ZC_MAKE_ITEM2_RECV (ZONE.h:1357, 29-byte payload) — same typedef as <see cref="UpgradeCapeResponse" />,
///     including the dead trailing <see cref="UpgradeCapeResponse.Padding" /> byte.
/// </summary>
public class ZcMakeItem2RecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(29, CraftLegendaryPetResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.CraftLegendaryPet, CraftLegendaryPetResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[CraftLegendaryPetResponse.PayloadSize];
        value.Write(actual);

        var expected = new byte[29];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static CraftLegendaryPetResponse CreatePopulated()
    {
        return new CraftLegendaryPetResponse
        {
            Result = 11,
            Value = [100, 101, 102, 103, 104, 105],
            Padding = 0xAB
        };
    }

    private static void EncodeGolden(Span<byte> destination, CraftLegendaryPetResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Result);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(4 + i * 4)..], value.Value[i]);
        destination[28] = value.Padding;
    }
}
