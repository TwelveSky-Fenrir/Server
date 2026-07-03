using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Attributes;

/// <summary>
///     Marks a <c>readonly partial record struct</c> as a legacy wire packet. The generator
///     (Fenrir.Generators.Protocol) emits <c>TryRead</c>/<c>Write</c>/<c>PayloadSize</c>/<c>Opcode</c>
///     and registers the type in <c>OpcodeRegistry</c>/<c>SessionStateGate</c>.
/// </summary>
/// <param name="server">Executable that owns this opcode (values overlap between Login and Zone).</param>
/// <param name="direction">Packet direction; determines the header (9 bytes inbound / 1 byte outbound).</param>
/// <param name="opcode">Raw <c>tProtocol</c> value.</param>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class FenrirPacketAttribute(FenrirServer server, FenrirDirection direction, byte opcode) : Attribute
{
    public FenrirServer Server { get; } = server;
    public FenrirDirection Direction { get; } = direction;
    public byte Opcode { get; } = opcode;

    /// <summary>Whole-packet obfuscation applied by the send layer (none by default).</summary>
    public LegacyObfuscation Obfuscation { get; init; } = LegacyObfuscation.None;

    /// <summary>ZPACKET + LZ4 envelope (zone outbound opcodes 12/13 only). §3.5.</summary>
    public bool Compressed { get; init; }

    /// <summary>
    ///     Expected TOTAL wire size (header included), copied from the wire contract's <c>sizeof</c>
    ///     column; -1 = unchecked. Generator emits FEN-LEGACY-001 if declared field sizes + header
    ///     diverge — the byte-exact safety net.
    /// </summary>
    public int ExpectedSize { get; init; } = -1;

    /// <summary>
    ///     Session states in which this opcode is legal (<see cref="LoginSessionState" /> or
    ///     <see cref="ZoneSessionState" /> values per <see cref="Server" />, cast to <see cref="byte" />).
    ///     Empty array = legal in any state (handshake packets like op 0).
    /// </summary>
    public byte[] AllowedStates { get; init; } = [];
}
