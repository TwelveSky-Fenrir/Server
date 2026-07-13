using Fenrir.Core.Wire;

namespace Fenrir.Core.Attributes;

[AttributeUsage(AttributeTargets.Struct)]
public sealed class FenrirPacketAttribute(FenrirServer server, FenrirDirection direction, byte opcode) : Attribute
{
    public FenrirServer Server { get; } = server;
    public FenrirDirection Direction { get; } = direction;
    public byte Opcode { get; } = opcode;

    public WireObfuscationMode Obfuscation { get; init; } = WireObfuscationMode.None;

    public bool Compressed { get; init; }

    public int ExpectedSize { get; init; } = -1;

    public byte[] AllowedStates { get; init; } = [];
}
