using System;
using System.Collections.Immutable;

namespace Fenrir.Generators.Analyzers;

internal sealed class ForbiddenClosureEntry
{
    public ForbiddenClosureEntry(string assemblyName, string forbiddenAssembly, string reason)
    {
        AssemblyName = assemblyName;
        ForbiddenAssembly = forbiddenAssembly;
        Reason = reason;
    }

    public string AssemblyName { get; }

    public string ForbiddenAssembly { get; }

    public string Reason { get; }
}

internal static class ClosureRules
{
    private const string LeafReason =
        "ce projet est la FEUILLE du graphe : la grammaire du protocole ne doit dependre d'aucun autre " +
        "assembly Fenrir, sinon toute la solution finit dans la cloture de tout le monde";

    private const string CrossServerReason =
        "Login et Game sont deux protocoles distincts dont les opcodes se recouvrent ; un serveur qui " +
        "compile les paquets de l'autre peut en construire un par accident et le mettre sur le fil";

    private const string SliceReason =
        "l'autorite monde est mono-ecrivain et vit desormais DANS le processus GameServer ; le LoginServer " +
        "qui la verrait pourrait l'appeler en process et ecrire un etat dont il n'est pas proprietaire";

    private const string HostingReason =
        "Handlers et Services sont des couches de regles ; Hosting compose les singletons d'autorite et les " +
        "hotes de cycle de vie, il ne doit etre vu que par l'executable";

    public static readonly ImmutableArray<ForbiddenClosureEntry> Entries = ImmutableArray.Create(
        new ForbiddenClosureEntry("Fenrir.Core", "Fenrir.Protocol.Game", LeafReason),
        new ForbiddenClosureEntry("Fenrir.Core", "Fenrir.Protocol.Login", LeafReason),
        new ForbiddenClosureEntry("Fenrir.Core", "Fenrir.Network", LeafReason),
        new ForbiddenClosureEntry("Fenrir.Core", "Fenrir.Data.Abstractions", LeafReason),
        new ForbiddenClosureEntry("Fenrir.LoginServer", "Fenrir.Protocol.Game", CrossServerReason),
        new ForbiddenClosureEntry("Fenrir.LoginServer", "Fenrir.Application.Game", CrossServerReason),
        new ForbiddenClosureEntry("Fenrir.GameServer", "Fenrir.Protocol.Login", CrossServerReason),
        new ForbiddenClosureEntry("Fenrir.GameServer", "Fenrir.Application.Login", CrossServerReason),
        new ForbiddenClosureEntry("Fenrir.LoginServer", "Fenrir.Application.Game.Handlers", SliceReason),
        new ForbiddenClosureEntry("Fenrir.LoginServer", "Fenrir.Application.Game.Services", SliceReason),
        new ForbiddenClosureEntry("Fenrir.LoginServer", "Fenrir.Application.Game.Hosting", SliceReason),
        new ForbiddenClosureEntry("Fenrir.Application.Game.Handlers", "Fenrir.Application.Game.Hosting", HostingReason),
        new ForbiddenClosureEntry("Fenrir.Application.Game.Services", "Fenrir.Application.Game.Hosting", HostingReason),
        new ForbiddenClosureEntry("Fenrir.Application.Game", "Fenrir.Network", "les regles de jeu ne " +
                                                                               "connaissent pas le transport ; elles passent par IPacketSession et FrameWriter, dans Fenrir.Core"),
        new ForbiddenClosureEntry("Fenrir.Application.Game", "Fenrir.Data", "aucune couche applicative ne " +
                                                                            "voit un depot CONCRET ; les contrats vivent dans Fenrir.Data.Abstractions"),
        new ForbiddenClosureEntry("Fenrir.Application.Login", "Fenrir.Data", "aucune couche applicative ne " +
                                                                             "voit un depot CONCRET ; les contrats vivent dans Fenrir.Data.Abstractions"),
        new ForbiddenClosureEntry("Fenrir.Network", "Fenrir.Data", "la pile TCP ne compile pas contre un " +
                                                                   "ORM ; c'est ce qui lui evitait CaeriusNet, Microsoft.Data.SqlClient et Argon2"),
        new ForbiddenClosureEntry("Fenrir.Network", "Fenrir.Security", "l'admission de connexion vit dans " +
                                                                       "Fenrir.Network lui-meme ; cette arete etait l'unique cause de la fuite SQL"));

    public static ImmutableArray<ForbiddenClosureEntry> ForAssembly(string? assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
            return ImmutableArray<ForbiddenClosureEntry>.Empty;

        var builder = ImmutableArray.CreateBuilder<ForbiddenClosureEntry>();

        foreach (var entry in Entries)
            if (string.Equals(entry.AssemblyName, assemblyName, StringComparison.Ordinal))
                builder.Add(entry);

        return builder.ToImmutable();
    }
}
