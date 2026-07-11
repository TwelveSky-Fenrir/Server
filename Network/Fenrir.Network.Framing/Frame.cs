using System.Buffers;
using Fenrir.Network.Abstractions;

namespace Fenrir.Network.Framing;

public readonly ref struct Frame(FenrirServer server, byte opcode, ReadOnlySequence<byte> payload)
{
    public FenrirServer Server { get; } = server;
    public byte Opcode { get; } = opcode;
    public ReadOnlySequence<byte> Payload { get; } = payload;
}
