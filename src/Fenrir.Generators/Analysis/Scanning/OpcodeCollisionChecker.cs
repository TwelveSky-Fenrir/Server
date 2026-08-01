using System.Collections.Generic;
using System.Collections.Immutable;
using Fenrir.Generators.Analysis.Diagnostics;
using Fenrir.Generators.Analysis.Model;

namespace Fenrir.Generators.Analysis.Scanning;

internal static class OpcodeCollisionChecker
{
    public static (ImmutableArray<TypeModel> Deduplicated, ImmutableArray<DiagnosticInfo> Diagnostics) Check(
        ImmutableArray<TypeModel> packets)
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var seen = new Dictionary<(FenrirServer, FenrirDirection, byte), TypeModel>();
        var deduplicated = ImmutableArray.CreateBuilder<TypeModel>(packets.Length);

        foreach (var packet in packets)
        {
            var key = (packet.Server, packet.Direction, packet.Opcode);

            if (seen.TryGetValue(key, out var existing))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    FenrirDiagnostics.OpcodeCollision,
                    packet.Location,
                    existing.FullTypeName,
                    packet.FullTypeName,
                    packet.Server,
                    packet.Direction,
                    packet.Opcode));
                continue;
            }

            seen.Add(key, packet);
            deduplicated.Add(packet);
        }

        return (deduplicated.ToImmutable(), diagnostics.ToImmutable());
    }
}
