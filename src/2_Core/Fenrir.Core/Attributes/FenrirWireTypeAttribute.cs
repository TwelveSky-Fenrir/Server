namespace Fenrir.Core.Attributes;

/// <summary>
/// Marque un sous-struct embarqué (sans opcode) sérialisé par valeur dans un paquet. <c>expectedSize = -1</c>
/// = taille non contrainte ; une valeur positive fait échouer la génération en cas de divergence (FEN013).
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class FenrirWireTypeAttribute(int expectedSize = -1) : Attribute
{
    public int ExpectedSize { get; } = expectedSize;
}
