namespace Fenrir.Generators.Analysis.Model;

internal sealed record FieldModel
{
    public required string PropertyName { get; init; }

    public required FieldShape Shape { get; init; }

    public int ReservedBefore { get; init; }

    public int ElementCount { get; init; }

    public int StringLength { get; init; }

    public string? NestedTypeFullName { get; init; }

    public int NestedSize { get; init; }

    public bool IsLegacyUidField { get; init; }

    public AvatarXorKind AvatarXor { get; init; } = AvatarXorKind.None;

    public int AvatarXorRowLength { get; init; }

    public int OwnSize { get; init; }
}
