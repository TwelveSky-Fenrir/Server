namespace Fenrir.Core.Abstractions;

/// <summary>
/// Contrat de base d'un paquet filaire : expose son octet d'opcode et sa taille de payload de façon
/// <b>statiquement liée</b> (membres statiques abstraits). Le dispatcher généré appelle ces membres sans réflexion.
/// </summary>
public interface IFenrirPacket
{
    public static abstract byte Opcode { get; }
    public static abstract int PayloadSize { get; }
}
