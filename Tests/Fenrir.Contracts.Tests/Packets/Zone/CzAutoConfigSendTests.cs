using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>
///     CZ_AUTO_CONFIG_SEND (116 bytes = 4 + 112). The golden encoder below is hand-built from the C++
///     layout (Sort @0, AUTO_HUNT @4: BuffType, BuffStore[16], HuntType, AttackType[4], MonNum,
///     ItemType, InvenCmd, DeathCmd, AnimalPreyCmd, AnimalFoodCmd), independent of the generated
///     <c>Write</c>.
/// </summary>
public class CzAutoConfigSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(116, CzAutoConfigSend.PayloadSize);
        Assert.Equal(4 + AutoHunt.WireSize, CzAutoConfigSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.AutoConfigSend, CzAutoConfigSend.Opcode);
    }

    [Fact]
    public void TryRead_RoundTrips_ViaManualGoldenEncoding()
    {
        var value = CreatePopulated();

        var golden = new byte[116];
        EncodeGolden(golden, value);

        Assert.True(CzAutoConfigSend.TryRead(golden, out var decoded));
        Assert.Equal(value.Sort, decoded.Sort);
        StructuralAssert.DeepEqual(value.AutoHunt, decoded.AutoHunt);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzAutoConfigSend.TryRead(new byte[115], out _));
    }

    private static CzAutoConfigSend CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new CzAutoConfigSend
        {
            Sort = 1,
            AutoHunt = new AutoHunt
            {
                BuffType = v.NextInt(),
                BuffStore = v.NextIntArray(16),
                HuntType = v.NextInt(),
                AttackType = v.NextIntArray(4),
                MonNum = v.NextInt(),
                ItemType = v.NextInt(),
                InvenCmd = v.NextInt(),
                DeathCmd = v.NextInt(),
                AnimalPreyCmd = v.NextInt(),
                AnimalFoodCmd = v.NextInt()
            }
        };
    }

    private static void EncodeGolden(Span<byte> destination, CzAutoConfigSend value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Sort);

        var hunt = destination[4..];
        var offset = 0;
        BinaryPrimitives.WriteInt32LittleEndian(hunt[offset..], value.AutoHunt.BuffType);
        offset += 4;
        for (var i = 0; i < 16; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(hunt[offset..], value.AutoHunt.BuffStore[i]);
            offset += 4;
        }

        BinaryPrimitives.WriteInt32LittleEndian(hunt[offset..], value.AutoHunt.HuntType);
        offset += 4;
        for (var i = 0; i < 4; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(hunt[offset..], value.AutoHunt.AttackType[i]);
            offset += 4;
        }

        BinaryPrimitives.WriteInt32LittleEndian(hunt[offset..], value.AutoHunt.MonNum);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(hunt[offset..], value.AutoHunt.ItemType);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(hunt[offset..], value.AutoHunt.InvenCmd);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(hunt[offset..], value.AutoHunt.DeathCmd);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(hunt[offset..], value.AutoHunt.AnimalPreyCmd);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(hunt[offset..], value.AutoHunt.AnimalFoodCmd);
    }
}
