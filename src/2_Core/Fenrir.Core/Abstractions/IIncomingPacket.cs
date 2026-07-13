namespace Fenrir.Core.Abstractions;

/// <summary>
/// Paquet <b>entrant</b> : sait se lire depuis un payload déjà déchiffré/décompressé et <b>entièrement
/// matérialisé par valeur</b> (après <see cref="TryRead"/>, il ne référence plus le buffer réseau).
/// </summary>
public interface IIncomingPacket<TSelf> : IFenrirPacket
    where TSelf : struct, IIncomingPacket<TSelf>
{
    public static abstract bool TryRead(ReadOnlySpan<byte> source, out TSelf packet);
}
