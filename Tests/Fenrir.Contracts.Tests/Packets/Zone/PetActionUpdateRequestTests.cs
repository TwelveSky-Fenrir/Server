using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

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
