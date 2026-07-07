using System.Collections.Generic;
using System.Collections.Immutable;
using Fenrir.Generators.Analysis.Diagnostics;
using Fenrir.Generators.Analysis.Model;
using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analysis.Scanning;

/// <summary>
///     FEN015: two packet handlers cannot both claim the same (Server, Opcode). Callers dedupe inline and
///     async handlers into separate buckets before calling <see cref="Check" />, so this only ever sees one
///     <c>IsAsync</c> bucket at a time — mirrors <see cref="OpcodeCollisionChecker.Check" />.
/// </summary>
internal static class HandlerCollisionChecker
{
    public static (ImmutableArray<HandlerModel> Deduplicated, ImmutableArray<Diagnostic> Diagnostics) Check(
        ImmutableArray<HandlerModel> handlers)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var seen = new Dictionary<(FenrirServer, byte), HandlerModel>();
        var deduplicated = ImmutableArray.CreateBuilder<HandlerModel>(handlers.Length);

        foreach (var handler in handlers)
        {
            var key = (handler.Server, handler.Opcode);

            if (seen.TryGetValue(key, out var existing))
            {
                diagnostics.Add(Diagnostic.Create(
                    FenrirDiagnostics.HandlerCollision,
                    handler.Location,
                    existing.HandlerTypeFullName,
                    handler.HandlerTypeFullName,
                    handler.Server,
                    handler.Opcode));
                continue;
            }

            seen.Add(key, handler);
            deduplicated.Add(handler);
        }

        return (deduplicated.ToImmutable(), diagnostics.ToImmutable());
    }
}
