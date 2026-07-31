using System;
using System.Collections.Immutable;
using System.Linq;
using Fenrir.Generators.Analysis.Diagnostics;
using Fenrir.Generators.Analysis.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Fenrir.Generators.Analyzers;

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

        if (inlineHandlerInterface is null)
            return;

        var awaitables = TaskLikeTypes.From(context.Compilation);

        context.RegisterSymbolAction(
            symbolContext => AnalyzeSignature(symbolContext, inlineHandlerInterface),
            SymbolKind.Method);

        context.RegisterOperationBlockStartAction(blockStartContext =>
        {
            if (blockStartContext.OwningSymbol is not IMethodSymbol method)
                return;

            if (!IsInlineHandle(method, inlineHandlerInterface))
                return;

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
            case IAwaitOperation:
                Report(context, handlerName, "it awaits inside the handler body");
                break;

            case IInvocationOperation invocation:
                InspectInvocation(context, invocation, awaitables, handlerName);
                break;

            case IPropertyReferenceOperation { Property.Name: "Result" } property
                when awaitables.IsTaskLike(property.Property.ContainingType):
                Report(context, handlerName, "it blocks the session loop on 'Task.Result'");
                break;

            case IExpressionStatementOperation { Operation: IInvocationOperation fireAndForget }
                when awaitables.IsTaskLike(fireAndForget.Type):
                Report(context, handlerName,
                    "it starts a task and never awaits it, so its exceptions are lost and per-session packet " +
                    "ordering is no longer guaranteed");
                break;

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

        private static bool IsAwaiter(INamedTypeSymbol? type)
    {
        return type is not null &&
               type.Name.EndsWith("Awaiter", StringComparison.Ordinal) &&
               SymbolNameHelpers.GetFullNamespace(type.ContainingNamespace) == "System.Runtime.CompilerServices";
    }

        private static bool IsInlineHandle(IMethodSymbol method, INamedTypeSymbol inlineHandlerInterface)
    {
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
