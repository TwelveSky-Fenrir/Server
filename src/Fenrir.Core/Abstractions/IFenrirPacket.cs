namespace Fenrir.Core.Abstractions;

public interface IFenrirPacket
{
    public static abstract byte Opcode { get; }
    public static abstract int PayloadSize { get; }
}
