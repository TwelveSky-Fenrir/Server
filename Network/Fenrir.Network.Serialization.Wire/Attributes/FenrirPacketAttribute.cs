namespace Fenrir.Network.Serialization.Wire.Attributes;

/// <summary>
///     Marks a legacy wire packet; the generator emits TryRead/Write/PayloadSize/Opcode and registers it in
///     OpcodeRegistry.
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
    public WireObfuscationMode Obfuscation { get; init; } = WireObfuscationMode.None;

    /// <summary>ZPACKET + LZ4 envelope (zone outbound opcodes 12/13 only).</summary>
    public bool Compressed { get; init; }

    /// <summary>Expected total wire size (header included); generator emits FEN013 if field sizes diverge.</summary>
    public int ExpectedSize { get; init; } = -1;

    /// <summary>
    ///     Legal session states (<c>LoginSessionState</c>/<c>ZoneSessionState</c> per <see cref="Server" />,
    ///     each now declared in its own split project and out of reach from here); empty = any state.
    /// </summary>
    public byte[] AllowedStates { get; init; } = [];
}
