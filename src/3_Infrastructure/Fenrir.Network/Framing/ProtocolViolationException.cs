namespace Fenrir.Network.Framing;

public sealed class ProtocolViolationException(FenrirServer server, byte opcode)
    : Exception($"Unknown legacy opcode {opcode} for server {server}.")
{
    public FenrirServer Server { get; } = server;
    public byte Opcode { get; } = opcode;
}
