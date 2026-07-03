namespace Fenrir.Generators.Analysis.Model;

/// <summary>
///     Local mirror of <c>Fenrir.Contracts.Attributes.AvatarXorKind</c> — the analyzer can't reference the
///     assembly it analyzes, so it reads the raw value via <c>TypedConstant</c>. Names/order must stay in
///     sync with the source attribute.
/// </summary>
internal enum AvatarXorKind : byte
{
    None,
    Int,
    IntArray,
    Char,
    Char2
}
