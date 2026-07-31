using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analysis.Diagnostics;

#pragma warning disable RS2008

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

    public static readonly DiagnosticDescriptor WireTypeMissingExpectedSize = new(
        "FEN005",
        "Wire type without an explicit size",
        "Type '{0}' carries [FenrirWireType] without an explicit size; declare [FenrirWireType(<octets>)] " +
        "so the layout stays computable when the type is nested from another assembly",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor MultipleServersInCompilation = new(
        "FEN006",
        "Packets of several servers in one assembly",
        "Assembly '{0}' declares packets for several FenrirServer values ({1}); the emitted aggregates are " +
        "per-assembly and would silently cover only '{2}'",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor UnresolvableNestedSize = new(
        "FEN007",
        "Nested wire type size cannot be resolved",
        "Field '{1}.{2}' nests '{0}', which comes from another assembly's metadata and declares no explicit " +
        "[FenrirWireType(<octets>)]; its size cannot be computed and would silently be 0",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor MultipleHandlerServersInCompilation = new(
        "FEN008",
        "Handlers of several servers in one assembly",
        "Assembly '{0}' declares packet handlers for several FenrirServer values ({1}); a single dispatcher " +
        "is emitted per assembly, named after the first handler collected ('{2}' here), an order that is not " +
        "guaranteed stable across compilations",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor PacketMissingExpectedSize = new(
        "FEN009",
        "Packet without an explicit ExpectedSize",
        "Packet '{0}' declares no ExpectedSize, so nothing verifies its wire size, and the protocol carries no " +
        "length prefix, so a wrong field would silently desynchronise the whole session byte stream instead of " +
        "failing one packet; declare ExpectedSize = <total bytes, header included> and FEN013 will report the " +
        "computed total whenever the declared one disagrees",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor DispatchOwnershipConflict = new(
        "FEN104",
        "Dispatch ownership conflict",
        "Assembly '{0}' declares packet handlers for the '{1}' server, but referenced assembly '{2}' already " +
        "emits '{3}'; two dispatchers for one server each route only half the opcodes and the other half " +
        "silently falls through to the default arm at runtime",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor HandlerDependsOnServiceImplementation = new(
        "FEN111",
        "Handler layer depends on a service implementation",
        "'{0}' lives under '{1}/' and imports namespace '{2}', whose types are declared under 'Services/'; " +
        "a handler may only depend on 'Abstractions/' so the service implementation stays substitutable",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor LayerDependsOnHosting = new(
        "FEN112",
        "Layer depends on the hosting composition root",
        "'{0}' lives under '{1}/' and imports namespace '{2}', whose types are declared under 'Hosting/'; " +
        "'Hosting/' is the composition root and may only be depended upon, never depended from",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor LayerRuleMatchedNothing = new(
        "FEN113",
        "Path-driven layer rule is no longer in force",
        "Path-driven layer rules for assembly '{0}' are no longer armed: {1}; restore the layout, or update " +
        "LayerRules in Fenrir.Generators to follow it",
        Category,
        DiagnosticSeverity.Error,
        true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public static readonly DiagnosticDescriptor ForbiddenNamespaceImport = new(
        "FEN114",
        "Layer imports a forbidden assembly's namespace",
        "'{0}' lives under '{1}/' and imports '{2}'; {3}",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor ForbiddenImportRuleMatchedNothing = new(
        "FEN110",
        "Forbidden-import rule is no longer in force",
        "Forbidden-import rules for assembly '{0}' are no longer armed: {1}; restore the layout, or update " +
        "ForbiddenImportRules in Fenrir.Generators to follow it",
        Category,
        DiagnosticSeverity.Error,
        true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public static readonly DiagnosticDescriptor ForbiddenAssemblyInClosure = new(
        "FEN115",
        "Forbidden assembly in the compile-time closure",
        "'{0}' resolves '{1}' in its transitive reference closure; {2}",
        Category,
        DiagnosticSeverity.Error,
        true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public static readonly DiagnosticDescriptor AsynchronyInInlineHandler = new(
        "FEN202",
        "Asynchrony in an inline packet handler",
        "'{0}' implements IInlinePacketHandler and runs on the session loop with a microsecond budget, but {1}; " +
        "implement IAsyncPacketHandler instead",
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
