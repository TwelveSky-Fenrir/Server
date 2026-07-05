namespace Fenrir.Network.Abstractions;

/// <summary><see cref="PayloadSize" /> excludes the header (9 bytes inbound, 1 byte outbound) — fields only.</summary>
public interface IFenrirPacket
{
    public static abstract byte Opcode { get; }
    public static abstract int PayloadSize { get; }
}
