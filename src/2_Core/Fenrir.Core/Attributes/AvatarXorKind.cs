namespace Fenrir.Core.Attributes;

/// <summary>
/// Variante de XOR champ-par-champ appliquée à l'aperçu avatar (login→client). Les ordinaux sont
/// <b>load-bearing</b> : le générateur lit l'argument d'attribut positionnellement en entier, donc toute
/// dérive de valeur désynchronise silencieusement le chiffrement (voir le manifeste F6 T1, R2).
/// </summary>
public enum AvatarXorKind : byte
{
    None = 0,
    Int = 1,
    IntArray = 2,
    Char = 3,
    Char2 = 4
}
