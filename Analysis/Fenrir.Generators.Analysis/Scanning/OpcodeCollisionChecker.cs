using System.Collections.Generic;
using System.Collections.Immutable;
using Fenrir.Generators.Analysis.Diagnostics;
using Fenrir.Generators.Analysis.Model;
using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analysis.Scanning;

/// <summary>FEN004 (spec §5.5): two <c>[FenrirPacket]</c>s cannot share (Server, Direction, Opcode).</summary>
internal static class OpcodeCollisionChecker
{
    public static (ImmutableArray<TypeModel> Deduplicated, ImmutableArray<Diagnostic> Diagnostics) Check(
        ImmutableArray<TypeModel> packets)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var seen = new Dictionary<(FenrirServer, FenrirDirection, byte), TypeModel>();
        var deduplicated = ImmutableArray.CreateBuilder<TypeModel>(packets.Length);

        foreach (var packet in packets)
        {
            var key = (packet.Server, packet.Direction, packet.Opcode);

            if (seen.TryGetValue(key, out var existing))
            {
                diagnostics.Add(Diagnostic.Create(
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
