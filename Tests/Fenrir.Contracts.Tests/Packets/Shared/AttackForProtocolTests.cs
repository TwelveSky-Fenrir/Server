using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

// Golden encoder is hand-built from the C++ ATTACK_FOR_PROTOCOL layout, independent of the generated Write.
public class AttackForProtocolTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(68, AttackForProtocol.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[AttackForProtocol.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(AttackForProtocol.WireSize, written);

        Assert.True(AttackForProtocol.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[68];
        value.Write(actual);

        var expected = new byte[68];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var value = CreatePopulated();
        var golden = new byte[68];
        EncodeGolden(golden, value);

        Assert.True(AttackForProtocol.TryRead(golden, out var decoded));
        StructuralAssert.DeepEqual(value, decoded);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(AttackForProtocol.TryRead(new byte[67], out _));
    }

    private static AttackForProtocol CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new AttackForProtocol
        {
            Case = v.NextInt(),
            ServerIndex1 = v.NextInt(),
            UniqueNumber1 = v.NextUInt(),
            ServerIndex2 = v.NextInt(),
            UniqueNumber2 = v.NextUInt(),
            SenderLocation = v.NextFloatArray(3),
            AttackActionValue1 = v.NextInt(),
            AttackActionValue2 = v.NextInt(),
            AttackActionValue3 = v.NextInt(),
            AttackActionValue4 = v.NextInt(),
            AttackResultValue = v.NextInt(),
            AttackCriticalExist = v.NextInt(),
            AttackElementDamage = v.NextInt(),
            AttackViewDamageValue = v.NextInt(),
            AttackRealDamageValue = v.NextInt()
        };
    }

    private static void EncodeGolden(Span<byte> destination, AttackForProtocol value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Case);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.ServerIndex1);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], value.UniqueNumber1);
        BinaryPrimitives.WriteInt32LittleEndian(destination[12..], value.ServerIndex2);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[16..], value.UniqueNumber2);
        for (var i = 0; i < 3; i++)
            BinaryPrimitives.WriteSingleLittleEndian(destination[(20 + i * 4)..], value.SenderLocation[i]);
        BinaryPrimitives.WriteInt32LittleEndian(destination[32..], value.AttackActionValue1);
        BinaryPrimitives.WriteInt32LittleEndian(destination[36..], value.AttackActionValue2);
        BinaryPrimitives.WriteInt32LittleEndian(destination[40..], value.AttackActionValue3);
        BinaryPrimitives.WriteInt32LittleEndian(destination[44..], value.AttackActionValue4);
        BinaryPrimitives.WriteInt32LittleEndian(destination[48..], value.AttackResultValue);
        BinaryPrimitives.WriteInt32LittleEndian(destination[52..], value.AttackCriticalExist);
        BinaryPrimitives.WriteInt32LittleEndian(destination[56..], value.AttackElementDamage);
        BinaryPrimitives.WriteInt32LittleEndian(destination[60..], value.AttackViewDamageValue);
        BinaryPrimitives.WriteInt32LittleEndian(destination[64..], value.AttackRealDamageValue);
    }
}
