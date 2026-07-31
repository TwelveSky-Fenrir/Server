using System;
using System.Collections.Immutable;
using System.Linq;
using Fenrir.Generators.Analysis.Diagnostics;
using Fenrir.Generators.Analysis.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Fenrir.Generators.Analyzers;

/// <summary>
/// FEN202 — interdit toute asynchronie dans le <c>Handle</c> d'un <c>IInlinePacketHandler&lt;T&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Un handler inline s'exécute SUR la boucle de session, en synchrone, avec un budget de quelques
/// microsecondes : c'est toute la raison d'être du couple <c>IInlinePacketHandler</c> /
/// <c>IAsyncPacketHandler</c>. Y bloquer sur une tâche (<c>.Result</c>, <c>.Wait()</c>,
/// <c>GetAwaiter().GetResult()</c>) fige la boucle, donc toutes les connexions qu'elle sert ; y lancer une
/// tâche non attendue fait fuir l'exception et perd l'ordre relatif des paquets, que le protocole suppose
/// pourtant total par session.
/// </para>
/// <para>
/// La détection est SÉMANTIQUE, jamais textuelle. Le dépôt contient aujourd'hui quatre <c>result.Result</c>
/// parfaitement légitimes (une propriété de record de domaine, p. ex. <c>MentorStatusResult.Result</c>) :
/// une recherche textuelle de « .Result » les signalerait à tort et casserait le build de tout le monde sous
/// <c>TreatWarningsAsErrors</c>. On compare donc le type DÉCLARANT du membre à <c>Task&lt;T&gt;</c> /
/// <c>ValueTask&lt;T&gt;</c>.
/// </para>
/// <para>
/// Le corps n'est inspecté que via <c>RegisterOperationBlockStartAction</c> : le filtrage a lieu AVANT que la
/// moindre action d'opération ne soit enregistrée, de sorte que Roslyn ne construit l'arbre d'IOperation que
/// pour la cinquantaine de <c>Handle</c> concernés, et non pour chacun des milliers de corps de méthode des
/// sept projets qui chargent cet analyseur.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InlineHandlerAsynchronyAnalyzer : DiagnosticAnalyzer
{
    private const string HandleMethodName = "Handle";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(FenrirDiagnostics.AsynchronyInInlineHandler);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var inlineHandlerInterface = context.Compilation.GetTypeByMetadataName(WellKnownNames.IInlinePacketHandler);

        // Compilation qui ne référence pas Fenrir.Core : aucun handler inline possible, rien à surveiller.
        if (inlineHandlerInterface is null)
            return;

        var awaitables = TaskLikeTypes.From(context.Compilation);

        // `async` se lit sur la signature : inutile d'ouvrir le corps pour ce cas-là.
        context.RegisterSymbolAction(
            symbolContext => AnalyzeSignature(symbolContext, inlineHandlerInterface),
            SymbolKind.Method);

        context.RegisterOperationBlockStartAction(blockStartContext =>
        {
            if (blockStartContext.OwningSymbol is not IMethodSymbol method)
                return;

            if (!IsInlineHandle(method, inlineHandlerInterface))
                return;

            // Déjà signalé par AnalyzeSignature ; chaque `await` du corps ne serait que du bruit en plus.
            if (method.IsAsync)
                return;

            var handlerName = HandlerNameOf(method);

            blockStartContext.RegisterOperationAction(
                operationContext => Inspect(operationContext, awaitables, handlerName),
                OperationKind.Await,
                OperationKind.Invocation,
                OperationKind.PropertyReference,
                OperationKind.ExpressionStatement);
        });
    }

    private static void AnalyzeSignature(SymbolAnalysisContext context, INamedTypeSymbol inlineHandlerInterface)
    {
        if (context.Symbol is not IMethodSymbol method || !method.IsAsync)
            return;

        if (!IsInlineHandle(method, inlineHandlerInterface))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            FenrirDiagnostics.AsynchronyInInlineHandler,
            method.Locations.FirstOrDefault(),
            HandlerNameOf(method),
            "it is declared 'async', so it hands control back to the session loop at the first await and lets " +
            "the next packet of the same session interleave halfway through"));
    }

    private static void Inspect(OperationAnalysisContext context, TaskLikeTypes awaitables, string handlerName)
    {
        switch (context.Operation)
        {
            // `await ...` dans une lambda ou une fonction locale du corps du handler.
            case IAwaitOperation:
                Report(context, handlerName, "it awaits inside the handler body");
                break;

            case IInvocationOperation invocation:
                InspectInvocation(context, invocation, awaitables, handlerName);
                break;

            // `task.Result` — reconnu sur le type DÉCLARANT du membre, pas sur son nom.
            case IPropertyReferenceOperation { Property.Name: "Result" } property
                when awaitables.IsTaskLike(property.Property.ContainingType):
                Report(context, handlerName, "it blocks the session loop on 'Task.Result'");
                break;

            // `SomethingAsync();` en instruction : la tâche part sans que personne ne l'attende.
            case IExpressionStatementOperation { Operation: IInvocationOperation fireAndForget }
                when awaitables.IsTaskLike(fireAndForget.Type):
                Report(context, handlerName,
                    "it starts a task and never awaits it, so its exceptions are lost and per-session packet " +
                    "ordering is no longer guaranteed");
                break;

            // `_ = SomethingAsync();` — même problème, simplement rendu explicite par un discard.
            case IExpressionStatementOperation
            {
                Operation: ISimpleAssignmentOperation { Target: IDiscardOperation } discarded
            } when awaitables.IsTaskLike(discarded.Value.Type):
                Report(context, handlerName,
                    "it discards a task without awaiting it, so its exceptions are lost and per-session packet " +
                    "ordering is no longer guaranteed");
                break;
        }
    }

    private static void InspectInvocation(
        OperationAnalysisContext context,
        IInvocationOperation invocation,
        TaskLikeTypes awaitables,
        string handlerName)
    {
        var target = invocation.TargetMethod;
        var declaringType = target.ContainingType;

        switch (target.Name)
        {
            case "Wait" when awaitables.IsTaskLike(declaringType):
            case "RunSynchronously" when awaitables.IsTaskLike(declaringType):
                Report(context, handlerName, "it blocks the session loop on 'Task.Wait()'");
                break;

            case "GetResult" when IsAwaiter(declaringType):
                Report(context, handlerName, "it blocks the session loop on 'GetAwaiter().GetResult()'");
                break;

            case "Run" when awaitables.IsTask(declaringType):
            case "StartNew" when SymbolNameHelpers.Is(declaringType, "System.Threading.Tasks.TaskFactory"):
                Report(context, handlerName,
                    "it offloads work to the thread pool, which leaves the packet half-handled the moment the " +
                    "handler returns to the session loop");
                break;
        }
    }

    /// <summary>TaskAwaiter, ValueTaskAwaiter, ConfiguredTaskAwaitable+Awaiter : tous suffixés « Awaiter ».</summary>
    private static bool IsAwaiter(INamedTypeSymbol? type)
    {
        return type is not null &&
               type.Name.EndsWith("Awaiter", StringComparison.Ordinal) &&
               SymbolNameHelpers.GetFullNamespace(type.ContainingNamespace) == "System.Runtime.CompilerServices";
    }

    /// <summary>Vrai si <paramref name="method" /> implémente <c>IInlinePacketHandler&lt;T&gt;.Handle</c>.</summary>
    private static bool IsInlineHandle(IMethodSymbol method, INamedTypeSymbol inlineHandlerInterface)
    {
        // Préfiltre très bon marché. EndsWith et non == : une implémentation EXPLICITE porte un nom mangé
        // (« Fenrir.Core.Abstractions.IInlinePacketHandler<T>.Handle ») qu'une égalité stricte laisserait fuir.
        if (!method.Name.EndsWith(HandleMethodName, StringComparison.Ordinal))
            return false;

        var containingType = method.ContainingType;
        if (containingType is null)
            return false;

        foreach (var candidateInterface in containingType.AllInterfaces)
        {
            if (!SymbolEqualityComparer.Default.Equals(candidateInterface.OriginalDefinition, inlineHandlerInterface))
                continue;

            foreach (var member in candidateInterface.GetMembers(HandleMethodName))
                if (SymbolEqualityComparer.Default.Equals(
                        containingType.FindImplementationForInterfaceMember(member), method))
                    return true;
        }

        return false;
    }

    private static string HandlerNameOf(IMethodSymbol method)
    {
        // ISymbol.ContainingType est annoté nullable ; IsInlineHandle a déjà écarté le cas, mais le compilateur
        // ne le sait pas et Nullable + TreatWarningsAsErrors en ferait une erreur de build.
        return method.ContainingType?.Name ?? method.Name;
    }

    private static void Report(OperationAnalysisContext context, string handlerName, string reason)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            FenrirDiagnostics.AsynchronyInInlineHandler,
            context.Operation.Syntax.GetLocation(),
            handlerName,
            reason));
    }

    /// <summary>Les quatre types « task-like » du BCL, résolus une seule fois par compilation.</summary>
    private readonly struct TaskLikeTypes
    {
        private readonly INamedTypeSymbol? _task;
        private readonly INamedTypeSymbol? _taskOfT;
        private readonly INamedTypeSymbol? _valueTask;
        private readonly INamedTypeSymbol? _valueTaskOfT;

        private TaskLikeTypes(
            INamedTypeSymbol? task,
            INamedTypeSymbol? taskOfT,
            INamedTypeSymbol? valueTask,
            INamedTypeSymbol? valueTaskOfT)
        {
            _task = task;
            _taskOfT = taskOfT;
            _valueTask = valueTask;
            _valueTaskOfT = valueTaskOfT;
        }

        public static TaskLikeTypes From(Compilation compilation)
        {
            return new TaskLikeTypes(
                compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"),
                compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1"),
                compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask"),
                compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1"));
        }

        public bool IsTask(ITypeSymbol? type)
        {
            return Matches(type, _task);
        }

        public bool IsTaskLike(ITypeSymbol? type)
        {
            return Matches(type, _task) || Matches(type, _taskOfT) ||
                   Matches(type, _valueTask) || Matches(type, _valueTaskOfT);
        }

        private static bool Matches(ITypeSymbol? type, INamedTypeSymbol? wellKnown)
        {
            return type is not null && wellKnown is not null &&
                   SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, wellKnown);
        }
    }
}
