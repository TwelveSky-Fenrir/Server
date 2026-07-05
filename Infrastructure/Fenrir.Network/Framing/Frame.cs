using System.Buffers;
using Fenrir.Contracts.Wire;

namespace Fenrir.Network.Framing;

// Ref struct: only usable before an await; pull Payload out into a normal parameter first if dispatching async.
public readonly ref struct Frame(FenrirServer server, byte opcode, ReadOnlySequence<byte> payload)
{
    public FenrirServer Server { get; } = server;
    public byte Opcode { get; } = opcode;
    public ReadOnlySequence<byte> Payload { get; } = payload;
}
