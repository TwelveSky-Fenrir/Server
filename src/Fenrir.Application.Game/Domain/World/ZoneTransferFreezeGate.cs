namespace Fenrir.Application.Game.Domain.World;

public static class ZoneTransferFreezeGate
{
    public static bool ShouldWithhold(bool isMovingZone, byte opcode, byte cancelOpcode, byte firstBypassOpcode,
        byte secondBypassOpcode)
    {
        return isMovingZone && opcode != cancelOpcode && opcode != firstBypassOpcode &&
               opcode != secondBypassOpcode;
    }
}
