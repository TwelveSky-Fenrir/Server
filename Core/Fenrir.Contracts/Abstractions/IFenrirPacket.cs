namespace Fenrir.Contracts.Abstractions;

/// <summary>
///     Wire packet carrying a legacy opcode. <see cref="PayloadSize" /> excludes the header (9-byte
///     <c>CLIENT_PACKET</c> inbound, 1-byte <c>DEFALUT_PACKET</c> outbound) — it's just the size of the
///     packet's own fields, read/written by <c>TryRead</c>/<c>Write</c>. Total wire size (legacy
///     <c>W_FUNCTION[opcode].SIZE</c>) is computed by the generated <c>OpcodeRegistry</c> from the
///     direction declared on <see cref="Attributes.FenrirPacketAttribute" />.
/// </summary>
public interface IFenrirPacket
{
    public static abstract byte Opcode { get; }
    public static abstract int PayloadSize { get; }
}
