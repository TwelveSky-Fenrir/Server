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

    /// <summary>
    /// FEN009 — un [FenrirPacket] sans ExpectedSize n'est vérifié par RIEN. TypeModelBuilder.BuildPacket ne
    /// compare la taille calculée que `if (expectedSize != -1)` : sans la valeur déclarée, FEN013 ne peut pas
    /// se déclencher. Le protocole n'ayant aucun préfixe de longueur, ExpectedSize est le seul garde-fou
    /// mécanique contre une désynchronisation silencieuse du flux d'octets de toute la session.
    /// </summary>
    public static readonly DiagnosticDescriptor PacketMissingExpectedSize = new(
        "FEN009",
        "Packet without an explicit ExpectedSize",
        // Pas de point à l'intérieur du format : RS1031/RS1032 exigent soit une phrase unique sans point final,
        // soit un texte multi-phrases terminé par un point — les 12 descripteurs existants suivent le premier cas.
        "Packet '{0}' declares no ExpectedSize, so nothing verifies its wire size, and the protocol carries no " +
        "length prefix, so a wrong field would silently desynchronise the whole session byte stream instead of " +
        "failing one packet; declare ExpectedSize = <total bytes, header included> and FEN013 will report the " +
        "computed total whenever the declared one disagrees",
        Category,
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// FEN104 — ferme le trou que FEN008 ne peut pas voir. FEN008 détecte PLUSIEURS serveurs dans UNE assembly ;
    /// FEN104 détecte UN MÊME serveur réparti sur DEUX assemblies. Ce second cas émet deux dispatchers concurrents
    /// (p. ex. deux ZoneMessageDispatcher), le build reste vert, et chacun n'aiguille que la moitié des opcodes :
    /// l'autre moitié tombe dans l'arme `default` et se perd dans un LogWarning à l'exécution.
    /// </summary>
    public static readonly DiagnosticDescriptor DispatchOwnershipConflict = new(
        "FEN104",
        "Dispatch ownership conflict",
        "Assembly '{0}' declares packet handlers for the '{1}' server, but referenced assembly '{2}' already " +
        "emits '{3}'; two dispatchers for one server each route only half the opcodes and the other half " +
        "silently falls through to the default arm at runtime",
        Category,
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// FEN111 — un handler doit dépendre d'Abstractions/, jamais d'une implémentation de Services/. La règle est
    /// pilotée par le CHEMIN du fichier : Domain/, Services/ et Handlers/ vivent dans la MÊME assembly, donc
    /// aucune analyse par assembly (ni ProjectReference, ni InternalsVisibleTo) ne peut les distinguer.
    /// </summary>
    public static readonly DiagnosticDescriptor HandlerDependsOnServiceImplementation = new(
        "FEN111",
        "Handler layer depends on a service implementation",
        // Même forme d'arguments que FEN112 ({0} fichier, {1} dossier source, {2} espace de noms importé) :
        // LayeringAnalyzer.AnalyzeUsing les remplit uniformément pour toutes les flèches.
        "'{0}' lives under '{1}/' and imports namespace '{2}', whose types are declared under 'Services/'; " +
        "a handler may only depend on 'Abstractions/' so the service implementation stays substitutable",
        Category,
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// FEN112 — Hosting/ est la racine de composition (DI, IOptions, hosted services) : tout le monde en dépend,
    /// il ne dépend de personne. Une flèche entrante depuis Abstractions/, Services/ ou Handlers/ inverse la
    /// couche et rend le graphe de dépendances circulaire au sens de l'architecture, sans que le compilateur
    /// n'ait rien à redire puisque tout est dans une seule assembly. Pilotée par CHEMIN, comme FEN111.
    /// </summary>
    public static readonly DiagnosticDescriptor LayerDependsOnHosting = new(
        "FEN112",
        "Layer depends on the hosting composition root",
        "'{0}' lives under '{1}/' and imports namespace '{2}', whose types are declared under 'Hosting/'; " +
        "'Hosting/' is the composition root and may only be depended upon, never depended from",
        Category,
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// FEN113 — LE garde-fou du garde-fou. Une règle pilotée par chemin qui ne matche plus aucun fichier passe de
    /// « 0 violation » à « 0 violation » : un `git mv Handlers/ Dispatchers/` décroche silencieusement le filet et
    /// personne ne le voit jamais. Ce diagnostic de fin de compilation transforme cette panne muette en échec de
    /// build. Il porte le tag CompilationEnd (RS1037) car il est reporté depuis RegisterCompilationEndAction.
    /// </summary>
    public static readonly DiagnosticDescriptor LayerRuleMatchedNothing = new(
        "FEN113",
        "Path-driven layer rule is no longer in force",
        "Path-driven layer rules for assembly '{0}' are no longer armed: {1}; restore the layout, or update " +
        "LayerRules in Fenrir.Generators to follow it",
        Category,
        DiagnosticSeverity.Error,
        true,
        // params string[] : en argument nommé il faut un tableau, pas un élément isolé.
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    /// <summary>
    /// FEN202 — un IInlinePacketHandler s'exécute SUR la boucle de session, avec un budget de quelques
    /// microsecondes, et bloquer là bloque toutes les autres connexions servies par cette boucle. La détection
    /// est SÉMANTIQUE et non textuelle : le dépôt contient des `result.Result` parfaitement légitimes qui sont
    /// une propriété de record de domaine, pas Task&lt;T&gt;.Result — un test textuel les signalerait à tort.
    /// </summary>
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
