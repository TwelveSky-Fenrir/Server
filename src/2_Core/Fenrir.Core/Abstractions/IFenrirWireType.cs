namespace Fenrir.Core.Abstractions;

/// <summary>
/// Sous-struct filaire embarqué (sans opcode) sérialisé par valeur dans un paquet (ex. <c>AvatarInfo</c>).
/// Implémenté par les 51 wire-types Shared ; <see cref="WireSize"/> est la taille fixe embarquée.
/// </summary>
public interface IFenrirWireType<TSelf>
    where TSelf : struct, IFenrirWireType<TSelf>
{
    public static abstract int WireSize { get; }

    public static abstract bool TryRead(ReadOnlySpan<byte> source, out TSelf value);

    public int Write(Span<byte> destination);
}
