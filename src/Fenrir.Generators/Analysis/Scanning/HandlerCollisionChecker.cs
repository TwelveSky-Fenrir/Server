using System.Collections.Generic;
using System.Collections.Immutable;
using Fenrir.Generators.Analysis.Diagnostics;
using Fenrir.Generators.Analysis.Model;

namespace Fenrir.Generators.Analysis.Scanning;

internal static class HandlerCollisionChecker
{
    public static (ImmutableArray<HandlerModel> Deduplicated, ImmutableArray<DiagnosticInfo> Diagnostics) Check(
        ImmutableArray<HandlerModel> handlers)
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var seen = new Dictionary<(FenrirServer, byte), HandlerModel>();
        var deduplicated = ImmutableArray.CreateBuilder<HandlerModel>(handlers.Length);

        foreach (var handler in handlers)
        {
            var key = (handler.Server, handler.Opcode);

            if (seen.TryGetValue(key, out var existing))
            {
                diagnostics.Add(DiagnosticInfo.Create(
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
