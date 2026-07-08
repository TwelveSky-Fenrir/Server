namespace Fenrir.Network.Abstractions;

/// <summary>
///     Per-process, single-server opcode -&gt; frame-size lookup for INCOMING frames only (the only direction
///     <c>FrameReader</c> ever needs to size -- an outgoing packet's frame size always comes directly from
///     <c>TPacket.PayloadSize</c>, never from this lookup). Implemented by an adapter each protocol project's
///     generated <c>LoginOpcodeRegistry</c>/<c>ZoneOpcodeRegistry</c> exposes as its <c>Provider</c> field,
///     injected into <c>FrameReader</c>/<c>SessionLoop</c> instead of a hardcoded static call, so
///     <c>Fenrir.Network.Framing</c> and the generic parts of <c>Fenrir.Network.Dispatch</c> compile with zero
///     reference to either split Serialization project.
/// </summary>
public interface IOpcodeFrameSizeProvider
{
    bool TryGetFrameSize(byte opcode, out int frameSize);
}
