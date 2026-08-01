# Fenrir — Architecture Technique de Référence (2026)

> **Version :** 1.0 — Document de référence (remplace et absorbe *Foundation v0.1* et *Architecture_Fenrir_2026*)
> **Stack :** C# 14 · .NET 10 **Native AOT** · Aspire 13 · SQL Server 2025 (17.x) · **CaeriusNet** · `System.IO.Pipelines` · TCP
> **Vérifié contre :** documentation officielle CaeriusNet (caerius.net, API `ICaeriusNetDbContext`), documentation Microsoft SQL Server 2025 (Optimized Locking, JSON natif, In-Memory OLTP).

---

## Table des matières

1. [Invariants d'architecture](#1-invariants-darchitecture)
2. [Topologie & Orchestration Aspire](#2-topologie--orchestration-aspire)
3. [Arborescence de la solution](#3-arborescence-de-la-solution)
4. [Le protocole filaire Fenrir](#4-le-protocole-filaire-fenrir)
5. [Couche réseau `System.IO.Pipelines`](#5-couche-réseau-systemiopipelines)
6. [Le système de messages : Factory, Dispatcher, Handlers](#6-le-système-de-messages)
7. [Les Source Generators Fenrir](#7-les-source-generators-fenrir)
8. [Sessions, Handover & Cryptographie](#8-sessions-handover--cryptographie)
9. [LoginServer](#9-loginserver)
10. [GameServer : tick, zones, persistance](#10-gameserver)
11. [Couche Data : CaeriusNet](#11-couche-data--caeriusnet)
12. [Base de données Data-First — SQL Server 2025](#12-base-de-données-data-first--sql-server-2025)
13. [Observabilité](#13-observabilité)
14. [Tests & Benchmarks](#14-tests--benchmarks)
15. [Règles d'or consolidées](#15-règles-dor-consolidées)
- [Annexe A — Budget de latence d'un paquet](#annexe-a--budget-de-latence-dun-paquet)
- [Annexe B — Dimensionnement mémoire par session](#annexe-b--dimensionnement-mémoire-par-session)

---

## 1. Invariants d'architecture

Ces dix invariants sont **non négociables**. Toute Pull Request qui en viole un est refusée. Ils sont vérifiables mécaniquement (analyzers Roslyn, benchmarks CI, revues de plans SQL) — un principe qu'on ne peut pas vérifier n'est pas un principe, c'est un vœu.

| # | Invariant | Vérification mécanique |
| :-- | :-- | :-- |
| I-01 | **Zéro réflexion runtime.** Le binaire est publié `PublishAot=true` ; tout `Type.GetType`, `Activator`, `MakeGenericType` est un échec de build. | Warnings AOT traités en erreurs (`TreatWarningsAsErrors` + `IsAotCompatible`) |
| I-02 | **Zéro allocation heap sur le hot path** (réception → parse → dispatch → handler inline → envoi). | BenchmarkDotNet `MemoryDiagnoser` en gate CI : `0 B/op` exigé |
| I-03 | **Zéro SQL inline.** Le C# ne connaît que des noms de procédures. | Analyzer interdisant `CommandType.Text` ; CaeriusNet n'expose de toute façon que des procédures |
| I-04 | **La base de données est le contrat de vérité** (Data-First). Les DTO C# se conforment aux result sets, jamais l'inverse. | Tests d'intégration : chaque procédure exécutée contre chaque DTO `[GenerateDto]` |
| I-05 | **Tout le code répétitif est généré** (sérialisation, dispatch, registres, mappers). Un humain n'écrit jamais deux fois le même `switch` d'opcodes. | Les fichiers générés sont en `obj/`, jamais commités, jamais édités |
| I-06 | **Le protocole est versionné.** Chaque paquet porte sa version ; un opcode ne change jamais de sémantique, il est déprécié et remplacé. | Golden tests binaires (snapshots d'octets) en CI |
| I-07 | **Les couches s'ignorent.** Le Domain ignore le réseau et SQL ; les handlers réseau ignorent SQL ; les repositories ignorent le protocole. | Matrice de dépendances projet (§3.3) verrouillée par `Directory.Build.targets` |
| I-08 | **Le GameServer est autoritaire.** Le client est une télécommande qui émet des *intentions* ; le serveur calcule la vérité. | Revue systématique des handlers : aucune donnée client n'est écrite sans validation Domain |
| I-09 | **Chaque allocation est justifiée** par un commentaire `// ALLOC:` expliquant pourquoi elle est hors hot path ou amortie. | Grep CI + revue |
| I-10 | **Toute décision structurante est un ADR** (Architecture Decision Record) dans `docs/adr/`. | Revue |

Le reste de ce document est l'application concrète de ces invariants, sous-système par sous-système. Les choix qui divergent des brouillons précédents sont signalés par des encadrés **`Décision`** avec leur justification — c'est là que se trouve la « réflexion 2026 » demandée.

---

## 2. Topologie & Orchestration Aspire

### 2.1 Rôle exact d'Aspire dans Fenrir

Aspire (v13+, `aspire` CLI) remplit **trois rôles et seulement trois** :

1. **Topologie déclarative** : qui existe, qui dépend de qui, qui démarre avant qui, quelles variables d'environnement et chaînes de connexion sont injectées où.
2. **Boucle de développement** : `aspire run` monte SQL Server 2025 en conteneur (volume persistant), exécute le migrateur, lance LoginServer et les shards GameServer, et ouvre le dashboard (traces, logs, métriques, ressources).
3. **Génération des manifestes de déploiement** (`aspire publish`) vers Compose/Kubernetes.

Aspire **n'est pas** sur le chemin des paquets : en production, un client TCP parle directement au socket du GameServer. Aucune requête ne traverse un composant Aspire. C'est un plan de contrôle, jamais un plan de données.

### 2.2 AppHost — topologie complète

```csharp
// src/0_Orchestration/Fenrir.AppHost/AppHost.cs
var builder = DistributedApplication.CreateBuilder(args);

// ── SQL Server 2025 ─────────────────────────────────────────────────────────
var sqlPassword = builder.AddParameter("sql-password", secret: true);

var sql = builder.AddSqlServer("sqlserver", sqlPassword)
    .WithImageTag("2025-latest")
    .WithDataVolume("fenrir-sql-data")            // les données survivent aux redémarrages
    .WithLifetime(ContainerLifetime.Persistent);   // le conteneur survit à l'AppHost (dev-loop rapide)

var fenrirDb = sql.AddDatabase("FenrirDb");

// ── Migrateur Data-First (s'exécute puis se termine) ────────────────────────
var migrator = builder.AddProject<Projects.Fenrir_Tools_DbMigrator>("db-migrator")
    .WithReference(fenrirDb)
    .WaitFor(fenrirDb);

// ── LoginServer ─────────────────────────────────────────────────────────────
var loginServer = builder.AddProject<Projects.Fenrir_LoginServer>("login-server")
    .WithReference(fenrirDb)
    .WaitForCompletion(migrator)                   // jamais de serveur sans schéma à jour
    .WithEndpoint(name: "login-tcp", scheme: "tcp", port: 42000, targetPort: 42000)
    .WithEnvironment("FENRIR__REALM", "Midgard");

// ── GameServers : un shard = une ressource nommée, un port explicite ────────
// (pas de WithReplicas : un endpoint TCP brut exige un port stable et publié,
//  et chaque shard possède un identifiant métier — voir Décision D-01)
for (byte shardId = 1; shardId <= 2; shardId++)
{
    builder.AddProject<Projects.Fenrir_GameServer>($"game-shard-{shardId:00}")
        .WithReference(fenrirDb)
        .WaitForCompletion(migrator)
        .WithEndpoint(name: "game-tcp", scheme: "tcp",
                      port: 42100 + shardId, targetPort: 42100 + shardId)
        .WithEnvironment("FENRIR__SHARD_ID", shardId.ToString())
        .WithEnvironment("FENRIR__TICK_HZ", "20");
}

builder.Build().Run();
```

> **Décision D-01 — Un shard = une ressource, pas des réplicas.**
> `WithReplicas(n)` est pensé pour des services HTTP sans état derrière un proxy. Un shard MMO est *stateful* (il possède des zones, des joueurs, un identifiant), écoute un port TCP publié fixe, et est adressé nominativement par le LoginServer lors du handover. La boucle `for` dans l'AppHost rend la flotte explicite, versionnée et diff-able.

### 2.3 ServiceDefaults

`Fenrir.ServiceDefaults` est référencé par les trois exécutables et configure, en une méthode d'extension `AddFenrirDefaults()` :

- **OpenTelemetry** : exporteur OTLP (dashboard Aspire en dev, collecteur en prod), `Meter("Fenrir.Network")`, `Meter("Fenrir.Game")`, `ActivitySource("Fenrir")` — avec la règle d'échantillonnage du §13 (jamais d'`Activity` par paquet).
- **HealthChecks** : `/health` (liveness) et `/alive` (readiness) exposés sur un **endpoint HTTP interne séparé** du port TCP de jeu — le port de jeu ne parle que le protocole Fenrir, jamais HTTP.
- **Resilience** : `Microsoft.Extensions.Resilience` sur les appels sortants (uniquement SQL ici — retries idempotents seulement, voir §11.6).
- **Configuration typée** : `FenrirServerOptions` lié à la section `FENRIR__*`, validé au démarrage (`ValidateOnStart`), incompatibilités AOT évitées via le binder source-généré (`Microsoft.Extensions.Configuration.Binder` avec `EnableConfigurationBindingGenerator=true`).

### 2.4 Contraintes AOT au niveau solution

`Directory.Build.props` à la racine impose le socle à tous les projets :

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IsAotCompatible>true</IsAotCompatible>            <!-- analyzers trimming+AOT sur les libs -->
    <InvariantGlobalization>true</InvariantGlobalization>
    <ServerGarbageCollection>true</ServerGarbageCollection>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <!-- Exécutables uniquement (LoginServer, GameServer, DbMigrator) -->
  <PropertyGroup Condition="'$(FenrirExecutable)' == 'true'">
    <PublishAot>true</PublishAot>
    <OptimizationPreference>Speed</OptimizationPreference>
    <IlcInstructionSet>x86-x64-v3</IlcInstructionSet>  <!-- AVX2 : à aligner sur la flotte cible -->
    <StackTraceSupport>true</StackTraceSupport>
    <EventSourcePublishSupport>true</EventSourcePublishSupport>
  </PropertyGroup>
</Project>
```

Points de vigilance AOT spécifiques à cette stack, tous résolus par construction :

| Risque AOT classique | Réponse Fenrir |
| :-- | :-- |
| Sérialisation par réflexion | Sérialiseurs binaires **générés** (§7) ; aucun JSON sur le chemin de jeu |
| DI par assembly scanning | Enregistrements **générés** (`FenrirHandlerModule.AddGeneratedHandlers()`, §6.6) |
| ORM à expression trees | CaeriusNet : mapping ordinal généré à la compilation, zéro réflexion (§11) |
| Binder de configuration | Générateur de binding de `Microsoft.Extensions.Configuration` |
| Bibliothèques tierces non-AOT | `IsAotCompatible` + publication AOT testée en CI sur les trois exécutables |

---

## 3. Arborescence de la solution

### 3.1 Le raisonnement avant l'arbre

> **Décision D-02 — 13 projets, pas 25.**
> Le brouillon *Foundation* découpait la couche réseau en cinq assemblies (`Network.Tcp`, `.Dispatching`, `.Serialization`, `.Security`, `.Compression`). C'est une taxonomie de *namespaces*, pas de *frontières de déploiement*. Multiplier les assemblies n'apporte ici aucune isolation utile (tout est déployé ensemble dans chaque exécutable AOT), mais coûte : temps de build/link ILC, friction de refactoring, et prolifération d'`InternalsVisibleTo`. La règle Fenrir : **une assembly = une frontière de dépendance qu'on veut interdire au compilateur**, un dossier = une organisation interne. La séparation `Login`/`Game` au niveau *Application* reste une vraie frontière (un handler de jeu ne doit jamais être compilable dans le LoginServer) ; `Serialization` vs `Dispatching` n'en est pas une.

### 3.2 L'arbre canonique

```text
Fenrir/
├── Fenrir.slnx
├── global.json                          # SDK 10.0.x épinglé
├── Directory.Build.props                # AOT, C# 14, analyzers (§2.4)
├── Directory.Packages.props             # Central Package Management (versions uniques)
├── .editorconfig
│
├── docs/
│   ├── Fenrir_Architecture_Reference_2026.md      # ce document
│   └── adr/                             # ADR-0001-native-aot.md, ADR-0002-caeriusnet.md, …
│
├── database/                            # ★ LA source de vérité (Data-First) — voir §12.7
│   ├── 00_init/                         # base, filegroup MEMORY_OPTIMIZED_DATA, options (ADR, RCSI, OL)
│   ├── 10_schemas/                      # auth, game, world, social, runtime, telemetry, admin
│   ├── 20_types/                        # types TVP :  runtime/tvp_SessionTicket.sql, …
│   ├── 30_tables/                       # une table = un fichier, rangé par schéma
│   ├── 40_indexes/
│   ├── 50_procedures/                   # une procédure = un fichier = un contrat
│   ├── 60_permissions/                  # rôles fenrir_login_svc / fenrir_game_svc (EXECUTE only)
│   ├── 70_seed/                         # données de référence (items, tables de loot…)
│   └── _manifest.txt                    # ordre d'exécution, journalisé par le DbMigrator
│
├── src/
│   ├── 0_Orchestration/
│   │   ├── Fenrir.AppHost/              # topologie Aspire (§2.2)
│   │   └── Fenrir.ServiceDefaults/      # OTel, health, resilience, options (§2.3)
│   │
│   ├── 1_Generators/
│   │   └── Fenrir.Generators.Protocol/  # Roslyn : sérialiseurs, dispatcher, registres (§7)
│   │       ├── Emitters/                #   PacketSerializerEmitter, DispatcherEmitter, …
│   │       ├── Model/                   #   modèle équatable (incrémentalité)
│   │       └── Diagnostics/             #   FEN001…FEN0xx
│   │
│   ├── 2_Core/
│   │   ├── Fenrir.Contracts/            # le protocole comme code : zéro dépendance
│   │   │   ├── Opcodes/                 #   Opcodes.Auth.cs, Opcodes.Game.cs (constantes ushort)
│   │   │   ├── Packets/Auth/            #   [FenrirPacket] readonly record structs
│   │   │   ├── Packets/Game/
│   │   │   ├── Abstractions/            #   IIncomingPacket<T>, IOutgoingPacket, IPacketHandler…
│   │   │   └── Wire/                    #   FrameHeader, PacketFlags, constantes protocole
│   │   └── Fenrir.Domain/               # règles pures : stats, combat, déplacement, inventaire
│   │       ├── Combat/                  #   zéro I/O, zéro DateTime.Now (horloge injectée)
│   │       ├── Movement/                #   validation autoritaire (vitesse max, collisions)
│   │       ├── Progression/
│   │       └── Primitives/              #   WorldPosition, EntityId, StatBlock (structs)
│   │
│   ├── 3_Infrastructure/
│   │   ├── Fenrir.Network/              # tout le bas niveau réseau (§5, §6, §8)
│   │   │   ├── Transport/               #   FenrirTcpListener, SocketConnection (dual-loop)
│   │   │   ├── Framing/                 #   FrameDecoder, FrameWriter
│   │   │   ├── Dispatching/             #   boucle de session, intégration dispatcher généré
│   │   │   ├── Sessions/                #   ClientSession, SessionRegistry, états
│   │   │   ├── Security/                #   AES-GCM, HKDF, anti-replay, rate limiting
│   │   │   └── Broadcasting/            #   serialize-once multicast (§10.4)
│   │   └── Fenrir.Data/                 # unique porte vers SQL (CaeriusNet) (§11)
│   │       ├── Auth/                    #   AccountRepository + DTO + TVP du schéma auth
│   │       ├── Characters/
│   │       ├── Runtime/                 #   SessionTicketRepository (natively compiled procs)
│   │       └── Persistence/             #   WriteBehindFlusher, DirtyTracker (§10.5)
│   │
│   ├── 4_Application/
│   │   ├── Fenrir.Application.Login/    # handlers + services du LoginServer (§9)
│   │   └── Fenrir.Application.Game/     # handlers + services du GameServer (§10)
│   │       ├── Handlers/Movement/
│   │       ├── Handlers/Combat/
│   │       ├── Handlers/Chat/
│   │       └── World/                   #   Zone, ZoneScheduler, AoiGrid, tick loop
│   │
│   └── 5_Servers/                       # exécutables : composition + Program.cs, rien d'autre
│       ├── Fenrir.LoginServer/
│       ├── Fenrir.GameServer/
│       └── Fenrir.Tools.DbMigrator/     # runner SQL journalisé, AOT (§12.7)
│
├── tests/
│   ├── Fenrir.Contracts.Tests/          # round-trip + golden tests binaires (§14.2)
│   ├── Fenrir.Network.Tests/            # fuzz du FrameDecoder, fragmentation, malformés
│   ├── Fenrir.Domain.Tests/             # règles de jeu (rapides, massifs)
│   ├── Fenrir.Data.Tests/               # DTO ↔ result sets contre SQL réel
│   └── Fenrir.IntegrationTests/         # Aspire.Hosting.Testing : login→handover→jeu (§14.4)
│
└── benchmarks/
    └── Fenrir.Benchmarks/               # gates 0 B/op : parse, dispatch, envoi, AOI (§14.5)
```

### 3.3 Matrice de dépendances (verrouillée)

Lecture : la ligne **peut référencer** la colonne. Tout le reste est interdit et cassé au build.

| ↓ dépend de → | Contracts | Domain | Network | Data | App.Login | App.Game | ServiceDefaults |
| :-- | :--: | :--: | :--: | :--: | :--: | :--: | :--: |
| **Contracts** | — | · | · | · | · | · | · |
| **Domain** | ✔ | — | · | · | · | · | · |
| **Network** | ✔ | · | — | · | · | · | · |
| **Data** | · | ✔ | · | — | · | · | · |
| **App.Login** | ✔ | ✔ | ✔ | ✔ | — | · | · |
| **App.Game** | ✔ | ✔ | ✔ | ✔ | · | — | · |
| **LoginServer (exe)** | ✔ | ✔ | ✔ | ✔ | ✔ | · | ✔ |
| **GameServer (exe)** | ✔ | ✔ | ✔ | ✔ | · | ✔ | ✔ |

Trois lignes de cette matrice portent toute l'architecture :

- `Domain` ne voit **que** `Contracts` (et encore, uniquement les primitives — jamais les paquets) : les règles de jeu sont testables sans socket ni base.
- `Network` ne voit pas `Data` : un handler bas niveau ne peut physiquement pas appeler SQL. Le passage se fait par les services d'Application (§6.5).
- `Data` ne voit pas `Contracts` : les DTO SQL ne sont pas des paquets réseau. La conversion paquet ↔ DTO est un acte explicite dans l'Application, jamais une identité accidentelle — c'est ce qui permet au schéma SQL et au protocole d'évoluer indépendamment (I-06, I-04).

---

## 4. Le protocole filaire Fenrir

### 4.1 Format de trame (frame)

Tout octet qui circule appartient à une trame. En-tête fixe de **12 octets**, little-endian explicite (jamais dépendant de la machine), suivi d'un payload borné.

```text
 Offset  Taille  Champ           Type   Rôle
 ──────  ──────  ──────────────  ─────  ────────────────────────────────────────────
 0       2       PayloadLength   u16    taille du payload (0..MaxPayload)
 2       2       OpCode          u16    identité du message (constantes Fenrir.Contracts)
 4       1       Version         u8     version du *paquet* (pas du protocole global)
 5       1       Flags           u8     bit0=Encrypted, bit1=Compressed, bit2=HasCorrelation
 6       2       Reserved        u16    0 obligatoire (marge d'évolution, validée)
 8       4       SequenceId      u32    compteur monotone par direction (anti-replay §8.5)
 [12     4       CorrelationId   u32    présent uniquement si bit2 — requête/réponse RPC]
 12(+4)  n       Payload         u8[n]  corps, chiffré/compressé selon Flags
```

Contraintes protocolaires, appliquées par le `FrameDecoder` avant tout traitement :

- `MaxPayload = 8 KiB` par défaut ; certains opcodes déclarent une borne inférieure via `[FenrirPacket(MaxPayload = 128)]` — un paquet de mouvement de 3 Ko est une violation, pas un gros paquet.
- `Reserved != 0`, opcode inconnu, version non supportée, taille hors borne ⇒ **violation de protocole** : la session est fermée immédiatement, un compteur `fenrir.net.protocol_violations` est incrémenté avec l'IP en dimension. On ne « tolère » jamais un flux corrompu : sur TCP, un octet faux signifie un pair bogué ou hostile.
- Les entiers sont écrits via `BinaryPrimitives.*LittleEndian` — le format est un contrat de bits, pas un `struct` C# projeté en mémoire.

> **Décision D-03 — Sérialisation générée champ à champ, pas `MemoryMarshal.Cast`.**
> Le brouillon précédent « castait » la mémoire brute en `ref struct`. C'est séduisant et c'est un piège : (1) le layout d'un struct C# (padding, ordre) n'est pas un contrat filaire sans `[StructLayout]` militarisé partout ; (2) l'endianness devient celle de la machine ; (3) impossible d'insérer validation, versionnage ou champs de taille variable ; (4) un `ref struct` pointant dans le buffer réseau interdit tout handler asynchrone (un `ref struct` ne traverse pas un `await`) et couple la durée de vie du paquet à celle du buffer du `PipeReader`. La lecture séquentielle générée (`BinaryPrimitives`, offsets constants, branch-free) a un coût identique — quelques mov — pour zéro de ces problèmes. Le gain théorique du cast est un mirage mesuré en dixièmes de nanoseconde ; ses défauts sont des incidents de production.

### 4.2 Les paquets comme types

Un paquet est un `readonly record struct` **entièrement matérialisé** : après `TryRead`, il ne référence plus le buffer réseau. Primitives copiées par valeur ; le buffer peut être rendu au pipe immédiatement, et le paquet peut traverser un `await` ou un `Channel` sans danger.

```csharp
// src/2_Core/Fenrir.Contracts/Packets/Game/MoveRequest.cs
[FenrirPacket(Opcodes.Game.MoveRequest, PacketDirection.ClientToServer,
              Version = 1, MaxPayload = 32)]
public readonly partial record struct MoveRequest(
    float X, float Y, float Z,
    ushort Heading,
    uint ClientTick);
// Le générateur (§7) émet : OpCode, Version, TryRead(ReadOnlySpan<byte>, out MoveRequest),
// PayloadSize, Write(Span<byte>) — et l'enregistre dans les registres générés.
```

Champs de taille variable : uniquement des chaînes UTF-8 préfixées `u16`, bornées par l'attribut (`[BoundedString(Max = 256)]`). La chaîne C# est allouée au parse — **allocation justifiée** (I-09) : un message de chat n'est pas le hot path, et il doit de toute façon survivre au buffer pour être relayé. Les paquets du hot path (mouvement, combat, heartbeat) sont 100 % primitives : zéro allocation, garanti par le benchmark-gate.

### 4.3 Interfaces du protocole (C# 13/14 : membres statiques abstraits)

```csharp
// src/2_Core/Fenrir.Contracts/Abstractions/
public interface IFenrirPacket
{
    static abstract ushort OpCode  { get; }
    static abstract byte   Version { get; }
}

public interface IIncomingPacket<TSelf> : IFenrirPacket
    where TSelf : struct, IIncomingPacket<TSelf>
{
    static abstract bool TryRead(ReadOnlySpan<byte> payload, out TSelf packet);
}

public interface IOutgoingPacket : IFenrirPacket
{
    int  PayloadSize { get; }              // exact, calculé sans allouer
    void Write(Span<byte> payload);        // écrit exactement PayloadSize octets
}
```

Les membres statiques abstraits font tout le travail que la réflexion faisait en 2019 : le dispatcher généré appelle `MoveRequest.TryRead(...)` de façon **statiquement liée, dévirtualisée et inlinée** par ILC. C'est le pattern central de tout ce protocole.

---

## 5. Couche réseau `System.IO.Pipelines`

### 5.1 Anatomie d'une connexion : le double-loop

Chaque socket accepté est enveloppé dans une `SocketConnection` qui possède **deux pipes et trois boucles** :

```text
                    ┌────────────────────── SocketConnection ──────────────────────┐
   octets kernel →  │  Receive loop ──write──▶ Pipe RX ──read──▶  Session loop      │
                    │  (socket.ReceiveAsync)                    (frames → dispatch) │
                    │                                                               │
   octets kernel ←  │  Send loop  ◀──read── Pipe TX ◀──write──  Session.Send<T>()  │
                    └───────────────────────────────────────────────────────────────┘
```

- **Receive loop** : demande `Memory<byte>` au `PipeWriter` RX (`GetMemory(4096)`), lit le socket *directement dedans* (`socket.ReceiveAsync(memory)`), `Advance(n)`, `FlushAsync()`. Zéro `byte[]` intermédiaire : le kernel écrit dans la mémoire poolée du pipe.
- **Session loop** : consomme le RX, découpe les trames, dispatche (§6).
- **Send loop** : draine le pipe TX vers `socket.SendAsync` — les écritures applicatives et les I/O réseau sont découplées, ce qui rend `Send<T>` non bloquant et permet la coalescence naturelle des petits paquets.

Configuration socket & pipes — chaque valeur est un choix, pas un défaut subi :

```csharp
// src/3_Infrastructure/Fenrir.Network/Transport/FenrirTcpListener.cs (extrait)
listenSocket.NoDelay = true;                    // Nagle OFF : latence > débit pour un MMO

private static readonly PipeOptions RxOptions = new(
    pool: MemoryPool<byte>.Shared,
    readerScheduler: PipeScheduler.ThreadPool,
    writerScheduler: PipeScheduler.Inline,       // le receive-loop écrit sans re-scheduling
    pauseWriterThreshold: 512 * 1024,            // backpressure : client qui n'est pas lu…
    resumeWriterThreshold: 256 * 1024,           // …finit par bloquer sa propre réception
    minimumSegmentSize: 4096,
    useSynchronizationContext: false);
```

Le couple pause/resume est la **défense anti « slow loris » et anti-flood** au niveau mémoire : un client qui envoie plus vite qu'on ne traite voit son producteur (le receive loop) suspendu, donc sa fenêtre TCP se fermer — la pression retourne chez l'attaquant, pas dans notre heap.

### 5.2 La boucle de session

```csharp
// src/3_Infrastructure/Fenrir.Network/Dispatching/SessionLoop.cs
public static async Task RunAsync(ClientSession session, CancellationToken ct)
{
    PipeReader reader = session.Transport.Input;
    try
    {
        while (true)
        {
            ReadResult result = await reader.ReadAsync(ct);
            ReadOnlySequence<byte> buffer = result.Buffer;

            while (FrameDecoder.TryReadFrame(ref buffer, session, out Frame frame))
            {
                // Le dispatch est awaité : l'ordre des paquets D'UNE session est
                // strictement préservé, et un client ne peut pas empiler du travail
                // plus vite qu'on ne l'exécute (backpressure de bout en bout).
                await MessageDispatcher.DispatchAsync(in frame, session, ct);
            }

            reader.AdvanceTo(consumed: buffer.Start, examined: buffer.End);
            if (result.IsCompleted) break;                    // FIN propre côté client
        }
    }
    catch (ProtocolViolationException pv) { session.Abort(DisconnectReason.Protocol, pv); }
    catch (OperationCanceledException)    { /* arrêt serveur */ }
    finally { await session.CompleteAsync(); }
}
```

### 5.3 `FrameDecoder` — découpage sans copie

```csharp
// src/3_Infrastructure/Fenrir.Network/Framing/FrameDecoder.cs
public static bool TryReadFrame(ref ReadOnlySequence<byte> buffer,
                                ClientSession session, out Frame frame)
{
    frame = default;
    if (buffer.Length < FrameHeader.Size) return false;                 // 12 octets

    Span<byte> header = stackalloc byte[FrameHeader.Size];
    buffer.Slice(0, FrameHeader.Size).CopyTo(header);                   // copie stack : 12 o

    ushort payloadLen = BinaryPrimitives.ReadUInt16LittleEndian(header);
    ushort opcode     = BinaryPrimitives.ReadUInt16LittleEndian(header[2..]);
    byte   version    = header[4];
    var    flags      = (PacketFlags)header[5];
    ushort reserved   = BinaryPrimitives.ReadUInt16LittleEndian(header[6..]);
    uint   sequence   = BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);

    if (reserved != 0 || payloadLen > OpcodeRegistry.MaxPayloadOf(opcode))
        throw new ProtocolViolationException(opcode, payloadLen);

    long total = FrameHeader.Size + payloadLen;
    if (buffer.Length < total) return false;                            // trame incomplète

    session.Security.ValidateSequence(sequence);                        // anti-replay §8.5

    ReadOnlySequence<byte> payload = buffer.Slice(FrameHeader.Size, payloadLen);
    frame  = new Frame(opcode, version, flags, sequence, payload);
    buffer = buffer.Slice(total);
    return true;
}
```

`Frame` est un `readonly ref struct` : il *peut* référencer le buffer parce qu'il ne vit que le temps du parse synchrone dans `DispatchAsync` — la matérialisation en `record struct` (§4.2) a lieu avant tout point d'`await`. Si le payload est fragmenté sur plusieurs segments du pipe (rare : trames ≤ 8 Ko, segments de 4 Ko), le générateur passe par un `stackalloc` de linéarisation ; toujours zéro heap.

### 5.4 Chemin d'envoi : `Send<T>` et flush

```csharp
// src/3_Infrastructure/Fenrir.Network/Sessions/ClientSession.Send.cs
private readonly Lock _sendLock = new();          // System.Threading.Lock (.NET 9+)

public void Send<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket
{
    int payloadSize = packet.PayloadSize;
    int total       = FrameHeader.Size + payloadSize;

    lock (_sendLock)                               // lock dédié : Monitor spécialisé, sans boxing
    {
        Span<byte> span = _txWriter.GetSpan(total);
        FrameWriter.WriteHeader(span, TPacket.OpCode, TPacket.Version,
                                _flags, NextOutboundSequence(), (ushort)payloadSize);
        packet.Write(span.Slice(FrameHeader.Size, payloadSize));
        if (Security.EncryptionEnabled)
            Security.EncryptInPlace(span[FrameHeader.Size..total], span[..FrameHeader.Size]);
        _txWriter.Advance(total);
    }
    _flusher.RequestFlush();   // hors lock : coalescence, voir ci-dessous
}
```

Le `Flusher` applique une politique à deux vitesses : flush immédiat si le pipe TX est « froid », sinon coalescence jusqu'au prochain point de flush (fin de dispatch, ou fin de tick pour les broadcasts de zone). Résultat : les réponses unitaires partent tout de suite, les rafales de tick partent en **un** `sendmsg` par session. `FlushResult.IsCanceled/IsCompleted` et le seuil `pauseWriterThreshold` du pipe TX (128 Ko) protègent contre le client qui ne lit plus : au-delà, la session est fermée (`fenrir.net.slow_consumer_kicks`).

---

## 6. Le système de messages

C'est le cœur demandé : **MessageFactory**, **MessageDispatcher**, **MessageHandlers**, Requests/Responses — entièrement générés, entièrement typés, zéro réflexion.

### 6.1 Vue d'ensemble du flux

```text
Frame ──▶ MessageDispatcher (généré)
             │ switch(frame.OpCode)                    ◀── un jump-table, pas un dictionnaire
             │
             ├─▶ MessageFactory (généré) : TryRead → readonly record struct   [0 alloc]
             │
             ├─▶ voie INLINE  : handler synchrone, sur la boucle de session   [mouvement, ping]
             └─▶ voie ASYNC   : ValueTask, awaitée par la boucle              [login, DB, RPC]
                                  └─▶ enqueue vers Zone/Service si travail lourd (§10.2)
```

### 6.2 Deux familles de handlers — et pourquoi

```csharp
// src/2_Core/Fenrir.Contracts/Abstractions/Handlers.cs
public interface IInlinePacketHandler<TPacket>
    where TPacket : struct, IIncomingPacket<TPacket>
{
    void Handle(in TPacket packet, ClientSession session);
}

public interface IAsyncPacketHandler<TPacket>
    where TPacket : struct, IIncomingPacket<TPacket>
{
    ValueTask HandleAsync(TPacket packet, ClientSession session, CancellationToken ct);
}
```

> **Décision D-04 — La règle de la boucle de session.**
> Un handler **inline** s'exécute sur la boucle de session : il a un budget de **quelques microsecondes**, ne touche ni SQL ni verrou contendu, et ne fait qu'une chose : valider, puis soit répondre (`session.Send`), soit *poster une intention* dans le channel de la zone (§10.2) — un enqueue lock-free, borné. Un handler **async** (authentification, sélection de personnage, consommation de ticket) est awaité par la boucle : pendant son I/O, cette session ne traite pas d'autre paquet — c'est voulu, c'est l'ordre causal et la backpressure gratuits. **Aucun handler ne fait jamais de calcul de simulation** : la simulation appartient au tick de zone, qui est le seul écrivain de l'état monde. Cette règle remplace des milliers de verrous par une topologie.

`Request/Response` (RPC applicatif) : un paquet entrant marqué `HasCorrelation` attend une réponse portant le même `CorrelationId`. Le générateur lie les paires via `[FenrirPacket(..., RespondsWith = typeof(CharacterListResponse))]` et vérifie à la compilation qu'un handler d'une requête corrélée émet bien le type de réponse déclaré. Les `INotification` sont simplement des paquets sortants sans corrélation (broadcasts).

### 6.3 Le `MessageDispatcher` généré

```csharp
// obj/…/Fenrir.Generators.Protocol/MessageDispatcher.g.cs  (extrait, jamais édité)
public static partial class MessageDispatcher
{
    public static ValueTask DispatchAsync(in Frame frame, ClientSession session,
                                          CancellationToken ct)
    {
        // Garde générée : cet opcode est-il légal dans l'état actuel de la session ?
        if (!SessionStateGate.Allows(session.State, frame.OpCode))
            return session.AbortAsync(DisconnectReason.StateViolation);

        switch (frame.OpCode)
        {
            case Opcodes.Game.MoveRequest:
            {
                if (!MessageFactory.TryCreate(in frame, out MoveRequest p))
                    return session.AbortAsync(DisconnectReason.Malformed);
                session.Handlers.MoveRequest.Handle(in p, session);       // INLINE
                return ValueTask.CompletedTask;
            }
            case Opcodes.Auth.LoginRequest:
            {
                if (!MessageFactory.TryCreate(in frame, out LoginRequest p))
                    return session.AbortAsync(DisconnectReason.Malformed);
                return session.Handlers.LoginRequest.HandleAsync(p, session, ct); // ASYNC
            }
            // … un case par opcode, trié : le JIT/ILC émet une jump table O(1)
            default:
                return session.AbortAsync(DisconnectReason.UnknownOpcode);
        }
    }
}
```

> **Décision D-05 — `switch` généré plutôt que table de `delegate* unmanaged`.**
> Les pointeurs de fonction non managés interdisent les génériques, les méthodes d'instance et le passage de `ref struct`/`in` typés — on retombe sur des signatures effacées (`void*`) qui détruisent précisément la sûreté qu'on a construite. Un `switch` sur des `ushort` denses compile en table de sauts indirecte : même coût, dévirtualisation totale des `TryRead`/`Handle` par ILC, et le débogueur montre du vrai code. Les opcodes sont assignés par plages contiguës (`Auth = 0x0100–01FF`, `Game.Movement = 0x0200–…`) pour garantir la densité de la table.

### 6.4 La `MessageFactory` générée

`MessageFactory.TryCreate` déchiffre (si `Flags.Encrypted`), décompresse (si `Flags.Compressed`, LZ4 via buffer `ArrayPool` restitué en `finally`), linéarise le payload si multi-segments (`stackalloc`), puis appelle le `TryRead` généré du type. Elle vérifie `frame.Version` contre les versions supportées du paquet et route vers `TryRead_V1`/`TryRead_V2` si le paquet a évolué (I-06). Toute sortie `false` = trame hostile ou bug pair ⇒ déconnexion, jamais de « meilleure interprétation possible ».

### 6.5 Écrire un handler (ce que voit un développeur Fenrir)

```csharp
// src/4_Application/Fenrir.Application.Game/Handlers/Movement/MoveRequestHandler.cs
public sealed class MoveRequestHandler(MovementRules rules) // Domain pur, injecté
    : IInlinePacketHandler<MoveRequest>
{
    public void Handle(in MoveRequest packet, ClientSession session)
    {
        var player = session.Player;                    // non-null garanti par SessionStateGate
        var intent = new MoveIntent(player.EntityId,
                                    new WorldPosition(packet.X, packet.Y, packet.Z),
                                    packet.Heading, packet.ClientTick);

        if (!rules.IsPlausible(player.LastAuthoritativeState, intent))   // anti-speed-hack
        {
            session.Send(new ForcePositionSync(player.LastAuthoritativeState)); // I-08
            return;
        }
        player.Zone.Post(ZoneCommand.Move(intent));     // enqueue lock-free ; le tick décidera
    }
}
```

Aucune trace de socket, de span, d'opcode : le développeur gameplay manipule des types, la plomberie est générée. C'est le critère de réussite de toute cette section.

### 6.6 Enregistrement : le `HandlerRegistry` généré

Le générateur découvre chaque implémentation de `IInlinePacketHandler<>` / `IAsyncPacketHandler<>` **présente dans la compilation de l'exécutable** (donc : les handlers Game n'existent pas dans le LoginServer — la matrice §3.3 le garantit) et émet :

```csharp
// généré — deux artefacts
public static class FenrirHandlerModule
{
    public static IServiceCollection AddGeneratedHandlers(this IServiceCollection s)
    {
        s.AddSingleton<MoveRequestHandler>();
        s.AddSingleton<LoginRequestHandler>();
        // …
        s.AddSingleton<PacketHandlerHub>();      // agrège tout, résolu UNE fois au démarrage
        return s;
    }
}

public sealed class PacketHandlerHub(MoveRequestHandler moveRequest,
                                     LoginRequestHandler loginRequest /*…*/)
{
    public readonly MoveRequestHandler  MoveRequest  = moveRequest;
    public readonly LoginRequestHandler LoginRequest = loginRequest;
}
```

Les handlers sont des **singletons sans état de requête** (l'état vit dans `ClientSession` et les zones) : zéro scope DI par paquet, zéro résolution au runtime, zéro allocation de dispatch. Diagnostic `FEN010` si deux handlers ciblent le même paquet, `FEN011` si un opcode n'a pas de handler.

---

## 7. Les Source Generators Fenrir

`Fenrir.Generators.Protocol` est un **incremental generator** Roslyn (pipeline `IIncrementalGenerator`, modèle intermédiaire *équatable* pour un cache parfait : retaper un commentaire ne régénère rien).

### 7.1 Entrées → sorties

| Le développeur écrit | Le générateur émet |
| :-- | :-- |
| `[FenrirPacket] readonly partial record struct` | `OpCode`, `Version`, `TryRead` (par version), `PayloadSize`, `Write` — lecture/écriture `BinaryPrimitives`, offsets constants, `stackalloc` pour la linéarisation |
| Implémentations de `IInline/IAsyncPacketHandler<T>` | `MessageDispatcher` (switch), `PacketHandlerHub`, `FenrirHandlerModule.AddGeneratedHandlers()` |
| Constantes `Opcodes.*` + attributs | `OpcodeRegistry` : `MaxPayloadOf(ushort)`, `NameOf(ushort)` (logs/metrics), table états-autorisés `SessionStateGate` |
| `[FenrirPacket(RespondsWith = …)]` | Vérification compile-time des paires Request/Response corrélées |

### 7.2 Diagnostics (extraits du catalogue `FEN`)

| Id | Sévérité | Règle |
| :-- | :-- | :-- |
| FEN001 | error | Type `[FenrirPacket]` non `readonly partial record struct` |
| FEN002 | error | Champ de type non sérialisable (référence, générique ouvert…) |
| FEN003 | error | Chaîne sans `[BoundedString]` |
| FEN004 | error | Collision d'opcode entre deux paquets |
| FEN005 | warning | Paquet hot-path (plage Movement/Combat) contenant une chaîne |
| FEN010 | error | Deux handlers pour le même paquet |
| FEN011 | error | Opcode entrant sans handler dans cet exécutable |
| FEN012 | error | Handler de requête corrélée n'émettant pas la réponse déclarée |

### 7.3 Contrat de non-régression du générateur

Le générateur est du code critique : il est testé comme tel. `Fenrir.Contracts.Tests` contient (a) des **round-trips** propriété-basés (`Write` puis `TryRead` ≡ identité, y compris payloads fragmentés à chaque frontière d'octet possible), et (b) des **golden tests** : les octets exacts de chaque paquet de référence sont figés en snapshot ; tout diff binaire non intentionnel casse la CI — c'est l'assurance I-06 que le protocole n'évolue que volontairement.

---

## 8. Sessions, Handover & Cryptographie

### 8.1 `ClientSession` : la seule chose mutable du réseau

```csharp
// src/3_Infrastructure/Fenrir.Network/Sessions/ClientSession.cs (surface)
public sealed class ClientSession
{
    public required long              SessionId  { get; init; }   // séquence process-local
    public required IDuplexPipe       Transport  { get; init; }
    public required PacketHandlerHub  Handlers   { get; init; }
    public SessionState               State      { get; private set; }
    public SessionSecurity            Security   { get; }          // clés, séquences, replay
    public PlayerHandle?              Player     { get; private set; } // ≠ null en InWorld
    public ConnectionStats            Stats      { get; }          // RTT, in/out, dernier ping
    // Transitions d'état : méthodes explicites, jamais un setter
    public void TransitionToAuthenticated(AccountIdentity id) { … }
    public void TransitionToInWorld(PlayerHandle player)      { … }
}
```

Machine à états, appliquée **avant** le dispatch par le `SessionStateGate` généré (§6.3) :

```text
Connected ──HelloOk──▶ Handshaking ──TicketOk──▶ Authenticated ──EnterWorld──▶ InWorld
     │                     │                          │                          │
     └──── timeout 5 s ────┴───── timeout 10 s ───────┴──── Disconnecting ◀─────┘
```

Un `LoginRequest` reçu en état `InWorld`, un `MoveRequest` en état `Handshaking` : violations d'état, déconnexion. Cette table générée élimine une classe entière d'exploits (paquets hors séquence) sans un seul `if` écrit à la main dans les handlers.

Le `SessionRegistry` (par serveur) est un `ConcurrentDictionary<long, ClientSession>` + index secondaire `AccountId → SessionId` pour appliquer **une connexion par compte** (la nouvelle connexion évince l'ancienne, politique configurable).

### 8.2 Handover LoginServer → GameServer

| Étape | Nœud | Action |
| :-- | :-- | :-- |
| 1 | Client → Login | `LoginRequest` (TLS applicatif §8.4 déjà établi) |
| 2 | Login → SQL | `[auth].[usp_Account_Authenticate]` — Argon2id vérifié **côté C#** (§9.1), la proc ne fait que lire hash+sel+état du compte et journaliser |
| 3 | Login | Génère `TicketId` (GUID v7) + `Secret` (32 octets `RandomNumberGenerator`) |
| 4 | Login → SQL | `[runtime].[usp_SessionTicket_Create]` — **natively compiled**, table memory-optimized, TTL 15 s (§12.4) |
| 5 | Login → Client | `HandoverResponse { GameHost, GamePort, TicketId }` — **le Secret ne transite jamais en clair** : il est chiffré sous la clé de session Login déjà établie |
| 6 | Client → Game | Ouvre le socket, envoie `GameHandshake { TicketId, Proof }` où `Proof = HMAC-SHA256(Secret, "fenrir/handshake" ‖ TicketId ‖ ClientNonce)` |
| 7 | Game → SQL | `[runtime].[usp_SessionTicket_Consume]` — lecture **et** suppression atomiques (single-use), renvoie `AccountId, CharacterId?, Secret, ExpiresAtUtc` |
| 8 | Game | Vérifie le Proof (comparaison temps-constant), dérive les clés (§8.4), `TransitionToAuthenticated` — SQL n'est **plus jamais** sur le chemin du gameplay |

Propriétés de sécurité obtenues : ticket à usage unique (le `DELETE` atomique de la proc native rend le rejeu impossible même sous course entre deux connexions), fenêtre de 15 s, preuve de possession du secret sans transmission du secret, et liaison cryptographique ticket ↔ canal de jeu.

### 8.3 Reconnexion (résilience mobile/Wi-Fi)

Une coupure TCP en `InWorld` ne détruit pas l'état : la session passe en `Limbo(90 s)` — l'avatar reste en monde (protégé/pacifié selon les règles Domain), les paquets sortants sont jetés. Le client se reconnecte via un `ReconnectTicket` (émis dans le `EnterWorldResponse` initial, même mécanique runtime que le ticket de handover). Succès : reprise du `PlayerHandle` existant, resynchronisation d'état complète poussée par la zone. Échec/timeout : déconnexion normale + flush de persistance (§10.5).

### 8.4 Cryptographie du canal

- **Phase Login** (avant toute donnée sensible) : échange **X25519** éphémère signé par la clé statique du serveur (embarquée + épinglée côté client) → secret partagé → `HKDF-SHA256` → clés directionnelles. Pas de PKI web, pas d'OpenSSL exotique : `System.Security.Cryptography` couvre X25519/HKDF/AES-GCM en .NET 10, AOT-friendly.
- **Phase Game** : les clés sont dérivées du `Secret` du ticket : `HKDF(Secret, salt: TicketId, info: "fenrir/c2s" | "fenrir/s2c")` — aucun nouvel échange de clés, un aller-retour de moins.
- **Chiffrement** : `AES-256-GCM`, *in place* dans le span d'envoi (§5.4). Nonce 96 bits = `4 octets de sel de session ‖ SequenceId u64 directionnel` — jamais réutilisé par construction. L'en-tête de trame (12 octets) est passé en **AAD** : un attaquant ne peut pas réécrire opcode/flags/séquence sans invalider le tag.
- Le heartbeat et le tout premier `Hello` sont les seuls paquets en clair (`Flags.Encrypted = 0` autorisé uniquement pour ces opcodes — vérifié par le `SessionStateGate`).

### 8.5 Anti-replay & rate limiting

`ValidateSequence` (appelé par le `FrameDecoder`) exige une séquence entrante **strictement croissante** — TCP garantit l'ordre, donc tout recul = rejeu ou implémentation hostile ⇒ déconnexion. Par-dessus : token-buckets par session **et par opcode-classe** (Movement : 30/s, burst 10 ; Chat : 4/s ; Auth : 1/5 s), configurés dans `Fenrir.Contracts`, appliqués par le dispatcher généré avant le handler. Dépassement : throttle silencieux (mouvement) ou disconnect (auth). Compteurs exportés (`fenrir.net.ratelimit_hits{opclass}`) — c'est aussi un capteur de bots.

---

## 9. LoginServer

Petit, paranoïaque, jetable (stateless hors sessions TCP en cours) — on peut en mettre N derrière un simple DNS round-robin.

### 9.1 Authentification

- **Argon2id** (m=64 MiB, t=3, p=1 — recalibré par benchmark annuel) via implémentation managée AOT-compatible. Le hash est vérifié **dans le LoginServer**, pas dans SQL : la CPU de hachage doit saturer un nœud sans état réplicable, jamais la base. La proc `[auth].[usp_Account_Authenticate]` renvoie `PasswordHash, PasswordSalt, Status, FailedCount, LockoutUntil` ; le verdict est journalisé ensuite via `[auth].[usp_Account_RecordLoginAttempt]` (TVP si rafale).
- **Anti-bruteforce** en profondeur : token-bucket par IP en mémoire process (première ligne, coût zéro) **puis** compteur persistant par compte en base (verrouillage progressif 1 min → 15 min → alerte). Réponses en temps homogène : « compte inconnu » et « mot de passe faux » sont indistinguables en contenu *et en latence* (padding temporel).
- **MFA (TOTP)** : si activé sur le compte, l'état de session intercale `AwaitingMfa` — un état de plus dans la machine §8.1, zéro code spécial dans les handlers.

### 9.2 Sélection du shard & annuaire

Les GameServers s'annoncent en base toutes les 5 s : `[runtime].[usp_GameServer_Heartbeat] (ShardId, Host, Port, Ccu, Capacity, TickP99)` — table memory-optimized `SCHEMA_ONLY`. Le LoginServer lit l'annuaire via CaeriusNet **avec cache InMemory 2 s** (`.AddInMemoryCache("shards", TimeSpan.FromSeconds(2))`, §11.4) : la sélection (moindre charge pondérée, affinité du personnage à son shard) ne coûte une requête SQL que toutes les 2 s quel que soit le débit de logins. Un shard sans heartbeat depuis 15 s disparaît de l'offre — c'est aussi le mécanisme de drain pour maintenance.

---

## 10. GameServer

### 10.1 Modèle d'exécution : des zones-acteurs, un seul écrivain

> **Décision D-06 — Un thread logique par zone, zéro verrou sur l'état monde.**
> L'état d'une zone (entités, positions, combats, aggro) n'est lu/écrit **que** par le tick de cette zone. Tout le reste du monde — handlers de session, autres zones, timers — communique par messages via un `Channel` borné. On échange des milliers de verrous fins (et leurs deadlocks, leurs convois, leurs heisenbugs) contre une file MPSC lock-free et un invariant simple à auditer. C'est le modèle éprouvé des serveurs qui tiennent : un acteur par partition spatiale.

```csharp
// src/4_Application/Fenrir.Application.Game/World/Zone.cs (squelette)
public sealed class Zone
{
    private readonly Channel<ZoneCommand> _inbox = Channel.CreateBounded<ZoneCommand>(
        new BoundedChannelOptions(capacity: 8192)
        { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite }); // on jette,
        // on ne bloque jamais un producteur : un Move perdu est réémis au tick suivant.

    public bool Post(in ZoneCommand cmd) => _inbox.Writer.TryWrite(cmd);

    internal async Task RunAsync(CancellationToken ct)     // le "thread" de zone
    {
        var tick = TimeSpan.FromMilliseconds(50);           // 20 Hz
        using var timer = new PeriodicTimer(tick);
        while (await timer.WaitForNextTickAsync(ct))
        {
            long t0 = Stopwatch.GetTimestamp();
            DrainInbox();            // 1. appliquer les intentions (bornées → temps borné)
            Simulate(tick);          // 2. Domain pur : mouvement, combat, IA, timers
            PublishAoi();            // 3. deltas de visibilité → broadcasts (§10.4)
            _persistence.Collect(this); // 4. marquage dirty, pas d'I/O ici (§10.5)
            FenrirMetrics.TickDuration.Record(Stopwatch.GetElapsedTime(t0).TotalMilliseconds,
                                              _shardTag, _zoneTag);
        }
    }
}
```

Le `ZoneScheduler` fixe l'affinité : N zones réparties sur `Environment.ProcessorCount - k` workers dédiés (les k restants servent l'I/O réseau) ; les zones chaudes (capitale) obtiennent un worker exclusif. `TickDuration p99 > 50 ms` = alerte : c'est **la** métrique de santé d'un shard.

### 10.2 `ZoneCommand` : l'ABI interne du GameServer

`ZoneCommand` est un `readonly struct` union discriminée à la main (un `enum Kind` + champs superposés via champs nullables *ou* `[StructLayout(Explicit)]` pour les variantes primitives) — pas de hiérarchie de classes, pas d'allocation par commande. Les intentions volumineuses (rare : échange d'objets multi-lignes) portent un index vers un pool d'objets réutilisés.

### 10.3 Interest management : la grille AOI

Partition uniforme de la zone en cellules carrées de côté = rayon de vue (75 m → cellules 75 m) : une entité n'est visible que depuis sa cellule et les 8 voisines. Structures : `entityId → cellIndex` (array), `cellIndex → PooledList<EntityId>`. Au tick, seuls les franchissements de cellule génèrent des événements enter/leave ; les états des entités visibles sont poussés en **snapshots delta** (position quantifiée : `ushort` sur l'étendue de la cellule, cap de N entités prioritaires par client). Coût : O(entités mobiles), pas O(n²).

### 10.4 Broadcast « serialize-once »

> **Décision D-07 — Un paquet broadcasté est sérialisé une fois, pas |audience| fois.**

```csharp
// src/3_Infrastructure/Fenrir.Network/Broadcasting/Broadcaster.cs (idée)
public static void Broadcast<TPacket>(in TPacket packet, ReadOnlySpan<ClientSession> audience)
    where TPacket : struct, IOutgoingPacket
{
    int total = FrameHeader.Size + packet.PayloadSize;
    byte[] rented = ArrayPool<byte>.Shared.Rent(total);      // ALLOC: amortie, restituée
    // écrire header (séquence laissée en blanc) + payload UNE fois…
    foreach (var s in audience) s.SendPrepared(rented.AsSpan(0, total)); // copie mémoire→pipe,
                                                              // séquence+chiffrement par session
    ArrayPool<byte>.Shared.Return(rented);
}
```

Avec chiffrement par session, la passe AES-GCM reste par destinataire (nonces distincts obligent) — mais la sérialisation, la validation et le calcul de taille ne sont payés qu'une fois. Pour les très grosses audiences (annonces monde), un canal *non chiffré autorisé par opcode* permet le vrai zéro-copie multicast interne.

### 10.5 Persistance write-behind (le pont vers §11/§12)

L'état monde vit en RAM ; SQL est un **journal de durabilité**, pas un participant au gameplay.

- Chaque entité joueur porte un `DirtyFlags` (Position, Vitals, Inventory, Progression…). Le tick les lève ; personne d'autre.
- Le `WriteBehindFlusher` (un par shard, hors threads de zone) draine toutes les **5 s** ou **512 entités** : il construit des listes TVP par nature de donnée et appelle les procs de batch — `[game].[usp_Character_PersistBatch] (@Positions tvp, @Vitals tvp)`, `[game].[usp_Inventory_ApplyDeltas] (@Deltas tvp)`. Un aller-retour SQL pour des centaines de joueurs (§11.3).
- **Flush immédiat et ciblé** sur : déconnexion, transaction économique (échange, hôtel des ventes, achat), franchissement de palier (level-up). L'économie ne perd jamais rien ; 4 secondes de position, oui, et c'est un choix assumé et documenté (ADR-0007).
- Idempotence : chaque batch porte un `FlushSequence` par personnage ; les procs ignorent un batch ≤ dernier appliqué — un retry réseau ne double-crédite jamais.

---

## 11. Couche Data : CaeriusNet

CaeriusNet est exactement l'outil que réclame l'invariant I-03 : un micro-ORM **dédié aux procédures stockées SQL Server**, sans traduction LINQ, sans change-tracking, avec mapping ordinal **généré à la compilation** (`[GenerateDto]` → `ISpMapper<T>`, `[GenerateTvp]` → `ITvpMapper<T>`), lecture `SequentialAccess` par index, listes pré-dimensionnées, et observabilité OpenTelemetry native. API vérifiée contre la documentation officielle (caerius.net) — les signatures ci-dessous sont exactes.

### 11.1 Enregistrement (côté serveurs, via Aspire)

```csharp
// src/5_Servers/Fenrir.GameServer/Program.cs (extrait)
var builder = Host.CreateApplicationBuilder(args);
builder.AddFenrirDefaults();                       // §2.3

CaeriusNetBuilder
    .Create(builder)
    .WithAspireSqlServer("FenrirDb")               // consomme la référence injectée par l'AppHost
    .Build();

builder.Services.AddGeneratedHandlers();           // §6.6
builder.Services.AddSingleton<CharacterRepository>();
builder.Services.AddSingleton<SessionTicketRepository>();
```

Règle Fenrir : `ICaeriusNetDbContext` n'est injecté **que** dans `Fenrir.Data`. Les repositories sont des `sealed record` à constructeur primaire, **singletons** (le contexte gère le pooling de connexions), et exposent des `ValueTask` typées métier — jamais de builder ni de `SqlDbType` ne fuit hors de `Fenrir.Data`.

### 11.2 Le pattern DTO — le contrat ordinal

```csharp
// src/3_Infrastructure/Fenrir.Data/Runtime/SessionTicketRepository.cs
[GenerateDto]                                       // mapper ISpMapper<T> généré, zéro réflexion
public sealed partial record ConsumedTicketDto(
    int AccountId, int? CharacterId, byte[] Secret, DateTime ExpiresAtUtc);

public sealed record SessionTicketRepository(ICaeriusNetDbContext Db)
{
    public async ValueTask<ConsumedTicketDto?> ConsumeAsync(Guid ticketId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_SessionTicket_Consume")
            .AddParameter("TicketId", ticketId, SqlDbType.UniqueIdentifier)   // sans '@'
            .Build();

        return await Db.FirstQueryAsync<ConsumedTicketDto>(sp, ct);           // null si expiré
    }
}
```

Le mapping est **positionnel** : l'ordre des paramètres du constructeur = l'ordre des colonnes du `SELECT` final de la procédure. C'est précisément l'incarnation de l'invariant I-04 : **le result set de la procédure est le contrat**, le DTO s'y conforme, et l'analyzer CaeriusNet (règles `CAERIUS0xx`) verrouille la forme `sealed partial record` à constructeur primaire dans l'IDE. Chaque en-tête de procédure documente donc son result set colonne par colonne (§12.3) — modifier l'un sans l'autre casse `Fenrir.Data.Tests` (§14.3).

Les quatre formes de lecture, choisies délibérément selon l'usage :

| Méthode | Retour | Usage Fenrir |
| :-- | :-- | :-- |
| `FirstQueryAsync<T>` | `T?` | lookups unitaires (ticket, compte) |
| `QueryAsImmutableArrayAsync<T>` | `ImmutableArray<T>` | données figées passées par valeur (templates, annuaire) |
| `QueryAsReadOnlyCollectionAsync<T>` | `ReadOnlyCollection<T>` | listes exposées à l'Application (personnages du compte) |
| `QueryAsIEnumerableAsync<T>` | `IEnumerable<T>` | pipelines internes ; jamais en surface d'API |

Le troisième argument du builder, `resultSetCapacity`, pré-dimensionne la liste : on le renseigne **partout** avec la cardinalité attendue (`("game","usp_Character_GetByAccount", 8)` — 8 personnages max par compte) — c'est une allocation de moins par appel, gratuite à écrire.

### 11.3 TVP : le cœur du write-behind

```csharp
// src/3_Infrastructure/Fenrir.Data/Characters/CharacterRepository.cs
[GenerateTvp(Schema = "game", TvpName = "tvp_CharacterPosition")]
public sealed partial record CharacterPositionTvp(
    int CharacterId, long FlushSequence, short MapId,
    float PosX, float PosY, float PosZ, short Heading);

public sealed record CharacterRepository(ICaeriusNetDbContext Db)
{
    public async ValueTask PersistPositionsAsync(
        IReadOnlyList<CharacterPositionTvp> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return;   // garde obligatoire : SQL Server rejette un TVP vide

        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_PersistBatch")
            .AddTvpParameter("Positions", rows)
            .Build();

        await Db.ExecuteAsync(sp, ct); // fire-and-forget : pas besoin du rowcount ici
    }
}
```

Le générateur `[GenerateTvp]` émet un flux `IEnumerable<SqlDataRecord>` qui **réutilise une seule instance** de record sur toutes les lignes : envoyer 500 positions coûte un aller-retour et pratiquement zéro pression GC. C'est exactement le profil du flusher §10.5. Écritures : `ExecuteAsync` (void), `ExecuteNonQueryAsync` (rowcount — utilisé quand l'idempotence doit être vérifiée), `ExecuteScalarAsync<T>` (identités, compteurs).

### 11.4 Caching à trois étages (par appel)

```csharp
// Annuaire des shards (LoginServer, §9.2) : fraîcheur 2 s suffit
var sp = new StoredProcedureParametersBuilder("runtime", "usp_GameServer_GetDirectory", 16)
    .AddInMemoryCache("shards:directory", TimeSpan.FromSeconds(2))
    .Build();

// Données de référence monde (boot du GameServer) : immuables jusqu'au reboot
var tpl = new StoredProcedureParametersBuilder("world", "usp_ItemTemplate_GetAll", 8192)
    .AddFrozenCache("world:item-templates")
    .Build();
var templates = await Db.QueryAsImmutableArrayAsync<ItemTemplateDto>(tpl, ct);
```

Sur un hit, **aucune commande SQL n'est exécutée** — seul le compteur `caerius.cache.lookups{hit=true}` s'incrémente. Le tiers Redis (`WithAspireRedis()`) reste hors périmètre v1 : deux shards partageant du cache runtime, c'est la porte d'entrée des incohérences ; l'annuaire en base + TTL 2 s suffit.

### 11.5 Transactions : uniquement pour l'économie

```csharp
// Échange joueur↔joueur : atomique ou rien (flush immédiat, §10.5)
await using var tx = await Db.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
await tx.ExecuteNonQueryAsync(spRemoveItemsA, ct);
await tx.ExecuteNonQueryAsync(spGrantItemsB, ct);
await tx.ExecuteNonQueryAsync(spLedgerAppend, ct);
await tx.CommitAsync(ct);
// Sans CommitAsync : rollback automatique au dispose ; état "Poisoned" si une étape a échoué
// → impossible de commiter à moitié par accident. Le scope trace un span parent unique.
```

Le write-behind ordinaire, lui, ne s'emballe **pas** dans des transactions applicatives : chaque proc de batch est atomique et idempotente par construction (`FlushSequence`, §12.6) — c'est plus rapide et plus sûr qu'une transaction longue.

### 11.6 Multi result sets & discipline de retry

L'écran de sélection de personnage tient en **un** aller-retour : `[game].[usp_Character_GetSelectionScreen]` renvoie trois result sets (personnages, apparences, équipement visible) mappés en tuple typé — CaeriusNet en supporte jusqu'à cinq. Côté résilience : **seules les lectures et les écritures idempotentes** (batchs à `FlushSequence`) sont retryables ; `usp_SessionTicket_Consume` et les procs d'économie ne le sont jamais (un retry aveugle sur un timeout de commit est le bug de dupe classique des MMO).

---

## 12. Base de données Data-First — SQL Server 2025

### 12.1 Options de base (posées par `00_init`, jamais « par défaut »)

```sql
ALTER DATABASE FenrirDb SET ACCELERATED_DATABASE_RECOVERY = ON;   -- prérequis d'Optimized Locking
ALTER DATABASE FenrirDb SET OPTIMIZED_LOCKING = ON;               -- SQL 2025 : TID locking + LAQ
ALTER DATABASE FenrirDb SET READ_COMMITTED_SNAPSHOT ON;           -- lecteurs jamais bloqués
ALTER DATABASE FenrirDb SET QUERY_STORE = ON (OPERATION_MODE = READ_WRITE);
ALTER DATABASE FenrirDb ADD FILEGROUP fenrir_mod CONTAINS MEMORY_OPTIMIZED_DATA;
ALTER DATABASE FenrirDb ADD FILE (NAME='fenrir_mod', FILENAME='/var/opt/mssql/data/fenrir_mod')
    TO FILEGROUP fenrir_mod;
```

Optimized Locking est **désactivé par défaut** sur SQL Server 2025 « boîte » : on l'active explicitement. Effet concret pour Fenrir : les rafales d'`UPDATE` du write-behind ne tiennent plus des milliers de verrous de ligne jusqu'au commit — un verrou de transaction (TID) + qualification optimiste (LAQ) —, donc moins de mémoire de verrous, pas d'escalade, pas de convois entre le flusher et les lectures de l'admin. **Caveat structurant** : les lignes contenant des colonnes LOB (`nvarchar(max)`, `varbinary(max)`, **`json`**) retombent sur le verrouillage classique — d'où la règle §12.5 : *aucune colonne LOB sur les tables chaudes*.

### 12.2 Schémas : la carte des températures

| Schéma | Température | Contenu | Implémentation |
| :-- | :-- | :-- | :-- |
| `auth` | froide | comptes, credentials, MFA, bans, journal de connexions | disque, RCSI |
| `game` | tiède-chaude | personnages, inventaires, progression, monnaies | disque + Optimized Locking, écrit **uniquement** par batchs TVP |
| `world` | froide, lecture | templates d'items, NPC, tables de loot, cartes | disque ; servie via FrozenCache au boot (§11.4) |
| `social` | tiède | guildes, amis, courrier, hôtel des ventes | disque ; transactions explicites (économie) |
| `runtime` | brûlante | tickets de session, annuaire des shards, présence | **In-Memory OLTP `SCHEMA_ONLY`** + procédures nativement compilées |
| `telemetry` | append-only | événements économie/combat/anti-cheat | **clustered columnstore**, partitionné par jour |
| `admin` | froide | `SchemaVersions`, `ErrorCatalog`, configuration | disque |

### 12.3 Conventions de contrat (chaque procédure = une API publiée)

- **Nommage** : `usp_<Agrégat>_<Action>` (`usp_Character_PersistBatch`), types TVP `tvp_<Ligne>`, un objet = un fichier dans `database/`, rangé par schéma.
- **En-tête obligatoire** : paramètres, *result set colonne par colonne dans l'ordre* (c'est le contrat ordinal des `[GenerateDto]`, §11.2), erreurs pouvant être levées, idempotence oui/non. L'en-tête est la documentation que lit le développeur C#.
- **Prologue systématique** : `SET NOCOUNT ON;` + `SET XACT_ABORT ON;` sur toute proc qui écrit.
- **Erreurs** : `THROW 50xxx` avec plages réservées (`501xx` auth, `502xx` game, `503xx` social…), catalogue dans `admin.ErrorCatalog`. Côté C#, `SqlException.Number` est mappé vers des erreurs métier typées dans `Fenrir.Data` — jamais de parsing de message.
- **Interdits** : `MERGE` (pièges de concurrence connus — on écrit `UPDATE … JOIN` puis `INSERT … WHERE NOT EXISTS`), `SELECT *`, curseurs sur chemin chaud, triggers sur tables chaudes, SQL dynamique.
- **Sécurité** : deux logins de service, `fenrir_login_svc` (EXECUTE sur `auth` + `runtime`) et `fenrir_game_svc` (EXECUTE sur `game`, `world`, `social`, `runtime`, `telemetry`). **Aucun droit de table** : la surface d'attaque SQL de Fenrir, c'est la liste exacte de ses procédures, rien d'autre.

### 12.4 Le schéma `runtime` : In-Memory OLTP + compilation native

```sql
-- database/30_tables/runtime/SessionTickets.sql
CREATE TABLE runtime.SessionTickets
(
    TicketId     UNIQUEIDENTIFIER NOT NULL,
    AccountId    INT              NOT NULL,
    CharacterId  INT              NULL,
    ShardId      TINYINT          NOT NULL,
    Secret       BINARY(32)       NOT NULL,
    ExpiresAtUtc DATETIME2(3)     NOT NULL,
    CONSTRAINT PK_SessionTickets PRIMARY KEY NONCLUSTERED HASH (TicketId)
        WITH (BUCKET_COUNT = 1048576)          -- ≥ 2× pics de tickets vivants, puissance de 2
)
WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
-- SCHEMA_ONLY : zéro I/O disque, zéro log. Un ticket survivant à un failover ne VAUT rien
-- (TTL 15 s) : la durabilité serait un coût pur. Décision documentée : ADR-0009.
```

```sql
-- database/50_procedures/runtime/usp_SessionTicket_Consume.sql
-- Contrat : @TicketId → RS0 { AccountId int, CharacterId int?, Secret binary(32),
--                             ExpiresAtUtc datetime2(3) } | vide si inconnu/expiré. Single-use.
CREATE PROCEDURE runtime.usp_SessionTicket_Consume
    @TicketId UNIQUEIDENTIFIER
WITH NATIVE_COMPILATION, SCHEMABINDING
AS
BEGIN ATOMIC WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE @AccountId INT, @CharacterId INT, @Secret BINARY(32), @Exp DATETIME2(3);

    SELECT @AccountId = AccountId, @CharacterId = CharacterId,
           @Secret    = Secret,    @Exp         = ExpiresAtUtc
    FROM runtime.SessionTickets WHERE TicketId = @TicketId;

    DELETE FROM runtime.SessionTickets WHERE TicketId = @TicketId;   -- lire+détruire : atomique

    IF @AccountId IS NOT NULL AND @Exp > SYSUTCDATETIME()
        SELECT @AccountId AS AccountId, @CharacterId AS CharacterId,
               @Secret AS Secret, @Exp AS ExpiresAtUtc;
END;
```

La procédure est compilée en code natif à sa création : sur ce chemin (le plus fréquent de tout le LoginServer), il n'y a **ni verrou physique, ni latch, ni interprétation T-SQL**. Même traitement pour `usp_SessionTicket_Create`, `usp_GameServer_Heartbeat`, `usp_GameServer_GetDirectory`, et une `usp_SessionTicket_Purge` (balayage des expirés, appelée par timer — `SCHEMA_ONLY` ne se nettoie pas tout seul).

### 12.5 Tables chaudes sur disque : `game`

```sql
-- database/30_tables/game/Characters.sql (extrait signifiant)
CREATE TABLE game.Characters
(
    CharacterId   INT IDENTITY(1,1) NOT NULL,
    AccountId     INT           NOT NULL,
    Name          NVARCHAR(24)  NOT NULL,
    ClassId       TINYINT       NOT NULL,
    Level         SMALLINT      NOT NULL CONSTRAINT DF_Char_Level DEFAULT 1,
    MapId         SMALLINT      NOT NULL,
    PosX REAL NOT NULL, PosY REAL NOT NULL, PosZ REAL NOT NULL,
    Heading       SMALLINT      NOT NULL,
    FlushSequence BIGINT        NOT NULL CONSTRAINT DF_Char_Flush DEFAULT 0,  -- §12.6
    UpdatedAtUtc  DATETIME2(3)  NOT NULL,
    CONSTRAINT PK_Characters PRIMARY KEY CLUSTERED (CharacterId),
    CONSTRAINT UQ_Characters_Name UNIQUE (Name),
    INDEX IX_Characters_Account NONCLUSTERED (AccountId) INCLUDE (Name, ClassId, Level)
);
```

Trois règles y sont lisibles : clé clusterée **étroite et monotone** (les batchs TVP joignent dessus, l'insertion ne fragmente pas) ; index *covering* pour l'unique requête de liste (sélection de personnage) ; **aucune colonne LOB** — la métadonnée flexible (affixes d'objets, cosmétiques) vit dans une table satellite `game.ItemInstanceMetadata` utilisant le **type `json` natif de SQL Server 2025 + index JSON**, hors du chemin verrouillé du flusher. C'est le mariage exact des deux nouveautés 2025 : Optimized Locking sur les tables chaudes compactes, JSON natif indexé sur les tables froides flexibles.

Autres apports SQL Server 2025 exploités, à leur juste place : **Optional Parameter Plan Optimization** sur les procs de recherche à filtres optionnels (`social.usp_Auction_Search` — fini le parameter sniffing des plans « une taille pour tous ») ; hint **`ABORT_QUERY_EXECUTION`** posé par l'exploitation via Query Store sur toute requête ad hoc qui menacerait un shard ; **Resource Governor sur tempdb** pour cloisonner l'analytique de la télémétrie ; Query Store actif sur les secondaires en lecture (rapports GM). Le type `vector`/DiskANN n'a aucun rôle sur ce chemin — noté pour d'éventuels usages méta (recommandation, modération), hors périmètre.

### 12.6 La procédure de batch idempotente (contrat du flusher §10.5)

```sql
-- database/50_procedures/game/usp_Character_PersistBatch.sql
-- Contrat : @Positions game.tvp_CharacterPosition READONLY → aucun result set.
-- Idempotente : rejouer un batch (retry réseau) est strictement neutre.
CREATE PROCEDURE game.usp_Character_PersistBatch
    @Positions game.tvp_CharacterPosition READONLY
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;

    UPDATE c
       SET c.MapId = s.MapId, c.PosX = s.PosX, c.PosY = s.PosY, c.PosZ = s.PosZ,
           c.Heading = s.Heading, c.FlushSequence = s.FlushSequence,
           c.UpdatedAtUtc = SYSUTCDATETIME()
      FROM game.Characters AS c
      JOIN @Positions      AS s ON s.CharacterId = c.CharacterId
     WHERE s.FlushSequence > c.FlushSequence;      -- la garde d'idempotence
END;
```

Cinq cents joueurs, un aller-retour, un plan stable, des verrous TID relâchés ligne à ligne : c'est la signature de tout le couple write-behind + TVP + Optimized Locking.

### 12.7 Migrations : `Fenrir.Tools.DbMigrator`

Un worker AOT d'une centaine de lignes, sans framework : il lit `database/_manifest.txt` (ordre explicite `00_init → 10_schemas → … → 70_seed`), exécute chaque script non encore journalisé dans `admin.SchemaVersions (ScriptName, Sha256, AppliedAtUtc)`, refuse tout script déjà appliqué dont le hash a changé (l'histoire ne se réécrit pas : on ajoute un script correctif), et se termine avec un code de sortie que l'AppHost attend (`WaitForCompletion`, §2.2). Les scripts sont **la** vérité versionnée dans Git ; SSMS ne sert qu'à lire.

---

## 13. Observabilité

### 13.1 Métriques (le tableau de bord d'un shard tient en douze courbes)

| Instrument | Type | Dimensions | Alerte type |
| :-- | :-- | :-- | :-- |
| `fenrir.net.ccu` | gauge | shard | — (capacité) |
| `fenrir.net.packets.in` / `.out` | counter | shard, opclass | dérive brutale = incident/attaque |
| `fenrir.net.bytes.in` / `.out` | counter | shard | — |
| `fenrir.net.dispatch.duration` | histogram | opclass | p99 > 200 µs (voie inline) |
| `fenrir.net.protocol_violations` | counter | reason | > 0 soutenu = scan/exploit |
| `fenrir.net.ratelimit_hits` | counter | opclass | capteur bots (§8.5) |
| `fenrir.net.slow_consumer_kicks` | counter | shard | pics = problème réseau sortant |
| `fenrir.game.tick.duration` | histogram | shard, zone | **p99 > 50 ms = alerte majeure** |
| `fenrir.game.zone.entities` | gauge | shard, zone | équilibrage des zones |
| `fenrir.data.flush.batch_size` / `.duration` | histogram | shard | dérive = SQL en souffrance |
| `caerius.*` (fournies par CaeriusNet) | — | procédure | durée/échecs/cache par proc |

### 13.2 Traces & logs — la règle de sobriété

> **Décision D-08 — Jamais d'`Activity` par paquet.** À 500 k paquets/s, tracer chaque paquet *est* l'incident de performance. Les spans sont réservés aux opérations rares et longues : login complet (avec le span SQL CaeriusNet imbriqué — un trace unique du `LoginRequest` au `HandoverResponse`), consommation de ticket, flush de persistance, transaction d'économie. Le hot path est observé **par métriques** (histogrammes), les anomalies par compteurs dimensionnés, et le détail par capture ciblée activable à chaud (dump des N derniers frames d'une session suspecte, ring buffer en mémoire).

Logs : `LoggerMessage` source-généré exclusivement (AOT + zéro boxing), niveau `Information` muet sur le hot path. Health checks (§2.3) : listener TCP vivant, `admin.usp_Ping` < 100 ms, watchdog de tick (un tick absent > 2 périodes = unhealthy → Aspire/orchestrateur redémarre le shard).

---

## 14. Tests & Benchmarks

### 14.1 La pyramide Fenrir

| Étage | Projet | Contenu | Vitesse |
| :-- | :-- | :-- | :-- |
| Unitaires purs | `Fenrir.Domain.Tests` | règles de jeu, plausibilité mouvement, formules | ms, massifs |
| Contrats protocole | `Fenrir.Contracts.Tests` | round-trips propriété-basés + **golden tests binaires** (§7.3) | ms |
| Réseau | `Fenrir.Network.Tests` | `FrameDecoder` sous fragmentation systématique (coupure à *chaque* frontière d'octet), trames malformées/hostiles, machine à états de session | ms |
| Contrats data | `Fenrir.Data.Tests` | chaque proc exécutée contre SQL 2025 conteneurisé, chaque `[GenerateDto]`/`[GenerateTvp]` validé colonne à colonne (I-04) | s |
| Intégration | `Fenrir.IntegrationTests` | `Aspire.Hosting.Testing` : l'AppHost réel démarre SQL + migrator + Login + un shard ; un client bot joue le scénario **login → MFA → handover → ticket single-use → EnterWorld → mouvement → déconnexion → vérif persistance** | min |

### 14.2 Le point critique : tester le générateur, pas seulement le code

Le générateur §7 fabrique le protocole : ses golden tests binaires sont le garde-fou I-06. Tout changement d'octets émis exige la mise à jour *explicite* d'un snapshot dans la même PR — le diff binaire devient un objet de revue, comme un changement d'API publique.

### 14.3 Chaos ciblé

Trois pannes sont rejouées en intégration à chaque nightly : **SQL indisponible 30 s** (le shard continue de jouer, le flusher accumule et rattrape, aucune déconnexion) ; **kill -9 d'un shard** (limbo §8.3, reconnexion sur un autre shard via ticket, perte bornée aux 5 s de write-behind, économie intacte) ; **client hostile** (fuzzer réseau branché sur le port de jeu : l'issue attendue est *toujours* une déconnexion propre + compteur, jamais une exception non gérée).

### 14.4 Benchmarks-gates (BenchmarkDotNet, exécutés en CI sur build AOT)

| Benchmark | Gate |
| :-- | :-- |
| `FrameDecode_MoveRequest` | **0 B/op**, < 80 ns |
| `Dispatch_Inline_MoveRequest` (parse + gate + handler + enqueue zone) | **0 B/op**, < 250 ns |
| `Send_MoveResponse` (header + write + AES-GCM in place) | **0 B/op** hors pipe, < 400 ns |
| `AoiGrid_Move_10kEntities` | 0 B/op amorti |
| `TvpBuild_500Positions` | ≤ 2 allocations (liste + enumerator), documentées `// ALLOC:` |

Une régression de gate **casse le build** : l'invariant I-02 n'est pas une intention, c'est un test.

---

## 15. Règles d'or consolidées

1. Aucune réflexion runtime ; tout ce qui est répétitif est **généré** ; les fichiers générés ne sont ni commités ni édités.
2. Le format filaire est un contrat de **bits** (little-endian explicite, versionné, golden-testé) — jamais la projection mémoire d'un struct.
3. Un paquet est matérialisé **par valeur** avant tout `await` ; le buffer réseau ne fuit jamais hors du parse.
4. Handler inline = microsecondes, zéro I/O ; handler async = awaité par la boucle de session ; la **simulation n'appartient qu'au tick de zone**, unique écrivain de l'état monde.
5. Le serveur est autoritaire : toute donnée client est une *intention* à valider par le Domain.
6. Le C# ne connaît de SQL que des **noms de procédures** via CaeriusNet ; le result set de la procédure est le contrat ; les DTO s'y conforment ordinalement.
7. Les écritures gameplay passent par **batchs TVP idempotents** (write-behind) ; l'économie passe par transactions explicites et flush immédiat ; rien d'autre n'est retryable.
8. `runtime` = In-Memory OLTP `SCHEMA_ONLY` + compilation native ; tables chaudes disque = compactes, sans LOB, sous Optimized Locking ; le flexible (json natif indexé) vit en satellite.
9. Le hot path est observé par métriques, jamais par spans ; `tick p99 ≤ 50 ms` est l'unique définition de « le shard va bien ».
10. Chaque allocation est justifiée (`// ALLOC:`), chaque gate de benchmark est bloquante, chaque décision structurante est un ADR.

---

## Annexe A — Budget de latence d'un paquet (voie inline, cible)

```text
kernel → PipeWriter RX (copie unique kernel→pool)        ~ contrôlé par l'OS
TryReadFrame (header stackalloc + validations)             <  30 ns
ValidateSequence + SessionStateGate + rate limit           <  20 ns
MessageFactory.TryCreate (AES-GCM 32 o + TryRead)          < 150 ns
Handler inline (validation Domain + enqueue zone)          < 100 ns
──────────────────────────────────────────────────────────
Total applicatif par paquet                                < ~300 ns, 0 alloc
```

À 300 ns/paquet, un seul cœur absorbe ~3 M paquets/s de traitement applicatif ; la limite réelle devient le réseau et la simulation — exactement là où elle doit être.

## Annexe B — Dimensionnement mémoire par session (ordre de grandeur)

| Poste | Estimation |
| :-- | :-- |
| Pipes RX+TX (segments poolés, régime nominal) | 16–64 Ko |
| `ClientSession` + sécurité + stats | < 1 Ko |
| État joueur en zone (entité + AOI) | 1–4 Ko |
| **Total / CCU** | **~20–70 Ko** → 10 k CCU ≈ 0,2–0,7 Go hors monde |

Les plafonds `pauseWriterThreshold` (512 Ko RX / 128 Ko TX) bornent le pire cas par session : la mémoire du serveur est **prévisible par construction**, pas par espoir.

---

*Fin du document de référence v1.0. Chaque section majeure (§5, §6, §10, §12) a vocation à être détaillée dans un document dédié (`docs/`), en gardant celui-ci comme contrat d'ensemble.*
