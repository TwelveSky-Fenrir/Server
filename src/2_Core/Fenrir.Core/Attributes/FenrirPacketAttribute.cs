using Fenrir.Core.Wire;

namespace Fenrir.Core.Attributes;

/// <summary>
/// Marque un <c>readonly partial record struct</c> comme paquet filaire porteur d'un opcode. L'ordre de
/// déclaration des champs <b>est</b> le layout filaire (aucun offset explicite ; protocole sans length-prefix).
/// Le générateur émet <c>TryRead</c>/<c>Write</c>/<c>PayloadSize</c> et enregistre l'opcode par (serveur, sens).
/// </summary>
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
