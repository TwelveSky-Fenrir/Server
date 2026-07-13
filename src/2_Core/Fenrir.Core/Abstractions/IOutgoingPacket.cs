using Fenrir.Core.Wire;

namespace Fenrir.Core.Abstractions;

/// <summary>
/// Paquet <b>sortant</b> : connaît statiquement son mode d'obfuscation et sa compression, et sait s'écrire dans
/// un span (retourne le nombre d'octets écrits).
/// </summary>
public interface IOutgoingPacket : IFenrirPacket
{
    public static abstract WireObfuscationMode Obfuscation { get; }

    public static abstract bool Compressed { get; }

    public int Write(Span<byte> destination);
}
