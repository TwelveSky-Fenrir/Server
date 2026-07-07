using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analysis.Diagnostics;

// RS2008 requires AnalyzerReleases.Shipped/Unshipped.md tracking; not applicable, this isn't a shipped NuGet analyzer.
#pragma warning disable RS2008

/// <summary>Catalog of <c>FEN0xx</c> diagnostics for the legacy protocol generator.</summary>
internal static class FenrirDiagnostics
{
    private const string Category = "Fenrir.Protocol";

    public static readonly DiagnosticDescriptor NotReadonlyPartialRecordStruct = new(
        "FEN001",
        "Invalid packet type",
        "Type '{0}' carrying [FenrirPacket]/[FenrirWireType] must be declared 'readonly partial record struct'",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor UnsupportedFieldType = new(
        "FEN002",
        "Unsupported field type",
        "Field '{0}.{1}' has type '{2}', which is not supported by the wire mapping table (§1.3)",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor MissingSizeAttribute = new(
        "FEN003",
        "Missing size attribute",
        "Field '{0}.{1}' of type '{2}' requires {3}",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor OpcodeCollision = new(
        "FEN004",
        "Opcode collision",
        "Types '{0}' and '{1}' both declare (Server={2}, Direction={3}, Opcode={4})",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor ExpectedSizeMismatch = new(
        "FEN013",
        "Expected size mismatch",
        "Type '{0}' declares ExpectedSize={1} but the computed size (including header where applicable) is {2}",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor InvalidLength = new(
        "FEN014",
        "Invalid attribute length",
        "Field '{0}.{1}' carries {2} with a length <= 0",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor HandlerCollision = new(
        "FEN015",
        "Handler collision",
        "Handlers '{0}' and '{1}' both handle (Server={2}, Opcode={3})",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor AllowedStatesOnOutgoingPacket = new(
        "FEN016",
        "AllowedStates on outgoing packet",
        "Type '{0}' declares AllowedStates on an Outgoing [FenrirPacket]; SessionStateGate only gates Incoming " +
        "packets, so this value is silently ignored",
        Category,
        DiagnosticSeverity.Error,
        true);
}
