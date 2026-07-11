using System.Buffers.Binary;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

public class ObjectForMonsterTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(112, ObjectForMonster.WireSize);
        Assert.Equal(104, ActionInfo.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[ObjectForMonster.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(ObjectForMonster.WireSize, written);

        Assert.True(ObjectForMonster.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[112];
        value.Write(actual);

        var expected = new byte[112];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var value = CreatePopulated();
        var golden = new byte[112];
        EncodeGolden(golden, value);

        Assert.True(ObjectForMonster.TryRead(golden, out var decoded));
        StructuralAssert.DeepEqual(value, decoded);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(ObjectForMonster.TryRead(new byte[111], out _));
    }

    private static ObjectForMonster CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new ObjectForMonster
        {
            Index = v.NextInt(),
            Action = CreateAction(v),
            LifeValue = v.NextInt()
        };
    }

    private static ActionInfo CreateAction(SequentialValueFactory v)
    {
        return new ActionInfo
        {
            Type = v.NextInt(),
            Sort = v.NextInt(),
            Frame = v.NextFloat(),
            Location = v.NextFloatArray(3),
            TargetLocation = v.NextFloatArray(3),
            Front = v.NextFloat(),
            TargetFront = v.NextFloat(),
            PetLocation = v.NextFloatArray(3),
            PetTargetLocation = v.NextFloatArray(3),
            PetFront = v.NextFloat(),
            PetSort = v.NextInt(),
            TargetObjectSort = v.NextInt(),
            TargetObjectIndex = v.NextInt(),
            TargetObjectUniqueNumber = v.NextInt(),
            SkillNumber = v.NextInt(),
            SkillGradeNum1 = v.NextInt(),
            SkillGradeNum2 = v.NextInt(),
            SkillValue = v.NextInt()
        };
    }

    private static int EncodeAction(Span<byte> destination, ActionInfo action)
    {
        var offset = 0;
        WriteInt(destination, ref offset, action.Type);
        WriteInt(destination, ref offset, action.Sort);
        WriteFloat(destination, ref offset, action.Frame);
        WriteFloats(destination, ref offset, action.Location);
        WriteFloats(destination, ref offset, action.TargetLocation);
        WriteFloat(destination, ref offset, action.Front);
        WriteFloat(destination, ref offset, action.TargetFront);
        WriteFloats(destination, ref offset, action.PetLocation);
        WriteFloats(destination, ref offset, action.PetTargetLocation);
        WriteFloat(destination, ref offset, action.PetFront);
        WriteInt(destination, ref offset, action.PetSort);
        WriteInt(destination, ref offset, action.TargetObjectSort);
        WriteInt(destination, ref offset, action.TargetObjectIndex);
        WriteInt(destination, ref offset, action.TargetObjectUniqueNumber);
        WriteInt(destination, ref offset, action.SkillNumber);
        WriteInt(destination, ref offset, action.SkillGradeNum1);
        WriteInt(destination, ref offset, action.SkillGradeNum2);
        WriteInt(destination, ref offset, action.SkillValue);
        return offset;
    }

    private static void EncodeGolden(Span<byte> destination, ObjectForMonster value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Index);
        var actionWritten = EncodeAction(destination.Slice(4, 104), value.Action);
        Assert.Equal(104, actionWritten);
        BinaryPrimitives.WriteInt32LittleEndian(destination[108..], value.LifeValue);
    }

    private static void WriteInt(Span<byte> destination, ref int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[offset..], value);
        offset += 4;
    }

    private static void WriteFloat(Span<byte> destination, ref int offset, float value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(destination[offset..], value);
        offset += 4;
    }

    private static void WriteFloats(Span<byte> destination, ref int offset, float[] values)
    {
        foreach (var value in values)
            WriteFloat(destination, ref offset, value);
    }
}
