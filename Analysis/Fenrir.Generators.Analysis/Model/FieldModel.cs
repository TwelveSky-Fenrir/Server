using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analysis.Model;

/// <summary>Property source declaration order is the binary wire layout; never reorder <see cref="Fields"/> collections built from it.</summary>
internal sealed class FieldModel
{
    public required string PropertyName { get; init; }

    public required IPropertySymbol Symbol { get; init; }

    public required FieldShape Shape { get; init; }

    public int ReservedBefore { get; init; }

    public int ElementCount { get; init; }

    public int StringLength { get; init; }

    public string? NestedTypeFullName { get; init; }

    public int NestedSize { get; init; }

    /// <summary>Only <c>LC_LOGIN_RECV.Id</c> carries this.</summary>
    public bool IsLegacyUidField { get; init; }

    /// <summary>Only <c>AvatarRosterResponse</c> uses this.</summary>
    public AvatarXorKind AvatarXor { get; init; } = AvatarXorKind.None;

    public int AvatarXorRowLength { get; init; }

    public int Offset { get; set; }

    public int OwnSize { get; init; }
}
