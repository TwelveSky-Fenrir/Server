# `src/` — Squelette de la solution cible (Fondations, Lot F1)

> Cette arborescence est le **squelette de la nouvelle architecture Fenrir**, posée au **Lot F1** du plan de
> réorganisation (voir `Roadmap/FONDATIONS/`). Elle **coexiste** avec la solution existante
> (`TwelveSky2.Fenrir.slnx` + `Application/`, `Network/`, `Infrastructure/`, …) qui **reste intacte et
> fonctionnelle** jusqu'au **Lot F6**, où les fichiers seront migrés ici et les anciens projets retirés.

## État actuel

- **Solution cible** : `Fenrir.slnx` (à la racine du dépôt) — **17 projets squelettes** compilables « à vide ».
- **Solution existante** : `TwelveSky2.Fenrir.slnx` — inchangée, porte encore tout le code de production.
- Chaque projet squelette contient un marqueur `_Skeleton.cs` (ou un `Program.cs`/`AppHost.cs` minimal pour les
  exécutables) documentant **ce qui y sera migré** et à **quel lot**. Aucun fichier de production n'a été déplacé.

## Structure (conforme à `Roadmap/FONDATIONS/02_Architecture_Cible_Solution.md`)

```
src/
  0_Orchestration/   Fenrir.AppHost (Aspire, plan de contrôle) · Fenrir.ServiceDefaults
  1_Generators/      Fenrir.Generators            (netstandard2.0 — fusion Protocol+Dispatch+Analysis, F10)
  2_Core/            Fenrir.Core (FEUILLE)        · Fenrir.Domain.Login · Fenrir.Domain.Game
  3_Infrastructure/  Fenrir.Network (unifié)      · Fenrir.Data (isolée) · Fenrir.Observability
                     Fenrir.Security             · Fenrir.Cluster
  4_Application/     Fenrir.Application.Login      · Fenrir.Application.Game
  5_Servers/         Fenrir.LoginServer · Fenrir.CenterServer · Fenrir.GameServer · Fenrir.Tools.DbMigrator
```

## Invariants du graphe (matrice `02` §5.2, vérifiables au compilateur)

- `Fenrir.Core` est une **feuille** : ne référence aucun autre projet Fenrir.
- `Fenrir.Data` est **isolée** : ne référence ni `Core`, ni `Network`, ni aucun `Application`.
- `Fenrir.LoginServer` ne référence **ni** `Application.Game` **ni** `Domain.Game` (un paquet Zone est
  physiquement absent de sa compilation) ; symétrique pour `Fenrir.GameServer` vs Login.
- Les générateurs (`Fenrir.Generators`, netstandard2.0) sont référencés en **`Analyzer`** par les projets qui
  déclarent des paquets (`Core`, `Application.Login`, `Application.Game`).

## Prochaines étapes (voir `Roadmap/FONDATIONS/10_Plan_de_Reorganisation.md`)

- **Lot F2** — Topologie TCP + Aspire : `AppHost` (SQL + migrator + Login + Center + N Zone, tout-TCP),
  boot en hosted-services, délégation par sessions.
- **Lot F3** — ZoneServer (★) : acteur mono-écrivain + dé/reconnexion par zone unifiée.
- **Lot F4** — CenterServer + `Cluster`. **Lot F5** — `Observability` + `Security`.
- **Lot F6** (phase suivante) — migration des ~1734 fichiers, redécoupage de `Game.Domain`, retrait des
  anciens projets.

## Critère de sortie du Lot F1

`dotnet build Fenrir.slnx` **vert** (`TreatWarningsAsErrors`), `dotnet publish` AOT OK sur les 4 exécutables,
graphe conforme à la matrice. *(Aucun test dans ce plan.)*
