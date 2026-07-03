using System.Buffers;
using Fenrir.Contracts.Wire;

namespace Fenrir.Network.Framing;

/// <summary>
///     A decoded legacy frame. Ref struct: it may only be touched synchronously, before any <c>await</c> — callers
///     that need to dispatch asynchronously must first pull <see cref="Payload" /> (an owned-index
///     <see cref="ReadOnlySequence{T}" />, safe to hold past an await) out into a normal parameter.
/// </summary>
public readonly ref struct Frame(FenrirServer server, byte opcode, ReadOnlySequence<byte> payload)
{
    public FenrirServer Server { get; } = server;
    public byte Opcode { get; } = opcode;
    public ReadOnlySequence<byte> Payload { get; } = payload;
}
