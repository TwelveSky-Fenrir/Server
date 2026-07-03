using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>ZC_MAKE_SKILL_RECV (ZONE.h:537, 28-byte payload) — same typedef as <see cref="ZcMakeItemRecv" />.</summary>
public class ZcMakeSkillRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(28, ZcMakeSkillRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.MakeSkillRecv, ZcMakeSkillRecv.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[ZcMakeSkillRecv.PayloadSize];
        value.Write(actual);

        var expected = new byte[28];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static ZcMakeSkillRecv CreatePopulated()
    {
        return new ZcMakeSkillRecv { Result = 11, Value = [100, 101, 102, 103, 104, 105] };
    }

    private static void EncodeGolden(Span<byte> destination, ZcMakeSkillRecv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Result);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(4 + i * 4)..], value.Value[i]);
    }
}
