using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcAvatarEffectValueInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(428, AvatarEffectStateResponse.PayloadSize);
        Assert.Equal(4 + 4 + 70 * 4 + 35 * 4, AvatarEffectStateResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.AvatarEffectState, AvatarEffectStateResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var effectValue = Enumerable.Range(1, 70).ToArray();
        var effectValueState = Enumerable.Range(1000, 35).ToArray();
        var packet = new AvatarEffectStateResponse
        {
            ServerIndex = 17,
            UniqueNumber = 555_222_111u,
            EffectValue = effectValue,
            EffectValueState = effectValueState
        };

        Span<byte> buffer = new byte[AvatarEffectStateResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(AvatarEffectStateResponse.PayloadSize, written);
        Assert.Equal(17, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal(555_222_111u, BinaryPrimitives.ReadUInt32LittleEndian(buffer[4..]));

        for (var i = 0; i < 70; i++)
            Assert.Equal(effectValue[i], BinaryPrimitives.ReadInt32LittleEndian(buffer[(8 + i * 4)..]));

        for (var i = 0; i < 35; i++)
            Assert.Equal(effectValueState[i], BinaryPrimitives.ReadInt32LittleEndian(buffer[(288 + i * 4)..]));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var effectValue = Enumerable.Range(1, 70).ToArray();
        var effectValueState = Enumerable.Range(1000, 35).ToArray();
        var packet = new AvatarEffectStateResponse
        {
            ServerIndex = 17,
            UniqueNumber = 555_222_111u,
            EffectValue = effectValue,
            EffectValueState = effectValueState
        };

        var golden = new byte[428];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 17);
        BinaryPrimitives.WriteUInt32LittleEndian(golden.AsSpan(4), 555_222_111u);
        for (var i = 0; i < 70; i++)
            BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8 + i * 4), effectValue[i]);
        for (var i = 0; i < 35; i++)
            BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(288 + i * 4), effectValueState[i]);

        Span<byte> buffer = new byte[AvatarEffectStateResponse.PayloadSize];
        packet.Write(buffer);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
