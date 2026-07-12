# Fenrir — Architecture Technique MMORPG 2026

> **Version :** Draft v0.1  
> **Stack :** C# 14 · .NET 10 Native AOT · Aspire.NET · SQL Server 2025 · CaeriusNet · System.IO.Pipelines

> **Important**  
> Un document exhaustif de plusieurs centaines de pages dépasse les limites d'une seule réponse. Ce document constitue une fondation volontairement très dense, conçue pour être étendue.

---

# 1. Vision

Fenrir est conçu selon les principes suivants :

- Native AOT First
- Data First
- Contract First
- Protocol First
- Zero Reflection
- Zero Allocation sur le Hot Path
- Source Generator First
- SQL First
- Strong Typing Everywhere
- Horizontal Scalability
- Observability by Design

---

# 2. Stack

- C# 14
- .NET 10
- Aspire.NET
- SQL Server 2025
- CaeriusNet
- TCP
- System.IO.Pipelines
- OpenTelemetry
- Native AOT
- Roslyn Source Generators

---

# 3. Architecture de la Solution

```text
Fenrir.slnx

build/
docs/
database/
deployment/
benchmarks/
scripts/
tests/

src/
    Fenrir.AppHost
    Fenrir.ServiceDefaults

    Fenrir.Contracts
    Fenrir.Protocol
    Fenrir.Protocol.Generator

    Fenrir.Network
    Fenrir.Network.Tcp
    Fenrir.Network.Dispatching
    Fenrir.Network.Serialization
    Fenrir.Network.Security
    Fenrir.Network.Compression

    Fenrir.Domain
    Fenrir.Application
    Fenrir.Infrastructure
    Fenrir.Infrastructure.SqlServer
    Fenrir.Infrastructure.CaeriusNet

    Fenrir.LoginServer
    Fenrir.GameServer
    Fenrir.AdminServer

    Fenrir.Tools
```

---

# 4. Pipeline Réseau

TCP Socket

↓

Connection

↓

PipeReader

↓

Frame Decoder

↓

Packet Decoder

↓

MessageFactory

↓

MessageDispatcher

↓

MessageHandler

↓

Application Service

↓

Domain

↓

Response Builder

↓

PipeWriter

---

# 5. Message System

Chaque paquet possède :

- Opcode
- Version
- Flags
- PayloadLength
- SequenceId
- CorrelationId

Les composants :

- MessageFactory
- MessageDispatcher
- IMessageHandler<TRequest>
- IRequest
- IResponse
- INotification

Aucune réflexion.

Tout est généré.

---

# 6. Source Generators

Génération automatique de :

- PacketSerializer
- PacketDeserializer
- Dispatcher
- HandlerRegistry
- OpcodeRegistry
- SQL Wrappers
- Stored Procedure Contracts
- DTO
- ResultSets
- Diagnostics

---

# 7. Sessions

Une session contient uniquement les données vivantes.

- Connection
- Authentication
- Character
- Encryption
- Ping
- Latency
- World State
- Visibility
- Permissions

---

# 8. LoginServer

Responsabilités :

- Authentification
- MFA
- Anti-Bruteforce
- Session Ticket
- Routing
- Sélection du GameServer

---

# 9. GameServer

Responsabilités :

- Simulation
- Combat
- IA
- Inventaire
- Chat
- Movement
- Skills
- Persistence

---

# 10. SQL Server 2025

Architecture Data First.

Schémas :

- auth
- character
- inventory
- item
- guild
- combat
- npc
- world
- runtime
- admin
- telemetry

Uniquement :

- Stored Procedures
- TVP
- Result Sets typés

Jamais de SQL inline.

---

# 11. CaeriusNet

Chaque procédure génère automatiquement :

- Request
- Parameters
- Reader
- Writer
- Executor
- Result
- Validation

---

# 12. Performance

Objectifs :

- zéro allocation pendant le traitement d'un paquet
- Span<T>
- Memory<T>
- ArrayPool<T>
- ObjectPool
- PipeReader/PipeWriter
- readonly record struct
- ValueTask
- CancellationToken

---

# 13. Observabilité

Aspire orchestre :

- SQL
- LoginServer
- GameServer
- Traces
- Metrics
- Logs
- HealthChecks

---

# 14. Base de Données

Organisation :

Schemas
  Tables
  Types
  Views
  Procedures
  Permissions

Index :

- Clustered
- Covering
- Filtered
- Partitionnés

Analyse permanente :

- Plan Cache
- Parameter Sniffing
- Fragmentation
- Cardinalité

---

# 15. Règles d'Or

- Aucune réflexion runtime.
- Aucun SQL inline.
- Tous les protocoles sont versionnés.
- Tous les handlers sont générés.
- Les Domain Services ignorent le réseau.
- Les MessageHandlers ignorent SQL.
- Les procédures stockées constituent le contrat de vérité.
- Les Source Generators éliminent le code répétitif.
- Le hot path est mesuré par BenchmarkDotNet.
- Chaque allocation est justifiée.

---

# Conclusion

Fenrir vise une architecture pérenne, fortement typée, orientée performances et maintenance, privilégiant la génération de code, les contrats explicites et une séparation stricte des responsabilités.

Ce document est destiné à servir de socle à une documentation beaucoup plus vaste couvrant en détail chaque sous-système.
