using Fenrir.Network.Abstractions;

namespace Fenrir.Network.Framing;

// Raised by FrameDecoder for an unregistered opcode; always fatal for the session, never recoverable.
public sealed class ProtocolViolationException(FenrirServer server, byte opcode)
    : Exception($"Unknown legacy opcode {opcode} for server {server}.")
{
    public FenrirServer Server { get; } = server;
    public byte Opcode { get; } = opcode;
}
