namespace Fenrir.Application.Game.Domain.World;

public static class ZoneTransferFreezeGate
{
    public static bool ShouldWithhold(bool isMovingZone, byte opcode, byte admittedOpcode)
    {
        return isMovingZone && opcode != admittedOpcode;
    }
}
