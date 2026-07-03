using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzRegisterAvatarSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(372, CzRegisterAvatarSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.RegisterAvatarSend, CzRegisterAvatarSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var action = new ActionInfo
        {
            Type = 3,
            Sort = 1,
            Frame = 12.5f,
            Location = [100f, 0f, 250f],
            TargetLocation = [110f, 0f, 260f],
            Front = 1.5f,
            TargetFront = 2.5f,
            PetLocation = [90f, 0f, 240f],
            PetTargetLocation = [95f, 0f, 245f],
            PetFront = 0.5f,
            PetSort = 2,
            TargetObjectSort = 4,
            TargetObjectIndex = 5,
            TargetObjectUniqueNumber = 777,
            SkillNumber = 42,
            SkillGradeNum1 = 1,
            SkillGradeNum2 = 2,
            SkillValue = 9001
        };

        Span<byte> buffer = new byte[CzRegisterAvatarSend.PayloadSize];
        WireTestKit.WriteFixedString(buffer[..255], "Freya");
        WireTestKit.WriteFixedString(buffer.Slice(255, 13), "Valkyrie");
        var written = WireTestKit.EncodeActionInfo(buffer[268..], action);

        Assert.Equal(ActionInfo.WireSize, written);
        Assert.Equal(268 + ActionInfo.WireSize, CzRegisterAvatarSend.PayloadSize);

        var ok = CzRegisterAvatarSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Freya", packet.Id);
        Assert.Equal("Valkyrie", packet.AvatarName);
        WireTestKit.AssertDeepEqual(action, packet.Action);
    }
}
