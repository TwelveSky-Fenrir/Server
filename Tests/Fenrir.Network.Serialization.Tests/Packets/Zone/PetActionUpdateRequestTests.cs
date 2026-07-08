using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzUpdatePetActionSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(104, PetActionUpdateRequest.PayloadSize);
        Assert.Equal(ActionInfo.WireSize, PetActionUpdateRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.PetActionUpdate, PetActionUpdateRequest.Opcode);
    }
}
