namespace Fenrir.Generators.Analysis.Model;

/// <summary>
///     Property source declaration order is the binary wire layout; never reorder <c>TypeModel.Fields</c>
///     collections built from it.
/// </summary>
/// <remarks>
///     A <c>record</c> (not <c>class</c>), for the same structural-equality/incremental-caching reason as
///     <see cref="TypeModel" />. Its former <c>Symbol</c> (<see cref="Microsoft.CodeAnalysis.IPropertySymbol" />)
///     property was removed rather than kept: it had zero readers anywhere in either generator (verified by
///     repo-wide grep before deletion). Kept as a reference-type <c>record</c> rather than a
///     <c>readonly record struct</c>: <see cref="Offset" /> is genuinely mutated after construction
///     (<c>FieldScanner.ScanCore</c> builds a field, then assigns its cumulative <see cref="Offset" /> once the
///     running total is known), and reworking that two-phase build to fit a fully-immutable struct would touch
///     the offset-computation logic itself — exactly the code this repo treats as highest-risk for silently
///     desyncing the wire format, so it's left alone here rather than "cleaned up" as a side effect of this
///     caching fix.
/// </remarks>
internal sealed record FieldModel
{
    public required string PropertyName { get; init; }

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
