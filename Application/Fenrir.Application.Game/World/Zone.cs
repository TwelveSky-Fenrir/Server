using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Channels;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Guilds;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Movement;
using Fenrir.Application.Game.Pets;
using Fenrir.Application.Game.Progression;
using Fenrir.Application.Game.Quests;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Skills;
using Fenrir.Application.Game.Social.Chat;
using Fenrir.Application.Game.Social.Mentor;
using Fenrir.Application.Game.Social.Pshop;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tribes;
using Fenrir.Application.Game.World.Geometry;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Application.Game.World.Monsters;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Framing;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.World;

/// <summary>
///     One zone actor per hosted map. Every player position, the AOI grid, and the dirty-tracker marking are
///     touched only from this zone's tick (<see cref="RunAsync" /> -&gt; <see cref="Tick" />) -- everything else
///     only ever calls <see cref="Post" /> and waits for the next tick.
/// </summary>
/// <remarks>
///     The tick runs in stages: drain inbox -&gt; simulate (whole 500 ms legacy ticks via
///     <see cref="SimulationTickAccumulator" />) -&gt; periodic keep-alive rebroadcast (avatars every 3.5 s,
///     monsters/items every 5 s, see <see cref="SimulationClock" />).
/// </remarks>
public sealed class Zone(
    short mapId,
    GameServerOptions options,
    MovementRules movementRules,
    DirtyTracker<int> dirtyTracker,
    IReadOnlyList<ISimulationSystem> simulationSystems,
    ILogger<Zone> logger,
    WorldDataCache worldData,
    IRandomSource? randomSource = null,
    QuestCatalog? questCatalog = null) : IZoneActor
{
    private readonly SimulationTickAccumulator _accumulator = new();

    /// <summary>
    ///     Op 94/95 (<c>ContinueSkillStat</c>/<c>ContinueSkillUse</c>) auto-buff registration + activation
    ///     mirror. See <see cref="ApplyAutoBuffCommand" />'s remarks for what this stub does/doesn't mirror yet.
    /// </summary>
    private readonly Channel<AutoBuffZoneCommand> _autoBuffInbox =
        Channel.CreateBounded<AutoBuffZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Op 97/111 (<c>PlaytimeBuff</c>/<c>RankBuff</c>) self-mutation mirror. See
    ///     <see cref="ApplyAvatarBuffCommand" />'s remarks for what this stub does/doesn't mirror yet.
    /// </summary>
    private readonly Channel<AvatarBuffZoneCommand> _avatarBuffInbox =
        Channel.CreateBounded<AvatarBuffZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>Op 129 <c>DrinkBottle</c> self-mutation mirror. See <see cref="ApplyDrinkBottleCommand" />'s remarks.</summary>
    private readonly Channel<DrinkBottleZoneCommand> _bottleInbox =
        Channel.CreateBounded<DrinkBottleZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Raw local/shout/tribe chat sends that need this zone's tick-owned <see cref="_grid" />/
    ///     <see cref="_players" /> to resolve their audience -- every other chat channel (whisper/party/guild/
    ///     world/notices) fans out directly via <c>ZoneRegistry</c>/<see cref="Players" /> without touching the tick thread.
    /// </summary>
    private readonly Channel<ChatZoneCommand> _chatInbox =
        Channel.CreateBounded<ChatZoneCommand>(
            new BoundedChannelOptions(2048) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Enqueued by <see cref="TryClaimGroundItem" /> (any thread) so the despawn broadcast (needs
    ///     <see cref="_grid" />, tick-thread-only) happens from the tick, never the claiming handler's thread.
    /// </summary>
    private readonly ConcurrentQueue<GroundItemEntity> _claimedGroundItemDespawns = new();

    /// <summary>Raw, unvalidated CZ_PROCESS_ATTACK_SEND requests, resolved entirely on the tick thread (zero-SQL combat).</summary>
    private readonly Channel<CombatCommand> _combatInbox = Channel.CreateBounded<CombatCommand>(
        new BoundedChannelOptions(4096) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Op 90/139 (<c>CostumeState</c>/<c>CostumeVisibility</c>) self-mutation mirror. See
    ///     <see cref="ApplyCostumeCommand" />'s remarks for what this stub does/doesn't mirror yet.
    /// </summary>
    private readonly Channel<CostumeZoneCommand> _costumeInbox =
        Channel.CreateBounded<CostumeZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Enqueued by <see cref="TryDamageMonster" /> (any thread) on a killing blow, drained by
    ///     <see cref="Monsters.MonsterSpawnScheduler" /> on this zone's own next tick (single-writer preserved).
    /// </summary>
    private readonly ConcurrentQueue<DeadMonsterEvent> _deadMonsters = new();

    /// <summary>
    ///     Op 103/104/105 (<c>FishingLine</c>/<c>FishingProgress</c>/<c>FishingCatch</c>) shared state-machine
    ///     mirror. See <see cref="ApplyFishingCommand" />'s remarks -- awaiting new PlayerRuntimeState fields.
    /// </summary>
    private readonly Channel<FishingZoneCommand> _fishingInbox =
        Channel.CreateBounded<FishingZoneCommand>(
            new BoundedChannelOptions(512) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly AoiGrid _grid = new(options.AoiCellSize);

    /// <summary>Tick-owned only -- see <see cref="_groundItems" />'s remarks.</summary>
    private readonly Dictionary<int, TimeSpan> _groundItemLastRebroadcast = new();

    // Populated/expired only by this zone's own tick; claimed (removed) via an atomic compare-and-remove
    // callable from any thread -- see TryClaimGroundItem's remarks for why pickup is the one exception.
    private readonly ConcurrentDictionary<int, GroundItemEntity> _groundItems = new();

    /// <summary>
    ///     Already-durably-persisted guild-membership mirrors, posted by <c>GuildActionHandler</c> onto this
    ///     character's own hosting zone, whether the target is the actor or a different guild member.
    /// </summary>
    private readonly Channel<GuildMembershipZoneCommand> _guildInbox =
        Channel.CreateBounded<GuildMembershipZoneCommand>(
            new BoundedChannelOptions(512) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Op 118 <c>HeroRanking</c> throttle-timestamp mirror -- the ranking query itself is read-only and answered
    ///     directly by the handler.
    /// </summary>
    private readonly Channel<HeroRankingQueryZoneCommand> _heroRankingInbox =
        Channel.CreateBounded<HeroRankingQueryZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly Channel<ZoneCommand> _inbox = Channel.CreateBounded<ZoneCommand>(
        new BoundedChannelOptions(8192) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Separate inbox for already-validated-and-SQL-durable inventory results, posted by
    ///     <c>GenericActionHandler</c>. Kept out of <see cref="_inbox" />/<see cref="ZoneCommand" />'s union so
    ///     this concern stays additive-only. Drop-on-full is safe here: the SQL write already committed, so a
    ///     dropped command only leaves the in-memory mirror stale (self-heals on next world entry).
    /// </summary>
    private readonly Channel<InventoryZoneCommand> _inventoryInbox = Channel.CreateBounded<InventoryZoneCommand>(
        new BoundedChannelOptions(2048) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly KeyValuePair<string, object?> _mapTag = ZoneTickMetrics.MapTag(mapId);

    /// <summary>
    ///     Mirrors one cross-character field write (mentor bonding, posted by <c>MentorStartHandler</c>) onto
    ///     the target character's own hosting zone rather than mutating it directly from another thread.
    /// </summary>
    private readonly Channel<MentorZoneCommand> _mentorInbox =
        Channel.CreateBounded<MentorZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>Already-validated, already-SQL-durable daily-mission claims, posted by <c>DailyMissionHandler</c>.</summary>
    private readonly Channel<MissionZoneCommand> _missionInbox =
        Channel.CreateBounded<MissionZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Released once per queued grant so <c>MonsterLootFlushHost</c> can flush as soon as a grant arrives
    ///     instead of waiting up to a full flush interval, shrinking the in-memory-only loss window to roughly
    ///     one SQL round trip.
    /// </summary>
    private readonly SemaphoreSlim _moneyGrantSignal = new(0, int.MaxValue);

    // Same ConcurrentDictionary posture as _players -- the tick is the sole writer for spawn/AI mutation, but
    // TryDamageMonster is a deliberate exception letting a combat packet handler thread apply damage directly
    // via an atomic Interlocked path on MonsterEntity itself.
    private readonly ConcurrentDictionary<int, MonsterEntity> _monsters = new();

    /// <summary>
    ///     Op 87/113 (<c>MountState</c>/<c>MountAbsorb</c>) self-mutation mirror. See
    ///     <see cref="ApplyMountCommand" />'s remarks for what this stub does/doesn't mirror yet.
    /// </summary>
    private readonly Channel<MountZoneCommand> _mountInbox =
        Channel.CreateBounded<MountZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Server-initiated monster-kill money grants, queued rather than awaited inline because
    ///     <see cref="Tick" /> is fully synchronous and must never block on SQL I/O; drained by
    ///     <see cref="MonsterLootFlushHost" /> from any thread.
    /// </summary>
    private readonly ConcurrentQueue<(int CharacterId, long Amount)> _pendingMoneyGrants = new();

    // ConcurrentDictionary, not a plain Dictionary: the tick is the sole writer, but the write-behind flush
    // callback and the directory-heartbeat CCU count both read this from other threads.
    private readonly ConcurrentDictionary<int, PlayerRuntimeState> _players = new();

    /// <summary>
    ///     Fire-and-forget, purely cosmetic PShop-stall mirrors posted by <c>BuyShopItemHandler</c> onto the
    ///     seller's own hosting zone after a purchase already durably committed.
    /// </summary>
    private readonly Channel<PshopZoneCommand> _pshopInbox =
        Channel.CreateBounded<PshopZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Null (production default) builds a private one from <paramref name="worldData" /> instead of the
    ///     process-wide singleton <see cref="ZoneRegistry" /> owns, so pre-existing test call sites that
    ///     construct a <see cref="Zone" /> directly keep compiling unchanged.
    /// </summary>
    private readonly QuestCatalog _questCatalog = questCatalog ?? new QuestCatalog(worldData);

    /// <summary>Already-validated, already-SQL-durable quest-state transitions, posted by <c>QuestProgressHandler</c>.</summary>
    private readonly Channel<QuestZoneCommand> _questInbox = Channel.CreateBounded<QuestZoneCommand>(
        new BoundedChannelOptions(512) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>Combat/skill RNG -- <see cref="SystemRandomSource" /> in production, injectable for deterministic tests.</summary>
    private readonly IRandomSource _random = randomSource ?? SystemRandomSource.Instance;

    /// <summary>
    ///     Op 157 <c>RuneSocket</c> self-mutation mirror. See <see cref="ApplyRuneSocketCommand" />'s remarks
    ///     for what this stub does/doesn't mirror yet.
    /// </summary>
    private readonly Channel<RuneSocketZoneCommand> _runeInbox =
        Channel.CreateBounded<RuneSocketZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Already-validated, already-SQL-durable skill learn/upgrade results, posted by
    ///     <c>GenericActionHandler</c>. Kept as its own channel rather than folded into
    ///     <see cref="_inventoryInbox" />'s union, same additive-only rationale.
    /// </summary>
    private readonly Channel<SkillZoneCommand> _skillInbox = Channel.CreateBounded<SkillZoneCommand>(
        new BoundedChannelOptions(1024) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Op 153 <c>StellarCoreState</c> self-mutation mirror, same shape as <see cref="_costumeInbox" />. See
    ///     <see cref="ApplyStellarCoreCommand" />'s remarks.
    /// </summary>
    private readonly Channel<StellarCoreZoneCommand> _stellarCoreInbox =
        Channel.CreateBounded<StellarCoreZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Already-decided tribe-progress self-mutations posted by <c>TribeActionHandler</c> for the actor's own hosting
    ///     zone.
    /// </summary>
    private readonly Channel<TribeProgressZoneCommand> _tribeInbox =
        Channel.CreateBounded<TribeProgressZoneCommand>(
            new BoundedChannelOptions(512) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     This zone's own monotonic simulated clock, the sum of every elapsed span fed to <see cref="Tick" />.
    ///     Periodic cadences are measured against this, not wall clock, so a test can drive simulated hours
    ///     through <see cref="Tick" /> in microseconds.
    /// </summary>
    private TimeSpan _clock;

    private int _groundItemServerIndexSeed;
    private int _groundItemUniqueNumberSeed;

    private int _monsterUniqueNumberSeed;

    /// <summary>The legacy map this actor simulates -- its key in <see cref="ZoneRegistry" />.</summary>
    public short MapId { get; } = mapId;

    public int PlayerCount => _players.Count;

    public int MonsterCount => _monsters.Count;

    public int GroundItemCount => _groundItems.Count;

    /// <summary>
    ///     Read-only enumeration for <see cref="ISimulationSystem" />s running on this zone's own tick thread. A
    ///     system may mutate the yielded <see cref="PlayerRuntimeState" /> instances directly, but must never
    ///     add/remove entries here.
    /// </summary>
    public IEnumerable<PlayerRuntimeState> Players => _players.Values;

    /// <summary>
    ///     Consumed by <see cref="HandleMove" /> via <see cref="MovementRules.IsPlausible" /> for terrain-aware
    ///     movement validation. Null (logged, not a startup crash) when the <c>.WM</c> file is absent -- the
    ///     legacy game-data tree is an external asset not committed to the repo, so its absence must not block
    ///     the zone from ticking; validation degrades to speed-only in that case.
    /// </summary>
    public ZoneGeometry? Geometry { get; } = TryLoadGeometry(mapId, options, logger);

    internal IEnumerable<MonsterEntity> MonstersSnapshot => _monsters.Values;

    /// <summary>
    ///     Enqueues a command for the next tick. Never blocks: a full inbox drops the write rather than stall
    ///     whichever session thread posted it -- a dropped Move is simply superseded by the client's next one.
    /// </summary>
    public bool Post(in ZoneCommand command)
    {
        return _inbox.Writer.TryWrite(command);
    }

    public bool PostInventoryCommand(in InventoryZoneCommand command)
    {
        return _inventoryInbox.Writer.TryWrite(command);
    }

    /// <summary>
    ///     Posts <paramref name="command" /> (its <see cref="InventoryZoneCommand.Applied" /> is overwritten --
    ///     leave it default) and waits until this zone's tick has actually mirrored it, not merely accepted it.
    ///     Every economy-affecting handler must call this (never the bare <see cref="PostInventoryCommand" />)
    ///     while holding <see cref="PlayerRuntimeState.EconomyActionLock" /> -- see that property's remarks for
    ///     the duplication race this closes. A timeout still returns true: the SQL write is already durable, a
    ///     timed-out mirror just stays stale until next world entry.
    /// </summary>
    public async Task<bool> PostInventoryCommandAndWaitAsync(InventoryZoneCommand command, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostInventoryCommand(in withSignal))
            return false; // inbox full -- caller logs this; nothing to wait for.

        try
        {
            await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // SQL is already durable; only the in-memory mirror timing is affected.
        }

        return true;
    }

    public bool PostSkillCommand(in SkillZoneCommand command)
    {
        return _skillInbox.Writer.TryWrite(command);
    }

    public bool PostMentorCommand(in MentorZoneCommand command)
    {
        return _mentorInbox.Writer.TryWrite(command);
    }

    public bool PostGuildCommand(in GuildMembershipZoneCommand command)
    {
        return _guildInbox.Writer.TryWrite(command);
    }

    /// <summary>
    ///     Same contract as <see cref="PostInventoryCommandAndWaitAsync" />. Callers acting on the requester's
    ///     own guild membership (anything that also touches money) must call this while holding
    ///     <see cref="PlayerRuntimeState.EconomyActionLock" />; callers mirroring a different, possibly-offline
    ///     member may use the bare <see cref="PostGuildCommand" /> instead.
    /// </summary>
    public async Task<bool> PostGuildCommandAndWaitAsync(GuildMembershipZoneCommand command,
        CancellationToken ct, TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostGuildCommand(in withSignal))
            return false;

        try
        {
            await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // SQL is already durable; only the in-memory mirror timing is affected.
        }

        return true;
    }

    public bool PostTribeProgressCommand(in TribeProgressZoneCommand command)
    {
        return _tribeInbox.Writer.TryWrite(command);
    }

    /// <summary>
    ///     Same contract as <see cref="PostInventoryCommandAndWaitAsync" />. Must be called while holding
    ///     <see cref="PlayerRuntimeState.EconomyActionLock" /> for any action that also debits money/CP.
    /// </summary>
    public async Task<bool> PostTribeProgressCommandAndWaitAsync(TribeProgressZoneCommand command,
        CancellationToken ct, TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostTribeProgressCommand(in withSignal))
            return false;

        try
        {
            await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // SQL is already durable; only the in-memory mirror timing is affected.
        }

        return true;
    }

    private bool PostQuestCommand(in QuestZoneCommand command)
    {
        return _questInbox.Writer.TryWrite(command);
    }

    /// <summary>
    ///     Same contract as <see cref="PostInventoryCommandAndWaitAsync" />. Callers must already hold
    ///     <see cref="PlayerRuntimeState.EconomyActionLock" /> and leave <see cref="QuestZoneCommand.Applied" />
    ///     at its default -- overwritten here.
    /// </summary>
    public async Task<bool> PostQuestCommandAndWaitAsync(QuestZoneCommand command, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostQuestCommand(in withSignal))
            return false;

        try
        {
            await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // SQL is already durable; only the in-memory mirror timing is affected.
        }

        return true;
    }

    private bool PostMissionCommand(in MissionZoneCommand command)
    {
        return _missionInbox.Writer.TryWrite(command);
    }

    /// <summary>
    ///     Same contract as <see cref="PostQuestCommandAndWaitAsync" />. Callers must already hold
    ///     <see cref="PlayerRuntimeState.EconomyActionLock" /> and leave
    ///     <see cref="Progression.MissionZoneCommand.Applied" /> at its default -- overwritten here.
    /// </summary>
    public async Task<bool> PostMissionCommandAndWaitAsync(MissionZoneCommand command,
        CancellationToken ct, TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostMissionCommand(in withSignal))
            return false;

        try
        {
            await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // SQL is already durable; only the in-memory mirror timing is affected.
        }

        return true;
    }

    public bool PostCombatCommand(in CombatCommand command)
    {
        return _combatInbox.Writer.TryWrite(command);
    }

    public bool PostChatCommand(in ChatZoneCommand command)
    {
        return _chatInbox.Writer.TryWrite(command);
    }

    public bool PostPshopCommand(in PshopZoneCommand command)
    {
        return _pshopInbox.Writer.TryWrite(command);
    }

    public bool PostDrinkBottleCommand(in DrinkBottleZoneCommand command)
    {
        return _bottleInbox.Writer.TryWrite(command);
    }

    public bool PostHeroRankingQueryCommand(in HeroRankingQueryZoneCommand command)
    {
        return _heroRankingInbox.Writer.TryWrite(command);
    }

    public bool PostFishingCommand(in FishingZoneCommand command)
    {
        return _fishingInbox.Writer.TryWrite(command);
    }

    /// <summary>Same contract as <see cref="PostInventoryCommandAndWaitAsync" />.</summary>
    public async Task<bool> PostFishingCommandAndWaitAsync(FishingZoneCommand command, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostFishingCommand(in withSignal))
            return false;

        try
        {
            await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }

        return true;
    }

    public bool PostMountCommand(in MountZoneCommand command)
    {
        return _mountInbox.Writer.TryWrite(command);
    }

    public bool PostCostumeCommand(in CostumeZoneCommand command)
    {
        return _costumeInbox.Writer.TryWrite(command);
    }

    public bool PostStellarCoreCommand(in StellarCoreZoneCommand command)
    {
        return _stellarCoreInbox.Writer.TryWrite(command);
    }

    public bool PostAvatarBuffCommand(in AvatarBuffZoneCommand command)
    {
        return _avatarBuffInbox.Writer.TryWrite(command);
    }

    public bool PostRuneSocketCommand(in RuneSocketZoneCommand command)
    {
        return _runeInbox.Writer.TryWrite(command);
    }

    /// <summary>
    ///     Same contract as <see cref="PostInventoryCommandAndWaitAsync" />. Callers must already hold
    ///     <see cref="PlayerRuntimeState.EconomyActionLock" /> and leave <see cref="RuneSocketZoneCommand.Applied" />
    ///     at its default -- overwritten here.
    /// </summary>
    public async Task<bool> PostRuneSocketCommandAndWaitAsync(RuneSocketZoneCommand command, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostRuneSocketCommand(in withSignal))
            return false;

        try
        {
            await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }

        return true;
    }

    public bool PostAutoBuffCommand(in AutoBuffZoneCommand command)
    {
        return _autoBuffInbox.Writer.TryWrite(command);
    }

    public bool TryGetPlayer(int characterId, out PlayerRuntimeState? state)
    {
        return _players.TryGetValue(characterId, out state);
    }

    public bool TryGetMonster(int serverIndex, out MonsterEntity? monster)
    {
        return _monsters.TryGetValue(serverIndex, out monster);
    }

    /// <summary>Tick-thread-only: <see cref="_grid" /> itself is not thread-safe.</summary>
    internal IEnumerable<int> NeighborsOfPosition(float x, float z)
    {
        return _grid.Neighbors(_grid.CellOf(x, z));
    }

    internal uint NextMonsterUniqueNumber()
    {
        return unchecked((uint)Interlocked.Increment(ref _monsterUniqueNumberSeed));
    }

    /// <summary>Tick-owned caller only (<see cref="Monsters.MonsterSpawnScheduler" />).</summary>
    internal void SpawnMonster(MonsterEntity monster)
    {
        monster.LastRebroadcastAt = _clock;
        _monsters[monster.ServerIndex] = monster;
        BroadcastMonsterAction(monster, 1); // action=1 on B_MONSTER_ACTION_RECV at creation
    }

    /// <summary>
    ///     Safe from any thread (see <see cref="MonsterEntity.TakeDamage" />'s remarks). On the killing blow,
    ///     atomically removes the monster from the live pool and queues a <see cref="DeadMonsterEvent" /> for
    ///     this zone's own next tick to process -- never processed inline here.
    /// </summary>
    public bool TryDamageMonster(int serverIndex, int amount, int? attackerCharacterId, out bool died,
        out int remainingLife)
    {
        if (!_monsters.TryGetValue(serverIndex, out var monster))
        {
            died = false;
            remainingLife = 0;
            return false;
        }

        died = monster.TakeDamage(amount, out remainingLife);
        if (died)
        {
            _monsters.TryRemove(serverIndex, out _);
            _deadMonsters.Enqueue(new DeadMonsterEvent(monster, attackerCharacterId));
        }

        return true;
    }

    internal bool TryDequeueDeadMonster(out DeadMonsterEvent? deadMonster)
    {
        return _deadMonsters.TryDequeue(out deadMonster);
    }

    /// <summary>
    ///     Tick-owned caller only -- sets the transient <see cref="MonsterAiState.Dead" /> value and broadcasts the final
    ///     (LifeValue == 0) frame.
    /// </summary>
    internal void BroadcastMonsterDeath(MonsterEntity monster)
    {
        monster.AiState = MonsterAiState.Dead;
        BroadcastMonsterAction(monster, 0);
    }

    /// <summary>
    ///     AI-initiated MvP attack -- the monster's own AI calls this directly, never via a client packet
    ///     (<c>S07_MyGame05.cpp:3961</c>). Runs on this zone's own tick thread.
    /// </summary>
    internal void ResolveMonsterAttack(MonsterEntity monster, int targetCharacterId)
    {
        if (!_players.TryGetValue(targetCharacterId, out var target) || target is null)
            return;

        var defenderSnapshot = ToCombatantSnapshot(target);
        var outcome = MonsterCombatResolver.ResolveMvpAttack(monster, defenderSnapshot, _clock, _random);
        if (outcome.Rejected)
            return;

        var response = new AttackResponse
        {
            AttackInfo = new AttackForProtocol
            {
                Case = 4,
                ServerIndex1 = monster.ServerIndex,
                UniqueNumber1 = monster.UniqueNumber,
                ServerIndex2 = target.CharacterId,
                UniqueNumber2 = target.UniqueNumber,
                SenderLocation = [monster.PosX, monster.PosY, monster.PosZ],
                AttackActionValue1 = 1,
                AttackActionValue2 = 0,
                AttackActionValue3 = 0,
                AttackActionValue4 = 0,
                AttackResultValue = outcome.Hit ? 1 : 0,
                AttackCriticalExist = outcome.Critical ? 1 : 0,
                AttackElementDamage = outcome.ElementDamage,
                AttackViewDamageValue = outcome.DamageApplied,
                AttackRealDamageValue = outcome.DamageApplied
            }
        };

        var recipients = new HashSet<int> { target.CharacterId };
        foreach (var id in _grid.Neighbors(target.CurrentCell)) recipients.Add(id);
        BroadcastAttackResult(recipients, response);

        if (!outcome.Hit)
            return;

        target.Life -= outcome.DamageApplied;
        dirtyTracker.MarkDirty(target.CharacterId, DirtyFlags.Vitals);

        if (target.Life <= 0)
            ApplyDeath(target.CharacterId, DeathCause.MonsterKill);
    }

    internal void QueueMoneyGrant(int characterId, long amount)
    {
        _pendingMoneyGrants.Enqueue((characterId, amount));
        _moneyGrantSignal.Release();
    }

    /// <summary>
    ///     Resolves as soon as a grant is queued (or immediately, if one is already pending un-awaited) -- lets
    ///     <c>MonsterLootFlushHost</c> race this against its own periodic timer via <c>Task.WhenAny</c> rather
    ///     than only ever waking up on the timer's fixed cadence.
    /// </summary>
    public Task WaitForMoneyGrantAsync(CancellationToken ct)
    {
        return _moneyGrantSignal.WaitAsync(ct);
    }

    /// <summary>Callable from any thread; the only intended caller is the background flush host.</summary>
    public IReadOnlyList<(int CharacterId, long Amount)> DrainPendingMoneyGrants()
    {
        if (_pendingMoneyGrants.IsEmpty)
            return [];

        List<(int CharacterId, long Amount)>? grants = null;
        while (_pendingMoneyGrants.TryDequeue(out var grant))
            (grants ??= []).Add(grant);

        return (IReadOnlyList<(int CharacterId, long Amount)>?)grants ?? [];
    }

    /// <summary>Tick-owned caller only (<see cref="Monsters.MonsterSpawnScheduler" />).</summary>
    internal void SpawnGroundItem(int itemId, int quantity, float posX, float posY, float posZ, string master,
        string partyName, int dropSort)
    {
        var index = Interlocked.Increment(ref _groundItemServerIndexSeed);
        var uniqueNumber = unchecked((uint)Interlocked.Increment(ref _groundItemUniqueNumberSeed));

        var entity = new GroundItemEntity(index, uniqueNumber, itemId, quantity, 0, 0, posX,
            posY, posZ, TruncateName(master), TruncateName(partyName), dropSort, _clock, 0,
            0, 0);

        _groundItems[index] = entity;
        _groundItemLastRebroadcast[index] = _clock;
        BroadcastGroundItemAction(entity, 1); // action=1 at creation
    }

    private static string TruncateName(string name)
    {
        return name.Length <= 13 ? name : name[..13]; // MAX_AVATAR_NAME_LENGTH
    }

    /// <summary>
    ///     Atomically claims (removes) a ground item for pickup -- callable from any thread. Ownership window
    ///     and distance are checked against immutable snapshot fields, so only the removal itself needs to be
    ///     atomic: <see cref="ConcurrentDictionary{TKey,TValue}" />'s compare-and-remove on the snapshot just
    ///     read gives "first claimant wins" with no custom locking. The despawn broadcast is NOT done here (it
    ///     needs <see cref="_grid" />, tick-thread-only) -- see <see cref="_claimedGroundItemDespawns" />.
    /// </summary>
    public GroundItemClaimOutcome TryClaimGroundItem(int serverIndex, uint expectedUniqueNumber, string claimantName,
        string? claimantPartyName, float claimantX, float claimantY, float claimantZ, out GroundItemEntity? item)
    {
        if (!_groundItems.TryGetValue(serverIndex, out var snapshot) ||
            snapshot.UniqueNumber != expectedUniqueNumber)
        {
            item = null;
            return GroundItemClaimOutcome.NotFound;
        }

        if (!snapshot.IsClaimableBy(claimantName, claimantPartyName, _clock))
        {
            item = null;
            return GroundItemClaimOutcome.NotOwned;
        }

        // ITEM_OBJECT::CheckPossibleGetItem (S07_MyGame06.cpp:63): full 3D distance, not XZ-only.
        var dx = snapshot.PosX - claimantX;
        var dy = snapshot.PosY - claimantY;
        var dz = snapshot.PosZ - claimantZ;
        if (dx * dx + dy * dy + dz * dz >
            GroundItemPickupPolicy.MaxPickupDistance * GroundItemPickupPolicy.MaxPickupDistance)
        {
            item = null;
            return GroundItemClaimOutcome.TooFar;
        }

        if (!((ICollection<KeyValuePair<int, GroundItemEntity>>)_groundItems).Remove(
                new KeyValuePair<int, GroundItemEntity>(serverIndex, snapshot)))
        {
            item = null;
            return GroundItemClaimOutcome.NotFound; // lost the race to a concurrent claimant
        }

        _claimedGroundItemDespawns.Enqueue(snapshot);
        item = snapshot;
        return GroundItemClaimOutcome.Success;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var tickInterval = TimeSpan.FromMilliseconds(1000.0 / options.TickRateHz);
        using var timer = new PeriodicTimer(tickInterval);

        // Real elapsed time between frames is measured, not assumed to equal tickInterval: PeriodicTimer
        // coalesces missed periods, and the SimulationTickAccumulator must be paid in actual time or the 2 Hz
        // simulation would silently slow down under load.
        var lastFrame = Stopwatch.GetTimestamp();

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            var now = Stopwatch.GetTimestamp();
            var elapsed = Stopwatch.GetElapsedTime(lastFrame, now);
            lastFrame = now;

            Tick(elapsed);

            var tickMs = Stopwatch.GetElapsedTime(now).TotalMilliseconds;
            if (tickMs > tickInterval.TotalMilliseconds)
                logger.LogWarning("Zone {MapId} tick took {ElapsedMs:F1} ms (budget {BudgetMs:F1} ms)", MapId,
                    tickMs, tickInterval.TotalMilliseconds);
        }
    }

    /// <summary>
    ///     One network frame of this zone: drain inbox -&gt; simulate due legacy ticks -&gt; periodic rebroadcast.
    ///     Public, but with exactly two legitimate callers -- <see cref="RunAsync" />'s timer loop, and tests
    ///     driving deterministic simulated time. Calling it from any other thread while <see cref="RunAsync" />
    ///     runs would break the single-writer invariant.
    /// </summary>
    public void Tick(TimeSpan elapsed)
    {
        _clock += elapsed;

        var t0 = Stopwatch.GetTimestamp();
        DrainInbox();
        DrainInventoryCommands();
        DrainSkillCommands();
        DrainCombatCommands();
        DrainChatCommands();
        DrainMentorCommands();
        DrainQuestCommands();
        DrainGuildCommands();
        DrainTribeProgressCommands();
        DrainPshopCommands();
        DrainMissionCommands();
        DrainDrinkBottleCommands();
        DrainHeroRankingQueryCommands();
        DrainFishingCommands();
        DrainMountCommands();
        DrainCostumeCommands();
        DrainStellarCoreCommands();
        DrainAvatarBuffCommands();
        DrainRuneSocketCommands();
        DrainAutoBuffCommands();
        var t1 = Stopwatch.GetTimestamp();
        Simulate(_accumulator.Advance(elapsed));
        var t2 = Stopwatch.GetTimestamp();
        ProcessPendingRevives();
        RebroadcastAvatars();
        // Claimed-item despawns first (so other players stop seeing it ASAP), then the 5 s keep-alives, then
        // the 60 s expiry sweep.
        DrainClaimedGroundItemDespawns();
        RebroadcastMonsters();
        RebroadcastGroundItems();
        ExpireGroundItems();
        var t3 = Stopwatch.GetTimestamp();

        ZoneTickMetrics.StageDurationMs.Record(Stopwatch.GetElapsedTime(t0, t1).TotalMilliseconds, _mapTag,
            ZoneTickMetrics.DrainStage);
        ZoneTickMetrics.StageDurationMs.Record(Stopwatch.GetElapsedTime(t1, t2).TotalMilliseconds, _mapTag,
            ZoneTickMetrics.SimulateStage);
        ZoneTickMetrics.StageDurationMs.Record(Stopwatch.GetElapsedTime(t2, t3).TotalMilliseconds, _mapTag,
            ZoneTickMetrics.RebroadcastStage);
    }

    private void DrainInbox()
    {
        while (_inbox.Reader.TryRead(out var command))
            try
            {
                switch (command.Kind)
                {
                    case ZoneCommandKind.Enter:
                        HandleEnter(command.CharacterId, command.EnterData!);
                        break;
                    case ZoneCommandKind.Leave:
                        HandleLeave(command.CharacterId, command.HandoffTarget, command.HandoffPosition);
                        break;
                    case ZoneCommandKind.Move:
                        var action = command.Action;
                        HandleMove(command.CharacterId, in action);
                        break;
                    case ZoneCommandKind.PetAction:
                        var petAction = command.Action;
                        HandlePetAction(command.CharacterId, in petAction);
                        break;
                }
            }
            catch (Exception ex)
            {
                // One bad command must never take the whole tick loop down -- the next command, and the next
                // tick, still have to run for every OTHER player in the zone.
                logger.LogError(ex, "Zone {MapId} command {Kind} for character {CharacterId} failed", MapId,
                    command.Kind, command.CharacterId);
            }
    }

    private void DrainInventoryCommands()
    {
        while (_inventoryInbox.Reader.TryRead(out var command))
            try
            {
                ApplyInventoryCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                // Same containment posture as DrainInbox: one bad inventory command must never take the whole
                // tick loop down for every other player in the zone.
                logger.LogError(ex, "Zone {MapId} inventory command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                // Still signal as faulted (not merely completed) so a caller awaiting Applied to release its
                // EconomyActionLock never hangs on a command that blew up.
                command.Applied?.TrySetException(ex);
            }
    }

    /// <summary>
    ///     No validation/I/O here on purpose -- already decided and persisted by the posting handler before
    ///     reaching the inbox. A no-op if the character already left this zone by the time the tick drains
    ///     this: their SQL write is already durable, so there is nothing left to mirror.
    /// </summary>
    private void ApplyInventoryCommand(in InventoryZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        foreach (var snapshot in command.Containers)
        {
            state.Inventory.ReplaceContainer(snapshot.Container, snapshot.Slots);

            // A pet SWAP (not just any equip touch) resets growth/activity to the newly-equipped pet's fresh state.
            if (snapshot.Container == ContainerMatrix.Equipment)
            {
                var newPetItemId = snapshot.Slots.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
                    ? petStack.ItemId
                    : 0;
                if (newPetItemId != state.LastSeenPetItemId)
                {
                    state.LastSeenPetItemId = newPetItemId;
                    state.PetGrowth = 0;
                    state.PetActivity = 0;
                    dirtyTracker.MarkDirty(command.CharacterId, DirtyFlags.Progression);
                }
            }
        }

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;
    }

    private void DrainSkillCommands()
    {
        while (_skillInbox.Reader.TryRead(out var command))
            try
            {
                ApplySkillCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} skill command for character {CharacterId} failed", MapId,
                    command.CharacterId);
            }
    }

    /// <summary>Same posture as <see cref="ApplyInventoryCommand" />.</summary>
    private void ApplySkillCommand(in SkillZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.LearnedSkills = state.LearnedSkills.SetItem(command.Slot, command.Skill);
        state.SkillPoints = command.NewSkillPoints;
        dirtyTracker.MarkDirty(command.CharacterId, DirtyFlags.Progression);
    }

    private void DrainMentorCommands()
    {
        while (_mentorInbox.Reader.TryRead(out var command))
            try
            {
                ApplyMentorCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} mentor command for character {CharacterId} failed", MapId,
                    command.CharacterId);
            }
    }

    /// <summary>Same posture as <see cref="ApplyInventoryCommand" />.</summary>
    private void ApplyMentorCommand(in MentorZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.TeacherCharacterId = command.TeacherCharacterId;
    }

    private void DrainGuildCommands()
    {
        while (_guildInbox.Reader.TryRead(out var command))
            try
            {
                ApplyGuildMembershipCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} guild command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    /// <summary>Same posture as <see cref="ApplyInventoryCommand" />.</summary>
    private void ApplyGuildMembershipCommand(in GuildMembershipZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.GuildId = command.GuildId;
        state.GuildName = command.GuildName;
        state.GuildRoleDb = command.GuildRoleDb;
        state.GuildCallName = command.GuildCallName;
    }

    private void DrainTribeProgressCommands()
    {
        while (_tribeInbox.Reader.TryRead(out var command))
            try
            {
                ApplyTribeProgressCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} tribe progress command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    /// <summary>
    ///     Same posture as <see cref="ApplyInventoryCommand" />. Every field is independently optional: null means "not
    ///     touched," never "reset to zero/false."
    /// </summary>
    private void ApplyTribeProgressCommand(in TribeProgressZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        var changed = false;

        if (command.ContributionPoints is { } contributionPoints)
        {
            state.ContributionPoints = contributionPoints;
            changed = true;
        }

        if (command.TribeRole is { } tribeRole)
        {
            state.TribeRole = tribeRole;
            changed = true;
        }

        if (command.Title is { } title)
        {
            state.Title = title;
            changed = true;
        }

        if (command.Halo is { } halo)
        {
            state.Halo = halo;
            changed = true;
        }

        if (command.ProtectForHalo is { } protectForHalo)
        {
            state.ProtectForHalo = protectForHalo;
            changed = true;
        }

        if (command.UseOrnament is { } useOrnament)
        {
            state.UseOrnament = useOrnament;
            changed = true;
        }

        if (command.BonusItemLevel is { } bonusItemLevel)
        {
            state.BonusItemLevel = bonusItemLevel;
            changed = true;
        }

        if (command.BonusItemValue is { } bonusItemValue)
        {
            state.BonusItemValue = bonusItemValue;
            changed = true;
        }

        if (command.StatVit is { } statVit)
        {
            state.StatVit = statVit;
            changed = true;
        }

        if (command.StatStr is { } statStr)
        {
            state.StatStr = statStr;
            changed = true;
        }

        if (command.StatInt is { } statInt)
        {
            state.StatInt = statInt;
            changed = true;
        }

        if (command.StatDex is { } statDex)
        {
            state.StatDex = statDex;
            changed = true;
        }

        if (command.StatPoints is { } statPoints)
        {
            state.StatPoints = statPoints;
            changed = true;
        }

        if (command.Life is { } life)
        {
            state.Life = life;
            changed = true;
        }

        if (command.Mana is { } mana)
        {
            state.Mana = mana;
            changed = true;
        }

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;

        if (command.TribeNotifyScrollCount is { } tribeNotifyScrollCount)
        {
            state.TribeNotifyScrollCount = tribeNotifyScrollCount;
            changed = true;
        }

        if (changed)
            dirtyTracker.MarkDirty(command.CharacterId, DirtyFlags.Progression);

        if (!command.DropItems.IsDefaultOrEmpty)
            foreach (var drop in command.DropItems)
                SpawnGroundItem(drop.ItemId, drop.Quantity, state.PosX, state.PosY, state.PosZ, state.Name, "", 0);
    }

    private void DrainQuestCommands()
    {
        while (_questInbox.Reader.TryRead(out var command))
            try
            {
                ApplyQuestCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} quest command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    /// <summary>Same posture as <see cref="ApplyInventoryCommand" />.</summary>
    private void ApplyQuestCommand(in QuestZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.QuestStepPermanent = command.Progress.StepPermanent;
        state.QuestActiveFlag = command.Progress.ActiveFlag;
        state.QuestSort = command.Progress.QSort;
        state.QuestTargetPhase = command.Progress.TargetPhase;
        state.QuestKillCounter = command.Progress.KillCounter;

        foreach (var snapshot in command.Containers)
            state.Inventory.ReplaceContainer(snapshot.Container, snapshot.Slots);

        if (command.ExperienceDelta != 0)
            state.Experience += command.ExperienceDelta;
        if (command.ContributionPointsDelta != 0)
            state.ContributionPoints += command.ContributionPointsDelta;
        if (command.TeacherPointDelta != 0)
            state.TeacherPoint += command.TeacherPointDelta;
        if (command.ExperienceDelta != 0 || command.ContributionPointsDelta != 0 || command.TeacherPointDelta != 0)
            dirtyTracker.MarkDirty(command.CharacterId, DirtyFlags.Progression);
    }

    private void DrainMissionCommands()
    {
        while (_missionInbox.Reader.TryRead(out var command))
            try
            {
                ApplyMissionCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} mission command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    /// <summary>Same posture as <see cref="ApplyInventoryCommand" />.</summary>
    private void ApplyMissionCommand(in MissionZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.MissionJoinWar = command.JoinWar;
        state.MissionKillOtherTribe = command.KillOtherTribe;
        state.MissionKillMonster = command.KillMonster;
        state.MissionPlayTime = command.PlayTime;

        foreach (var snapshot in command.Containers)
            state.Inventory.ReplaceContainer(snapshot.Container, snapshot.Slots);
    }

    private void DrainDrinkBottleCommands()
    {
        while (_bottleInbox.Reader.TryRead(out var command))
            try
            {
                ApplyDrinkBottleCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} drink-bottle command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    private void ApplyDrinkBottleCommand(in DrinkBottleZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        var itemId = command.NewItemId ?? state.BottleSlots[command.BottleIndex].ItemId;
        state.BottleSlots = state.BottleSlots.SetItem(command.BottleIndex, (itemId, command.RemainingCount));

        state.Life = command.NewLife;

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;

        dirtyTracker.MarkDirty(command.CharacterId, DirtyFlags.Vitals);
    }

    private void DrainHeroRankingQueryCommands()
    {
        while (_heroRankingInbox.Reader.TryRead(out var command))
            try
            {
                ApplyHeroRankingQueryCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} hero-ranking query command for character {CharacterId} failed",
                    MapId, command.CharacterId);
            }
    }

    /// <summary>Same posture as <see cref="ApplyInventoryCommand" />.</summary>
    private void ApplyHeroRankingQueryCommand(in HeroRankingQueryZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        if (command.Previous)
            state.LastHeroRankingPreviousQueryAtZoneClock = command.QueriedAtZoneClock;
        else
            state.LastHeroRankingCurrentQueryAtZoneClock = command.QueriedAtZoneClock;
    }

    private void DrainFishingCommands()
    {
        while (_fishingInbox.Reader.TryRead(out var command))
            try
            {
                ApplyFishingCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} fishing command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    /// <summary>
    ///     Mirrors the fishing state machine decided by <c>FishingLineHandler</c>/<c>FishingProgressHandler</c>/
    ///     <c>FishingCatchHandler</c> (op 103/104/105) and, when <see cref="FishingZoneCommand.Broadcast" /> is
    ///     set, rebroadcasts the avatar action to self + AOI neighbors -- matching the legacy's <c>Broadcast11</c>
    ///     (self included) / <c>Broadcast22</c>+<c>USEND</c> (self direct-sent, neighbors excluded) pair, which
    ///     are net-equivalent "everyone in range including self."
    /// </summary>
    private void ApplyFishingCommand(in FishingZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.FishingState = command.NewFishingState;
        state.FishingStep = command.NewFishingStep;
        state.CatchingFish = command.CatchingFish;

        if (command.CastAtUtc is { } castAt)
            state.FishingCastAtUtc = castAt;

        if (!command.Broadcast)
            return;

        if (command.ActionSort is { } sort)
            state.ActionSort = sort;

        var action = new ActionInfo
        {
            Type = 0,
            Sort = state.ActionSort,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = new float[3],
            PetTargetLocation = new float[3],
            PetFront = 0,
            PetSort = 0,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };

        var fisherId = command.CharacterId;
        var recipients = _grid.Neighbors(state.CurrentCell).Where(id => id != fisherId).ToList();
        recipients.Add(fisherId);
        BroadcastAvatarAction(recipients, state, action);
    }

    private void DrainMountCommands()
    {
        while (_mountInbox.Reader.TryRead(out var command))
            try
            {
                ApplyMountCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} mount command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    /// <summary>
    ///     Op 87/113 (<c>MountState</c>/<c>MountAbsorb</c>) self-mutation mirror. <see cref="MountZoneCommand.Broadcast" />
    ///     decides which AOI/self packets follow, matching the legacy's own per-case B_AVATAR_CHANGE_INFO_1/2 pairing.
    /// </summary>
    private void ApplyMountCommand(in MountZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        var wasAbsorbed = state.AnimalAbsorbState != 0;
        var changed = false;

        if (command.AnimalIndex is { } animalIndex)
            state.AnimalIndex = animalIndex;

        if (command.AnimalNumber is { } animalNumber)
        {
            state.AnimalNumber = animalNumber;
            changed = true;
        }

        if (command.AnimalAbsorbState is { } animalAbsorbState)
        {
            state.AnimalAbsorbState = animalAbsorbState;
            changed = true;
        }

        if (command.Life is { } life)
        {
            state.Life = life;
            changed = true;
        }

        if (command.Mana is { } mana)
        {
            state.Mana = mana;
            changed = true;
        }

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;

        if (changed)
            dirtyTracker.MarkDirty(command.CharacterId, DirtyFlags.Vitals);

        switch (command.Broadcast)
        {
            case MountBroadcastKind.Mount:
                BroadcastAvatarStateFlag(state, 12, state.AnimalNumber, 0, 0);
                BroadcastAvatarStateFlag(state, 26, 0, 0, 0);
                break;
            case MountBroadcastKind.Dismount:
                if (wasAbsorbed)
                    state.Session.Send(new AvatarStatUpdateResponse { Sort = 79, Value = 0, Value2 = 0 });
                BroadcastAvatarStateFlag(state, 13, 0, 0, 0);
                break;
            case MountBroadcastKind.AbsorbToggle:
                BroadcastAvatarStateFlag(state, 26, state.AnimalAbsorbState, 0, 0);
                state.Session.Send(new AvatarStatUpdateResponse
                    { Sort = 79, Value = state.AnimalAbsorbState, Value2 = 0 });
                break;
        }
    }

    /// <summary>
    ///     Builds one B_AVATAR_CHANGE_INFO_1-equivalent frame and sends it once to <paramref name="state" />
    ///     itself plus every other AOI neighbor (unlike the legacy's own Broadcast11, which re-sends to the
    ///     source player too -- functionally a no-op duplicate there, so intentionally not reproduced).
    /// </summary>
    private void BroadcastAvatarStateFlag(PlayerRuntimeState state, int sort, int value01, int value02, int value03)
    {
        var response = new AvatarStateFlagResponse
        {
            ServerIndex = state.CharacterId,
            UniqueNumber = state.UniqueNumber,
            Sort = sort,
            Value01 = value01,
            Value02 = value02,
            Value03 = value03
        };

        state.Session.Send(response);
        foreach (var neighborId in _grid.Neighbors(state.CurrentCell))
        {
            if (neighborId == state.CharacterId) continue;
            if (_players.TryGetValue(neighborId, out var neighbor))
                neighbor.Session.Send(response);
        }
    }

    private void DrainCostumeCommands()
    {
        while (_costumeInbox.Reader.TryRead(out var command))
            try
            {
                ApplyCostumeCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} costume command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    /// <summary>
    ///     Op 90/139 (<c>CostumeState</c>/<c>CostumeVisibility</c>) self-mutation mirror.
    ///     <see cref="CostumeZoneCommand.Broadcast" /> covers op 90's AvatarStateFlag pair;
    ///     <see cref="CostumeZoneCommand.FullActionRebroadcast" /> covers op 139's full avatar-action rebroadcast
    ///     instead (matches the legacy's own B_AVATAR_ACTION_RECV + Broadcast11 for that opcode specifically).
    /// </summary>
    private void ApplyCostumeCommand(in CostumeZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        var changed = false;

        if (command.CostumeIndex is { } costumeIndex)
            state.CostumeIndex = costumeIndex;

        if (command.CostumeNumber is { } costumeNumber)
        {
            state.CostumeNumber = costumeNumber;
            changed = true;
        }

        if (command.CostumeState is { } costumeState)
            state.CostumeState = costumeState;

        if (command.WardrobeSlotCleared is { } clearedSlot)
            state.CostumeWardrobe = state.CostumeWardrobe.SetItem(clearedSlot, 0);

        if (command.Life is { } life)
        {
            state.Life = life;
            changed = true;
        }

        if (command.Mana is { } mana)
        {
            state.Mana = mana;
            changed = true;
        }

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;

        if (changed)
            dirtyTracker.MarkDirty(command.CharacterId, DirtyFlags.Vitals);

        switch (command.Broadcast)
        {
            case CostumeBroadcastKind.Equip:
                BroadcastAvatarStateFlag(state, 16, state.CostumeNumber, 0, 0);
                break;
            case CostumeBroadcastKind.Remove:
                BroadcastAvatarStateFlag(state, 17, 0, 0, 0);
                break;
        }

        if (!command.FullActionRebroadcast) return;
        var characterId = command.CharacterId;
        SendAvatarAction(state.Session, state);
        var neighbors = _grid.Neighbors(state.CurrentCell).Where(id => id != characterId).ToArray();
        BroadcastAvatarAction(neighbors, state);
    }

    private void DrainStellarCoreCommands()
    {
        while (_stellarCoreInbox.Reader.TryRead(out var command))
            try
            {
                ApplyStellarCoreCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} stellar-core command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    /// <summary>
    ///     Op 153 <c>StellarCoreState</c> self-mutation mirror. <see cref="StellarCoreZoneCommand.Broadcast" />
    ///     covers the equip/remove AvatarStateFlag pair (sort 37/38), matching the legacy's own
    ///     B_AVATAR_CHANGE_INFO_1 + Broadcast11 pairing for CZ cases 3/4 exactly (same posture as
    ///     <see cref="ApplyCostumeCommand" />'s case 3/4 -- Broadcast11 there re-sends the just-composed
    ///     AVATAR_CHANGE_INFO_1 frame, it does not build a separate full-action packet).
    /// </summary>
    private void ApplyStellarCoreCommand(in StellarCoreZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        var changed = false;

        if (command.CoreIndex is { } coreIndex)
            state.StellarCoreIndex = coreIndex;

        if (command.CoreNumber is { } coreNumber)
        {
            state.StellarCoreNumber = coreNumber;
            changed = true;
        }

        if (command.WardrobeSlotCleared is { } clearedSlot)
            state.StellarCoreWardrobe = state.StellarCoreWardrobe.SetItem(clearedSlot, 0);

        if (command.Life is { } life)
        {
            state.Life = life;
            changed = true;
        }

        if (command.Mana is { } mana)
        {
            state.Mana = mana;
            changed = true;
        }

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;

        if (changed)
            dirtyTracker.MarkDirty(command.CharacterId, DirtyFlags.Vitals);

        switch (command.Broadcast)
        {
            case StellarCoreBroadcastKind.Equip:
                BroadcastAvatarStateFlag(state, 37, state.StellarCoreNumber, 0, 0);
                break;
            case StellarCoreBroadcastKind.Remove:
                BroadcastAvatarStateFlag(state, 38, 0, 0, 0);
                break;
        }
    }

    private void DrainAvatarBuffCommands()
    {
        while (_avatarBuffInbox.Reader.TryRead(out var command))
            try
            {
                ApplyAvatarBuffCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} avatar-buff command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    /// <summary>
    ///     Op 97/111 (<c>PlaytimeBuff</c>/<c>RankBuff</c>) self-mutation mirror. Both opcodes' own
    ///     AvatarStatUpdateResponse self-unicast is sent directly by the handler (the value is already known
    ///     before the tick mirrors it, same posture as <c>DrinkBottleHandler</c>) -- this only mirrors the
    ///     already-decided state.
    /// </summary>
    private void ApplyAvatarBuffCommand(in AvatarBuffZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        var changed = false;

        if (command.StateTimeEffect is { } stateTimeEffect)
            state.StateTimeEffect = stateTimeEffect;

        if (command.RankBuffType is { } rankBuffType)
            state.RankBuffType = rankBuffType;

        if (command.Life is { } life)
        {
            state.Life = life;
            changed = true;
        }

        if (command.Mana is { } mana)
        {
            state.Mana = mana;
            changed = true;
        }

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;

        if (changed)
            dirtyTracker.MarkDirty(command.CharacterId, DirtyFlags.Vitals);
    }

    private void DrainRuneSocketCommands()
    {
        while (_runeInbox.Reader.TryRead(out var command))
            try
            {
                ApplyRuneSocketCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} rune-socket command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    /// <summary>
    ///     Op 157 <c>RuneSocket</c> self-mutation mirror. <c>RuneItemId</c>/<c>RuneStat</c> null means "clear
    ///     this slot" (both sort 0 insert and sort 1 remove always write the full new value for
    ///     <see cref="RuneSocketZoneCommand.RuneIndex" />, never "leave untouched"). The paired inventory-slot
    ///     mirror rides the existing <see cref="_inventoryInbox" /> separately, same as every other
    ///     economy-adjacent handler. <see cref="RuneSocketZoneCommand.UpdatedStats" /> is always null today --
    ///     see <c>RuneSocketHandler</c>'s remarks for why recomputing would be a pure no-op.
    /// </summary>
    private void ApplyRuneSocketCommand(in RuneSocketZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.RuneSystem = state.RuneSystem.SetItem(command.RuneIndex, command.RuneItemId ?? 0);
        state.RuneSystemStat = state.RuneSystemStat.SetItem(command.RuneIndex, command.RuneStat ?? 0);

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;
    }

    private void DrainAutoBuffCommands()
    {
        while (_autoBuffInbox.Reader.TryRead(out var command))
            try
            {
                ApplyAutoBuffCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} auto-buff command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    /// <summary>
    ///     Op 94/95 (<c>ContinueSkillStat</c>/<c>ContinueSkillUse</c>) mirror. <c>RegisteredSkills</c> (op 94)
    ///     lands on <see cref="PlayerRuntimeState.AutoBuffSkill" />; <c>ManaAfterActivation</c>/<c>ActionSort</c>
    ///     (op 95 <c>tSort==1</c>) mirror <see cref="PlayerRuntimeState.Mana" />/<see cref="PlayerRuntimeState.ActionSort" />
    ///     and, when <see cref="AutoBuffZoneCommand.Broadcast" /> is set, rebroadcast the resulting avatar action
    ///     to self + AOI neighbors -- same self-inclusive <c>Broadcast11</c> posture as <see cref="ApplyFishingCommand" />.
    ///     Op 95 <c>tSort==2</c>'s per-tick buff-application logic (<c>ProcessForCreateEffectValue</c>) is out of
    ///     scope -- see <see cref="Skills.AutoBuffActivationResolver" />'s remarks.
    /// </summary>
    private void ApplyAutoBuffCommand(in AutoBuffZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        if (command.RegisteredSkills is { } registered)
            state.AutoBuffSkill = registered;

        if (command.ManaAfterActivation is { } mana)
        {
            state.Mana = mana;
            dirtyTracker.MarkDirty(command.CharacterId, DirtyFlags.Vitals);
        }

        if (!command.Broadcast)
            return;

        if (command.ActionSort is { } sort)
            state.ActionSort = sort;

        state.ActionSkillNumber = 0;
        state.ActionSkillGradeNum1 = 0;
        state.ActionSkillGradeNum2 = 0;

        var action = new ActionInfo
        {
            Type = 0,
            Sort = state.ActionSort,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = new float[3],
            PetTargetLocation = new float[3],
            PetFront = 0,
            PetSort = 0,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };

        var casterId = command.CharacterId;
        var recipients = _grid.Neighbors(state.CurrentCell).Where(id => id != casterId).ToList();
        recipients.Add(casterId);
        BroadcastAvatarAction(recipients, state, action);
    }

    private void DrainPshopCommands()
    {
        while (_pshopInbox.Reader.TryRead(out var command))
            try
            {
                ApplyPshopCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} pshop command for character {CharacterId} failed", MapId,
                    command.CharacterId);
            }
    }

    /// <summary>
    ///     No-op if the character left, or their stall was already re-opened/re-populated since (a stale mirror is
    ///     harmless).
    /// </summary>
    private void ApplyPshopCommand(in PshopZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state) || state.PshopListing is not { } listing)
            return;

        if (command.CloseShop)
        {
            state.PshopOpen = false;
            return;
        }

        var itemInfo = listing.ItemInfo;
        var baseIndex = (command.Page * 5 + command.Slot) * 9;
        for (var k = 0; k < 9; k++)
            itemInfo[baseIndex + k] = 0;
    }

    private void DrainCombatCommands()
    {
        while (_combatInbox.Reader.TryRead(out var command))
            try
            {
                ApplyCombatCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} combat command from character {CharacterId} failed", MapId,
                    command.AttackerCharacterId);
            }
    }

    private void DrainChatCommands()
    {
        while (_chatInbox.Reader.TryRead(out var command))
            try
            {
                ApplyChatCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} chat command from character {CharacterId} failed", MapId,
                    command.SenderCharacterId);
            }
    }

    /// <summary>
    ///     Resolves Local/Shout/Tribe chat's audience on this zone's own tick thread. Local = AOI-neighbor
    ///     broadcast filtered by the sender's own tribe (alliance not modeled); Shout/Tribe = whole-zone, Tribe
    ///     additionally filtered by tribe. The sender always receives its own echo.
    /// </summary>
    private void ApplyChatCommand(in ChatZoneCommand command)
    {
        if (!_players.TryGetValue(command.SenderCharacterId, out var sender))
            return; // sender disconnected/handed off between post and drain -- benign

        switch (command.Kind)
        {
            case ChatBroadcastKind.Local:
            {
                var response = new LocalChatResponse
                    { AvatarName = sender.Name, Content = command.Content, Link = command.Link };
                foreach (var id in _grid.Neighbors(sender.CurrentCell))
                    if (_players.TryGetValue(id, out var recipient) && recipient.Tribe == sender.Tribe)
                        recipient.Session.Send(response);
                break;
            }
            case ChatBroadcastKind.Shout:
            {
                var response = new ShoutResponse
                    { AvatarName = sender.Name, Content = command.Content, Link = command.Link };
                foreach (var recipient in _players.Values)
                    recipient.Session.Send(response);
                break;
            }
            case ChatBroadcastKind.Tribe:
            {
                var response = new TribeChatResponse
                    { AvatarName = sender.Name, Content = command.Content, Link = command.Link };
                foreach (var recipient in _players.Values)
                    if (recipient.Tribe == sender.Tribe)
                        recipient.Session.Send(response);
                break;
            }
        }
    }

    /// <summary>
    ///     Resolves one CZ_PROCESS_ATTACK_SEND request (<c>mCase</c> 1-6 dispatch) entirely on this zone's own
    ///     tick thread. Only <c>mCase</c> 2 (Avatar -&gt; Avatar, enemy tribe) and 3 (Avatar -&gt; Monster) are
    ///     implemented -- see <see cref="CombatResolver" />'s remarks for what else is/isn't modeled and why.
    ///     Every unimplemented case is a silent no-op, not a disconnect: <c>AttackHandler</c> already rejected
    ///     any <c>mCase</c> outside 1-6, so an in-range-but-unwired value is an in-progress feature, not a hostile packet.
    /// </summary>
    private void ApplyCombatCommand(in CombatCommand command)
    {
        // mCase 4 is deliberately not handled here even though a client could send it: the legacy itself only
        // ever reaches ProcessAttack04 from the monster's own AI (S07_MyGame05.cpp:3961) -- see ResolveMonsterAttack.
        if (command.AttackInfo.Case == 3)
        {
            ApplyPvmAttack(command);
            return;
        }

        if (command.AttackInfo.Case != 2)
            return; // mCase 1/4/5/6 -- deliberately unimplemented, see method remarks.

        if (!_players.TryGetValue(command.AttackerCharacterId, out var attackerState))
            return;
        if (!_players.TryGetValue(command.AttackInfo.ServerIndex2, out var defenderState))
            return;

        var attackerSnapshot = ToCombatantSnapshot(attackerState);
        var defenderSnapshot = ToCombatantSnapshot(defenderState);

        var attackSkill = command.AttackInfo.AttackActionValue1 == 2 &&
                          worldData.SkillsById.TryGetValue(command.AttackInfo.AttackActionValue2,
                              out var skillDef)
            ? skillDef
            : null;

        var outcome = CombatResolver.ResolveEnemyTribeAttack(attackerSnapshot, defenderSnapshot,
            command.AttackInfo, _clock, attackSkill, _random);

        if (outcome.Rejected)
            return;

        if (outcome.ChargeConsumed)
            attackerState.Buffs.Buff[8 * 2] = 0; // charge buff slot 8, value half -- single-use

        // "1 + attacker's weapon ItemId" on a hit (client picks the swing animation/effect from this), 0 on a miss.
        var attackerWeaponItemId = attackerState.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId ?? 0;
        var response = new AttackResponse
        {
            AttackInfo = command.AttackInfo with
            {
                AttackResultValue = outcome.Hit ? 1 + attackerWeaponItemId : 0,
                AttackCriticalExist = outcome.Critical ? 1 : 0,
                AttackElementDamage = outcome.ElementDamage,
                AttackViewDamageValue = outcome.DamageApplied,
                AttackRealDamageValue = outcome.DamageApplied
            }
        };

        var recipients = CombatRecipients(attackerState, defenderState);
        BroadcastAttackResult(recipients, response);

        if (!outcome.Hit)
            return;

        defenderState.Life -= outcome.DamageApplied;
        dirtyTracker.MarkDirty(defenderState.CharacterId, DirtyFlags.Vitals);

        if (defenderState.Life <= 0)
            ApplyDeath(defenderState.CharacterId, DeathCause.PlayerKill);
    }

    /// <summary>
    ///     mCase 3, Avatar -&gt; Monster. Reuses <see cref="TryDamageMonster" /> for the HP mutation/death
    ///     handoff so there is exactly one code path that ever decides "this monster just died."
    /// </summary>
    private void ApplyPvmAttack(in CombatCommand command)
    {
        if (!_players.TryGetValue(command.AttackerCharacterId, out var attackerState))
            return;
        if (!_monsters.TryGetValue(command.AttackInfo.ServerIndex2, out var monster))
            return;
        if (monster.UniqueNumber != command.AttackInfo.UniqueNumber2)
            return;

        var attackerSnapshot = ToCombatantSnapshot(attackerState);
        var outcome = MonsterCombatResolver.ResolvePvmAttack(attackerSnapshot, monster, command.AttackInfo, _clock,
            _random);

        if (outcome.Rejected)
            return;

        if (outcome.ChargeConsumed)
            attackerState.Buffs.Buff[8 * 2] =
                0; // charge buff slot 8, value half -- single-use, same convention as mCase 2

        var attackerWeaponItemId = attackerState.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId ?? 0;
        var response = new AttackResponse
        {
            AttackInfo = command.AttackInfo with
            {
                AttackResultValue = outcome.Hit ? 1 + attackerWeaponItemId : 0,
                AttackCriticalExist = outcome.Critical ? 1 : 0,
                AttackElementDamage = outcome.ElementDamage,
                AttackViewDamageValue = outcome.DamageApplied,
                AttackRealDamageValue = outcome.DamageApplied
            }
        };

        var recipients = new HashSet<int> { attackerState.CharacterId };
        foreach (var id in _grid.Neighbors(attackerState.CurrentCell)) recipients.Add(id);
        foreach (var id in NeighborsOfPosition(monster.PosX, monster.PosZ)) recipients.Add(id);
        BroadcastAttackResult(recipients, response);

        if (!outcome.Hit)
            return;

        TryDamageMonster(monster.ServerIndex, outcome.DamageApplied, attackerState.CharacterId, out _, out _);
    }

    private CombatantSnapshot ToCombatantSnapshot(PlayerRuntimeState state)
    {
        return new CombatantSnapshot(
            state.CharacterId,
            state.Tribe,
            state.IsDead,
            state.Life,
            state.MaxLife,
            state.PosX,
            state.PosY,
            state.PosZ,
            state.ZoneEntryAtZoneClock,
            state.Stats ?? default,
            state.Buffs.Buff[8 * 2]);
    }

    /// <summary>
    ///     Attacker + defender + both their AOI neighbors, deduplicated -- matches the legacy's own "AOI broadcast +
    ///     unicast to the attacker" (contract doc on <c>AttackResponse</c>).
    /// </summary>
    private HashSet<int> CombatRecipients(PlayerRuntimeState attacker, PlayerRuntimeState defender)
    {
        var recipients = new HashSet<int> { attacker.CharacterId, defender.CharacterId };
        foreach (var id in _grid.Neighbors(attacker.CurrentCell)) recipients.Add(id);
        foreach (var id in _grid.Neighbors(defender.CurrentCell)) recipients.Add(id);
        return recipients;
    }

    private void BroadcastAttackResult(IEnumerable<int> recipientCharacterIds, in AttackResponse response)
    {
        foreach (var id in recipientCharacterIds)
            try
            {
                if (_players.TryGetValue(id, out var recipient))
                    recipient.Session.Send(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} attack-result send to character {RecipientId} failed", MapId,
                    id);
            }
    }

    /// <summary>XP hook called once a monster's death resolves its killer.</summary>
    /// <param name="partyMemberIds">
    ///     Killer's full party roster; null/empty for solo. The base gain above is always solo/killer-only; a
    ///     separate flat party bonus (10/20/30/50% of raw XP by 2-5 present members) goes to every present,
    ///     non-dead member in this same zone, including the killer again (verified: the killer's own record
    ///     also matches the source's party-name filter, which does not exclude it).
    /// </param>
    public void GrantMonsterKillExperience(int killerCharacterId, int monsterLevel, int monsterGeneralExperience,
        IReadOnlyList<int>? partyMemberIds = null)
    {
        if (!_players.TryGetValue(killerCharacterId, out var state))
            return;

        var fixedLevel = ExperienceFormulas.ReturnFixedLevel(state.Level);
        var rawGain = ExperienceFormulas.ComputeMonsterKillExperience(fixedLevel, monsterLevel,
            monsterGeneralExperience);
        var finalGain = ExperienceFormulas.ApplyRebirthDivisor(rawGain, state.Level);
        if (finalGain > 0)
            ApplyExperienceGain(state, finalGain);

        if (partyMemberIds is not { Count: > 0 })
            return;

        List<PlayerRuntimeState>? present = null;
        foreach (var memberId in partyMemberIds)
            if (_players.TryGetValue(memberId, out var member) && !member.IsDead)
                (present ??= []).Add(member);

        if (present is not { Count: >= 2 })
            return;

        var bonus = ExperienceFormulas.ComputePartyBonusExperience(present.Count, monsterGeneralExperience);
        if (bonus <= 0)
            return;

        foreach (var member in present)
            ApplyExperienceGain(member, bonus);

        // Local, not a new Zone method: mirrors MyUtil::ProcessForExperience's own per-recipient level-up loop
        // (S07_MyGame05.cpp:3916 calls it once per present party member too, not just the killer).
        void ApplyExperienceGain(PlayerRuntimeState target, int gain)
        {
            var previousExperience = target.Experience;
            target.Experience += gain;
            dirtyTracker.MarkDirty(target.CharacterId, DirtyFlags.Progression);

            var levelUp = LevelProgressionCalculator.ResolveLevelUp(previousExperience, gain, worldData.LevelsByLevel);
            if (!levelUp.LeveledUp)
                return;

            target.Level = levelUp.NewLevel;
            target.StatPoints += levelUp.StatPointsGranted;
            target.SkillPoints += levelUp.SkillPointsGranted;

            var equipmentContainer = target.Inventory.GetContainer(ContainerMatrix.Equipment);
            var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
                ? petStack.ItemId
                : 0;
            var petContribution = PetGrowthCalculator.Compute(petItemId, target.PetGrowth, target.PetActivity,
                worldData.ItemsById);
            var attributes = new CharacterBaseAttributes(target.StatVit, target.StatStr, target.StatInt,
                target.StatDex, target.Level, target.Tribe, target.Title, target.Halo, target.RebirthCount);
            var stats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, target.Buffs,
                petContribution);

            target.Stats = stats;
            target.MaxLife = stats.MaxLife;
            target.MaxMana = stats.MaxMana;

            // SetBasicAbilityFromEquip's aMaxLifeValue/aMaxManaValue cache-write above happens unconditionally;
            // only the actual heal is gated on the character being alive (S07_MyGame03.cpp:285-289).
            if (target.Life > 0)
            {
                target.Life = stats.MaxLife;
                target.Mana = stats.MaxMana;
            }

            dirtyTracker.MarkDirty(target.CharacterId, DirtyFlags.Vitals);
        }
    }

    /// <summary>
    ///     Quest kill-hook alongside <see cref="GrantMonsterKillExperience" />, from the same kill. Increments
    ///     <see cref="PlayerRuntimeState.QuestKillCounter" /> by 1 when the killer's active quest is qSort 1 or
    ///     5 and <paramref name="monsterId" /> matches <see cref="PlayerRuntimeState.QuestTargetPhase" />, but
    ///     only while the counter is still below its target (qSort 1: below Solution2; qSort 5: still 0) --
    ///     this is the clamp, not an unbounded increment. Party propagation is not modeled here.
    /// </summary>
    public void ApplyQuestKillProgress(int killerCharacterId, int monsterId)
    {
        if (!_players.TryGetValue(killerCharacterId, out var state))
            return;

        if (state.QuestActiveFlag != 1 || state.QuestTargetPhase != monsterId)
            return;

        switch (state.QuestSort)
        {
            case 1:
                var quest = _questCatalog.TryGet(state.Tribe, state.QuestStepPermanent);
                if (quest is null)
                    return;
                if (state.QuestKillCounter < (quest.Quest.Solution2 ?? 0))
                {
                    state.QuestKillCounter++;
                    dirtyTracker.MarkDirty(killerCharacterId, DirtyFlags.Progression);
                }

                break;
            case 5:
                if (state.QuestKillCounter < 1)
                {
                    state.QuestKillCounter++;
                    dirtyTracker.MarkDirty(killerCharacterId, DirtyFlags.Progression);
                }

                break;
        }
    }

    /// <summary>
    ///     Runs every registered <see cref="ISimulationSystem" /> in declared order, once per frame with a whole 500 ms
    ///     legacy tick due.
    /// </summary>
    private void Simulate(int legacyTicksElapsed)
    {
        if (legacyTicksElapsed <= 0)
            return;

        foreach (var system in simulationSystems)
            try
            {
                system.Simulate(this, legacyTicksElapsed);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} simulation system {System} failed", MapId,
                    system.GetType().Name);
            }
    }

    /// <summary>
    ///     Keep-alive rebroadcast: re-emits every avatar's current state to its surroundings every 3.5 s even
    ///     when idle, so late-arriving or packet-lossy neighbors converge.
    /// </summary>
    private void RebroadcastAvatars()
    {
        // Direct enumeration (no Values snapshot): ConcurrentDictionary's enumerator is lock-free, and the
        // tick thread is the only mutator anyway.
        foreach (var (characterId, state) in _players)
        {
            if (_clock - state.LastAvatarRebroadcastAt < SimulationClock.AvatarRebroadcastInterval)
                continue;

            state.LastAvatarRebroadcastAt = _clock;

            var neighbors = _grid.Neighbors(state.CurrentCell).Where(id => id != characterId).ToArray();
            BroadcastAvatarAction(neighbors, state);
        }
    }

    /// <summary>Keep-alive rebroadcast for monsters, 5 s cadence.</summary>
    private void RebroadcastMonsters()
    {
        foreach (var monster in _monsters.Values)
        {
            if (_clock - monster.LastRebroadcastAt < SimulationClock.MonsterRebroadcastInterval)
                continue;

            monster.LastRebroadcastAt = _clock;
            BroadcastMonsterAction(monster, 0);
        }
    }

    /// <summary>Keep-alive rebroadcast for ground items, 5 s cadence.</summary>
    private void RebroadcastGroundItems()
    {
        foreach (var (index, item) in _groundItems)
        {
            var last = _groundItemLastRebroadcast.TryGetValue(index, out var t) ? t : TimeSpan.MinValue;
            if (_clock - last < SimulationClock.GroundItemRebroadcastInterval)
                continue;

            _groundItemLastRebroadcast[index] = _clock;
            BroadcastGroundItemAction(item, 0);
        }
    }

    /// <summary>60 s lifetime sweep -- despawns and broadcasts every expired ground item still present.</summary>
    private void ExpireGroundItems()
    {
        List<(int Index, GroundItemEntity Item)>? expired = null;
        foreach (var (index, item) in _groundItems)
            if (item.IsExpired(_clock))
                (expired ??= []).Add((index, item));

        if (expired is null)
            return;

        foreach (var (index, item) in expired)
            if (_groundItems.TryRemove(index, out _))
            {
                _groundItemLastRebroadcast.Remove(index);
                BroadcastGroundItemAction(item, 3);
            }
    }

    private void DrainClaimedGroundItemDespawns()
    {
        while (_claimedGroundItemDespawns.TryDequeue(out var item))
        {
            _groundItemLastRebroadcast.Remove(item.ServerIndex);
            BroadcastGroundItemAction(item, 3);
        }
    }

    /// <summary>Serialize-once broadcast for monster replication -- same pattern as <see cref="BroadcastAvatarAction" />.</summary>
    private void BroadcastMonsterAction(MonsterEntity monster, int checkChangeActionState)
    {
        var recipients = NeighborsOfPosition(monster.PosX, monster.PosZ).ToArray();
        if (recipients.Length == 0)
            return;

        var packet = BuildMonsterActionRecv(monster, checkChangeActionState);
        var total = FrameWriter.FrameSizeOf<MonsterReplicationResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in packet, span);

            foreach (var id in recipients)
                try
                {
                    if (_players.TryGetValue(id, out var recipient) &&
                        recipient.Session is ClientSession clientSession)
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Zone {MapId} monster broadcast to character {RecipientId} failed", MapId,
                        id);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static MonsterReplicationResponse BuildMonsterActionRecv(MonsterEntity monster,
        int checkChangeActionState)
    {
        return new MonsterReplicationResponse
        {
            ServerIndex = monster.ServerIndex,
            UniqueNumber = monster.UniqueNumber,
            Data = new ObjectForMonster
            {
                Index = monster.Template.MonsterId,
                Action = new ActionInfo
                {
                    Type = 0,
                    Sort = (int)monster.AiState,
                    Frame = 0,
                    Location = [monster.PosX, monster.PosY, monster.PosZ],
                    TargetLocation = [monster.PosX, monster.PosY, monster.PosZ],
                    Front = monster.Heading,
                    TargetFront = monster.Heading,
                    PetLocation = new float[3],
                    PetTargetLocation = new float[3],
                    PetFront = 0,
                    PetSort = 0,
                    TargetObjectSort = 0,
                    TargetObjectIndex = monster.TargetCharacterId ?? 0,
                    TargetObjectUniqueNumber = 0,
                    SkillNumber = 0,
                    SkillGradeNum1 = 0,
                    SkillGradeNum2 = 0,
                    SkillValue = 0
                },
                LifeValue = monster.Life
            },
            CheckChangeActionState = checkChangeActionState
        };
    }

    /// <summary>Serialize-once broadcast for ground-item replication -- same pattern as <see cref="BroadcastAvatarAction" />.</summary>
    private void BroadcastGroundItemAction(GroundItemEntity item, int checkChangeActionState)
    {
        var recipients = NeighborsOfPosition(item.PosX, item.PosZ).ToArray();
        if (recipients.Length == 0)
            return;

        var packet = BuildItemActionRecv(item, checkChangeActionState);
        var total = FrameWriter.FrameSizeOf<GroundItemReplicationResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in packet, span);

            foreach (var id in recipients)
                try
                {
                    if (_players.TryGetValue(id, out var recipient) &&
                        recipient.Session is ClientSession clientSession)
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Zone {MapId} ground-item broadcast to character {RecipientId} failed",
                        MapId, id);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static GroundItemReplicationResponse BuildItemActionRecv(GroundItemEntity item,
        int checkChangeActionState)
    {
        return new GroundItemReplicationResponse
        {
            ServerIndex = item.ServerIndex,
            UniqueNumber = item.UniqueNumber,
            Data = new ObjectForItem
            {
                Index = item.ItemId,
                Quantity = item.Quantity,
                Value = item.Value,
                SerialNumber = item.SerialNumber,
                Location = [item.PosX, item.PosY, item.PosZ],
                Master = item.Master,
                PartyName = item.PartyName,
                DropSort = item.DropSort,
                CreateTime = 0,
                PresentTime = 0,
                CreateState = 1,
                SocketGem = [item.SocketGem1, item.SocketGem2, item.SocketGem3]
            },
            CheckChangeActionState = checkChangeActionState
        };
    }

    private void HandleEnter(int characterId, PlayerEnterData data)
    {
        var state = new PlayerRuntimeState
        {
            CharacterId = characterId,
            Session = data.Session,
            Name = data.Name,
            Tribe = data.Tribe,
            Gender = data.Gender,
            HeadType = data.HeadType,
            FaceType = data.FaceType,
            Level = data.Level,
            MapId = data.MapId,
            PosX = data.PosX,
            PosY = data.PosY,
            PosZ = data.PosZ,
            Heading = data.Heading,
            Life = data.Life,
            MaxLife = data.MaxLife,
            Mana = data.Mana,
            MaxMana = data.MaxMana,
            FlushSequence = data.FlushSequence,
            LastMoveUtc = DateTime.UtcNow,
            LastAvatarRebroadcastAt = _clock,
            // Carried through an in-process handoff so a player mid-death who transfers zones before the
            // auto-revive fires doesn't silently come back "alive" with 0 HP on arrival.
            IsDead = data.IsDead,
            StatVit = data.StatVit,
            StatStr = data.StatStr,
            StatInt = data.StatInt,
            StatDex = data.StatDex,
            StatPoints = data.StatPoints,
            Title = data.Title,
            Halo = data.Halo,
            RebirthCount = data.RebirthCount,
            Experience = data.Experience,
            ContributionPoints = data.ContributionPoints,
            TeacherPoint = data.TeacherPoint,
            IsMuted = data.IsMuted,
            GuildId = data.GuildId,
            GuildName = data.GuildName,
            GuildRoleDb = data.GuildRoleDb,
            GuildCallName = data.GuildCallName,
            TribeRole = data.TribeRole,
            // No rebirth/tribe-transition system populates a real "previous tribe" yet -- defaults to the
            // character's current tribe (a "never transferred" inference).
            PreviousTribe = data.Tribe,
            // This zone's own clock plus whatever remained of the revive timer in the source zone.
            ReviveAtZoneClock = _clock + (data.ReviveRemaining ?? TimeSpan.Zero),
            // The only write site for this field: a one-shot ~10s combat grace period starting now, for every
            // arrival. Combat code must never write this field again after today.
            ZoneEntryAtZoneClock = _clock
        };

        // Items/Stats are already-computed data handed down through the command -- a plain copy, never a
        // catalog lookup, keeping this tick-thread method's cost independent of WorldDataCache size.
        if (data.Items is { } items)
            state.Inventory.Seed(items);
        if (data.Stats is { } stats)
            state.Stats = stats;
        if (data.Skills is { } skills)
        {
            var builder = ImmutableDictionary.CreateBuilder<byte, LearnedSkill>();
            foreach (var skill in skills)
                builder[skill.SlotIndex] = new LearnedSkill(skill.SkillId, skill.Grade);
            state.LearnedSkills = builder.ToImmutable();
        }

        if (data.FriendsBySlot is { } friends)
            foreach (var (slot, friendId) in friends)
                state.Friends[slot] = friendId;

        state.TeacherCharacterId = data.TeacherCharacterId;
        state.StudentCharacterId = data.StudentCharacterId;

        state.QuestStepPermanent = data.QuestProgress.StepPermanent;
        state.QuestActiveFlag = data.QuestProgress.ActiveFlag;
        state.QuestSort = data.QuestProgress.QSort;
        state.QuestTargetPhase = data.QuestProgress.TargetPhase;
        state.QuestKillCounter = data.QuestProgress.KillCounter;
        state.MissionJoinWar = data.MissionJoinWar;
        state.MissionKillOtherTribe = data.MissionKillOtherTribe;
        state.MissionKillMonster = data.MissionKillMonster;
        state.MissionPlayTime = data.MissionPlayTime;
        state.AutoHuntEnabled = data.AutoHuntEnabled;
        state.AutoHuntConfig = data.AutoHuntConfig;
        state.AutoLifeRatio = data.AutoLifeRatio;
        state.AutoManaRatio = data.AutoManaRatio;
        state.PetGrowth = data.PetGrowth;
        state.PetActivity = data.PetActivity;
        state.LastSeenPetItemId = data.Items is { } petScanItems
            ? PetSlots.ResolveEquippedPetItemId(petScanItems)
            : 0;

        var cell = _grid.CellOf(state.PosX, state.PosZ);
        state.CurrentCell = cell;

        if (!_players.TryAdd(characterId, state))
        {
            logger.LogWarning(
                "Character {CharacterId} entered zone {MapId} while already tracked -- ignoring duplicate Enter",
                characterId, MapId);
            return;
        }

        _grid.Add(characterId, cell);

        // Marked dirty on entry so a handoff's map change reaches SQL even if the player never moves again;
        // on a fresh world entry the sequence already equals the DB baseline, so this flush is a deliberate no-op.
        dirtyTracker.MarkDirty(characterId, DirtyFlags.Position);

        // Mutual visibility: existing neighbors learn about the new arrival, and vice versa. The self-spawn
        // packet is sent directly by the registration handler before this command is posted.
        var others = _grid.Neighbors(cell).Where(id => id != characterId).ToArray();

        // Direct send to each neighbor's own session for the new arrival's view of them; the new arrival
        // itself is announced to neighbors via the broadcast below. Swapping these would send the new
        // arrival's own data to itself and leave it blind to everyone already there.
        foreach (var otherId in others)
            if (_players.TryGetValue(otherId, out var other))
                SendAvatarAction(state.Session, other);

        BroadcastAvatarAction(others, state);
    }

    private void HandleLeave(int characterId, Zone? handoffTarget, (float X, float Y, float Z)? handoffPosition = null)
    {
        if (!_players.TryRemove(characterId, out var state))
            return;

        _grid.Remove(characterId, state.CurrentCell);

        if (handoffTarget is null)
            // Plain leave (disconnect). No despawn/logout opcode exists in the M1 client protocol -- nearby
            // clients simply stop receiving updates for this entity. A documented gap, not an oversight.
            return;

        // In-process map transfer: the live state is snapshotted into the Enter command and travels inside
        // it -- this zone has already forgotten the player (TryRemove above), so the character never exists
        // in two zones at once.
        var enterData = ZoneTransfer.CreateEnterData(state, handoffTarget.MapId, _clock, handoffPosition);

        if (!handoffTarget.Post(ZoneCommand.Enter(characterId, enterData)))
        {
            // The player is now in no zone, permanently invisible, while their client still believes it is in
            // the world. Fail loudly and drop the connection rather than leave a phantom.
            logger.LogError(
                "Zone {TargetMapId} inbox full: dropped handoff Enter for character {CharacterId} from zone {MapId} -- aborting session",
                handoffTarget.MapId, characterId, MapId);

            if (state.Session is ClientSession client)
                client.Abort(DisconnectReason.Faulted);
            return;
        }

        // Plain reference write: atomic, and a stale read by a racing movement handler is benign.
        if (state.Session is ZoneClientSession zoneSession)
            zoneSession.CurrentZone = handoffTarget;
    }

    /// <summary>
    ///     Kills <paramref name="characterId" /> in this zone: Life -&gt; 0, <see cref="PlayerRuntimeState.IsDead" />
    ///     set, and an automatic revive scheduled <see cref="SimulationClock.DeathReviveDelay" /> later. Public
    ///     and characterId-addressed so the combat handler never needs its own <see cref="PlayerRuntimeState" />
    ///     reference -- only this zone's own tick may construct/mutate one. A no-op if the character is not
    ///     tracked here, or already dead (so a duplicate killing blow never re-arms the revive timer).
    /// </summary>
    /// <remarks>
    ///     XP penalty on death is applied here, but only for <see cref="DeathCause.MonsterKill" /> -- a PvP
    ///     death instead rewards the killer (not implemented, see <see cref="Combat.CombatResolver" />'s
    ///     remarks) and does not dock the victim's XP.
    /// </remarks>
    public void ApplyDeath(int characterId, DeathCause cause = DeathCause.Unknown)
    {
        if (!_players.TryGetValue(characterId, out var state))
        {
            logger.LogWarning(
                "ApplyDeath({CharacterId}) on zone {MapId}: character not tracked here -- ignoring (already disconnected or mid-handoff)",
                characterId, MapId);
            return;
        }

        if (state.IsDead)
            return;

        state.Life = 0;
        state.IsDead = true;
        state.ReviveAtZoneClock = _clock + SimulationClock.DeathReviveDelay;

        dirtyTracker.MarkDirty(characterId, DirtyFlags.Vitals);

        if (cause == DeathCause.MonsterKill)
            ApplyDeathExperienceLoss(state);

        // Death pose (aAction.aSort = 12) so nearby clients see the character fall immediately. Self is
        // excluded: the combat handler tells the dying player's own client via combat-result packets instead.
        var deathAction = new ActionInfo
        {
            Type = 0,
            Sort = 12,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = new float[3],
            PetTargetLocation = new float[3],
            PetFront = 0,
            PetSort = 0,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };

        var neighbors = _grid.Neighbors(state.CurrentCell).Where(id => id != characterId).ToArray();
        BroadcastAvatarAction(neighbors, state, deathAction);
    }

    /// <summary>
    ///     The MvP XP-loss branch of <see cref="ApplyDeath" /> (<c>S07_MyGame02.cpp:3445-3489</c>): refuses
    ///     below level 10 or at/above the level cap (loses CP instead, <see cref="ExperienceFormulas.CpLossAtLevelCap" />).
    ///     A level outside the catalog contributes 0 (no loss).
    /// </summary>
    private void ApplyDeathExperienceLoss(PlayerRuntimeState state)
    {
        switch (state.Level)
        {
            case < ExperienceFormulas.MinimumLevelForDeathExperienceLoss:
                return;
            case >= ExperienceFormulas.MaxLimitLevel:
                state.ContributionPoints -= ExperienceFormulas.CpLossAtLevelCap;
                dirtyTracker.MarkDirty(state.CharacterId, DirtyFlags.Progression);
                return;
        }

        if (!worldData.LevelsByLevel.TryGetValue(state.Level, out var levelRow))
            return;

        var loss = ExperienceFormulas.ComputeDeathExperienceLoss(state.Experience, levelRow.ExpRangeMin);
        if (loss <= 0)
            return;

        state.Experience -= loss;
        dirtyTracker.MarkDirty(state.CharacterId, DirtyFlags.Progression);
    }

    /// <summary>Sweeps every dead player whose scheduled revive (<see cref="ApplyDeath" />) is due this tick.</summary>
    private void ProcessPendingRevives()
    {
        List<(int CharacterId, PlayerRuntimeState State)>? due = null;

        foreach (var (characterId, state) in _players)
        {
            if (!state.IsDead || _clock < state.ReviveAtZoneClock)
                continue;

            (due ??= []).Add((characterId, state));
        }

        if (due is null)
            return;

        foreach (var (characterId, state) in due)
            Revive(characterId, state);
    }

    /// <summary>
    ///     Executes a due revive: HP forced to 1 regardless of MaxLife, in place (same zone/position) -- the
    ///     legacy only auto-clears the death flag locally; an actual "return to town" transfer is always
    ///     client-driven (CZ_DEMAND_ZONE_SERVER_INFO_2), already handled by <c>ZoneMoveHandler</c>. A prior
    ///     version of this auto-timer also teleported to a hardcoded tribe capital, which silently misfired on
    ///     a player revived mid zone-handoff; reviving in place removes that bug class by construction.
    /// </summary>
    private void Revive(int characterId, PlayerRuntimeState state)
    {
        state.IsDead = false;
        state.Life = 1;

        dirtyTracker.MarkDirty(characterId, DirtyFlags.Vitals);

        SendAvatarAction(state.Session, state);
        var neighbors = _grid.Neighbors(state.CurrentCell).Where(id => id != characterId).ToArray();
        BroadcastAvatarAction(neighbors, state);
    }

    private void HandleMove(int characterId, in ActionInfo action)
    {
        if (!_players.TryGetValue(characterId, out var state))
            return;

        var now = DateTime.UtcNow;

        if (!movementRules.IsPlausible(state, in action, now, Geometry))
        {
            // Reject: reply with the player's own last-known-good state so the client corrects itself -- no
            // dedicated ForcePositionSync packet exists in the M1 protocol, so this reuses ZC_AVATAR_ACTION_RECV.
            SendAvatarAction(state.Session, state);
            return;
        }

        state.PosX = action.Location[0];
        state.PosY = action.Location[1];
        state.PosZ = action.Location[2];
        state.Heading = action.Front;
        state.LastMoveUtc = now;
        state.FlushSequence++;

        // Mirrors the legacy's persistent mDATA.aAction fields for every accepted action, not just movement --
        // sit/meditation and skill casts ride the same unified CZ_AVATAR_ACTION_SEND wire shape.
        state.ActionSort = action.Sort;
        state.ActionSkillNumber = action.SkillNumber;
        state.ActionSkillGradeNum1 = action.SkillGradeNum1;
        state.ActionSkillGradeNum2 = action.SkillGradeNum2;

        var newCell = _grid.CellOf(state.PosX, state.PosZ);
        _grid.Move(characterId, state.CurrentCell, newCell);
        state.CurrentCell = newCell;

        dirtyTracker.MarkDirty(characterId, DirtyFlags.Position);

        // Self is excluded: the legacy client applies its own movement locally (client-side prediction) and
        // does not need its own action echoed back to it.
        var neighbors = _grid.Neighbors(newCell).Where(id => id != characterId).ToArray();
        BroadcastAvatarAction(neighbors, state, action);

        if (action.Sort == 30)
            ApplySkillCast(state, action);
    }

    /// <summary>
    ///     Op156 CZ_UPDATE_PET_ACTION_SEND -- copies only the pet sub-fields of <paramref name="action" />,
    ///     matching the legacy handler exactly (no reply, no broadcast; the update rides along on the next
    ///     periodic full-avatar keep-alive rebroadcast instead).
    /// </summary>
    private void HandlePetAction(int characterId, in ActionInfo action)
    {
        if (!_players.TryGetValue(characterId, out var state))
            return;

        state.PetActionSort = action.PetSort;
        state.PetActionFront = action.PetFront;
        state.PetActionLocationX = action.PetLocation[0];
        state.PetActionLocationY = action.PetLocation[1];
        state.PetActionLocationZ = action.PetLocation[2];
        state.PetActionTargetLocationX = action.PetTargetLocation[0];
        state.PetActionTargetLocationY = action.PetTargetLocation[1];
        state.PetActionTargetLocationZ = action.PetTargetLocation[2];
    }

    /// <summary>
    ///     Non-attack skill cast (Sort=30). Damage-dealing skills do not go through here -- those ride
    ///     <c>CZ_PROCESS_ATTACK_SEND</c>'s <c>AttackActionValue1==2</c> path instead. Silent no-op on every
    ///     failure path, matching the legacy's own bare early-return contract (no dedicated failure packet).
    /// </summary>
    private void ApplySkillCast(PlayerRuntimeState state, ActionInfo action)
    {
        // One skill-cast per legacy tick. Null (never cast) always passes.
        if (state.LastSkillCastAtZoneClock is { } lastCast && _clock - lastCast < SimulationClock.LegacyTick)
            return;

        worldData.SkillsById.TryGetValue(action.SkillNumber, out var skillDef);
        var gradePoints = action.SkillGradeNum1 + action.SkillGradeNum2;
        var weaponItemId = state.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId;
        var weaponSort = weaponItemId is { } id && worldData.ItemsById.TryGetValue(id, out var weaponDef)
            ? (int?)weaponDef.Item.Sort
            : null;
        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;

        var result = SkillCastResolver.TryCast(skillDef, gradePoints, state.Mana, maxLife, weaponSort);
        if (!result.Success)
            return;

        state.LastSkillCastAtZoneClock = _clock;
        state.Mana -= result.ManaCost;
        dirtyTracker.MarkDirty(state.CharacterId, DirtyFlags.Vitals);

        switch (result.Kind)
        {
            case SkillEffectKind.SelfBuff:
                ApplySkillBuffWrites(state, result.BuffWrites);
                break;
            case SkillEffectKind.HealLife:
                ApplyTargetedHeal(action, true, result.HealAmount);
                break;
            case SkillEffectKind.HealMana:
                ApplyTargetedHeal(action, false, result.HealAmount);
                break;
        }
    }

    private void ApplySkillBuffWrites(PlayerRuntimeState state, ImmutableArray<SkillCastResolver.BuffWrite> writes)
    {
        if (writes.IsEmpty)
            return;

        var changedSlots = new int[35];
        foreach (var write in writes)
        {
            if (write.Slot is < 0 or >= 35) continue;
            state.Buffs.Buff[write.Slot * 2] = write.Value;
            state.Buffs.Buff[write.Slot * 2 + 1] = write.DurationTicks;
            changedSlots[write.Slot] = 1;
        }

        RecomputeStatsAndBroadcastBuffs(state, changedSlots);
    }

    /// <summary>
    ///     Targeted heal (skills 106-111): resolves the target against this same zone, clamps the flat heal
    ///     amount to remaining capacity (<c>S07_MyGame03.cpp:9500-9510/9563-9573</c>). A target at full HP/MP,
    ///     or not found/dead, silently receives nothing.
    /// </summary>
    private void ApplyTargetedHeal(ActionInfo action, bool isLife, int rawAmount)
    {
        if (rawAmount < 1)
            return;
        if (!_players.TryGetValue(action.TargetObjectIndex, out var target))
            return;
        if (target.UniqueNumber != unchecked((uint)action.TargetObjectUniqueNumber))
            return;
        if (target.IsDead)
            return;

        if (isLife)
        {
            var max = target.Stats?.MaxLife ?? target.MaxLife;
            var amount = Math.Min(rawAmount, max - target.Life);
            if (amount < 1) return;
            target.Life += amount;
        }
        else
        {
            var max = target.Stats?.MaxMana ?? target.MaxMana;
            var amount = Math.Min(rawAmount, max - target.Mana);
            if (amount < 1) return;
            target.Mana += amount;
        }

        dirtyTracker.MarkDirty(target.CharacterId, DirtyFlags.Vitals);
    }

    /// <summary>
    ///     Recomputes <see cref="PlayerRuntimeState.Stats" /> from the live Equipment container + current
    ///     <see cref="PlayerRuntimeState.Buffs" /> snapshot, and broadcasts the updated buff view to this
    ///     player and their AOI neighbors. Unlike the legacy's live-read wrappers,
    ///     <see cref="PlayerRuntimeState.Stats" /> is an explicit cache that must be refreshed on every buff change.
    /// </summary>
    internal void RecomputeStatsAndBroadcastBuffs(PlayerRuntimeState state, int[] changedSlots)
    {
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.Title, state.Halo, state.RebirthCount);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);

        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;
        var petContribution = PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity,
            worldData.ItemsById);

        state.Stats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution);

        var response = new AvatarEffectStateResponse
        {
            ServerIndex = state.CharacterId,
            UniqueNumber = state.UniqueNumber,
            EffectValue = state.Buffs.Buff,
            EffectValueState = changedSlots
        };

        state.Session.Send(response);
        foreach (var neighborId in _grid.Neighbors(state.CurrentCell))
        {
            if (neighborId == state.CharacterId) continue;
            if (_players.TryGetValue(neighborId, out var neighbor))
                neighbor.Session.Send(response);
        }
    }

    private static void SendAvatarAction(IPacketSession session, PlayerRuntimeState state)
    {
        session.Send(BuildAvatarActionRecv(state));
    }

    /// <summary>
    ///     Serialize-once broadcast: the frame is written to a rented buffer once and copied into each recipient's own
    ///     pipe.
    /// </summary>
    private void BroadcastAvatarAction(IReadOnlyList<int> recipientCharacterIds, PlayerRuntimeState state,
        ActionInfo? action = null)
    {
        if (recipientCharacterIds.Count == 0)
            return;

        var packet = action is null ? BuildAvatarActionRecv(state) : BuildAvatarActionRecv(state, action.Value);
        var total = FrameWriter.FrameSizeOf<AvatarActionResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in packet, span);

            foreach (var id in recipientCharacterIds)
                try
                {
                    if (_players.TryGetValue(id, out var recipient) &&
                        recipient.Session is ClientSession clientSession)
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    // A recipient whose transport is already gone must not abort the broadcast for every other one.
                    logger.LogError(ex, "Zone {MapId} broadcast to character {RecipientId} failed", MapId, id);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static AvatarActionResponse BuildAvatarActionRecv(PlayerRuntimeState state)
    {
        return BuildAvatarActionRecv(state, new ActionInfo
        {
            Type = 0,
            Sort = 0,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = new float[3],
            PetTargetLocation = new float[3],
            PetFront = 0,
            PetSort = 0,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        });
    }

    /// <summary>
    ///     Internal (not private): reused by <c>ZoneMoveHandler</c> to build the self-spawn packet for a
    ///     zone-transfer, with an explicit <paramref name="action" /> carrying the just-resolved arrival
    ///     position rather than <paramref name="state" />'s own (still the source zone's).
    /// </summary>
    internal static AvatarActionResponse BuildAvatarActionRecv(PlayerRuntimeState state, ActionInfo action)
    {
        return new AvatarActionResponse
        {
            ServerIndex = state.CharacterId,
            UniqueNumber = state.UniqueNumber,
            Data = new ObjectForAvatar
            {
                VisibleState = 0,
                SpecialState = 0,
                KillOtherTribe = 0,
                GoodFellow = 0,
                GuildName = "",
                GuildRole = 0,
                CallName = "",
                GuildMarkEffect = 0,
                Name = state.Name,
                Tribe = state.Tribe,
                PreviousTribe = 0,
                Gender = state.Gender,
                HeadType = state.HeadType,
                FaceType = state.FaceType,
                Level1 = state.Level,
                Level2 = 0,
                // Reflects the live Equipment container instead of a hardcoded blank.
                EquipForView =
                    EquipmentViewCodec.BuildEquipForView(state.Inventory.GetContainer(ContainerMatrix.Equipment)),
                AnimalNumber = 0,
                Title = state.Title,
                Halo = state.Halo,
                RebirthNum = state.RebirthCount,
                BattleTeam = 0,
                Action = action,
                MaxLifeValue = state.MaxLife,
                LifeValue = state.Life,
                MaxManaValue = state.MaxMana,
                ManaValue = state.Mana,
                EffectValueForView = new int[35],
                PartyName = "",
                DuelState = new int[3],
                PShopState = 0,
                PShopName = "",
                CostumeNumber = 0,
                BufEffectTimeState = 0,
                BufSort = 0,
                AutoState = 0,
                FishingState = 0,
                FishingStep = 0,
                FishingPoint = new float[3],
                RankPoint = 0,
                TargetState = 0,
                AnimalAbsorbState = 0,
                PetValid = 0,
                Unk1 = 0,
                PetLocation = new float[3],
                PetFrame = 0,
                Unk624 = 0,
                Unk625 = 0,
                UniqueSkillNumber = 0,
                UniqueSkillBuffTime = 0,
                CostumeState = 0,
                StellarCoreNumber = 0
            },
            CheckChangeActionState = 0
        };
    }

    /// <summary>
    ///     Resolves <c>{GameDataDirectory}/WORLD/Z{mapId:D3}.WM</c> against the process's current working
    ///     directory, matching the legacy <c>ServerInfo.ini</c>'s <c>DataDir=./DATA/</c> convention.
    /// </summary>
    private static ZoneGeometry? TryLoadGeometry(short mapId, GameServerOptions gameServerOptions,
        ILogger<Zone> zoneLogger)
    {
        var wmPath = Path.Combine(Directory.GetCurrentDirectory(), gameServerOptions.GameDataDirectory, "WORLD",
            $"Z{mapId:D3}.WM");

        if (!File.Exists(wmPath))
        {
            zoneLogger.LogWarning(
                "No world geometry found at {Path} for MapId {MapId} -- movement validation continues without terrain awareness",
                wmPath, mapId);
            return null;
        }

        try
        {
            return ZoneGeometryReader.Load(wmPath);
        }
        catch (Exception ex)
        {
            zoneLogger.LogError(ex, "Failed to load world geometry from {Path}", wmPath);
            return null;
        }
    }
}

/// <summary>
///     Op 23/129 (<c>UseInventoryItem</c> iSort==26 acquisition / <c>DrinkBottle</c> consumption) shared
///     self-mutation mirror for <see cref="PlayerRuntimeState.BottleSlots" />. <c>NewItemId</c> is null for a
///     consumption (the slot's item id is unchanged, only its count decrements) and non-null for an
///     acquisition (the slot now holds this item id, freshly refilled).
/// </summary>
public readonly record struct DrinkBottleZoneCommand(
    int CharacterId,
    int BottleIndex,
    int RemainingCount,
    int NewLife,
    int? NewItemId = null,
    EffectiveStats? UpdatedStats = null,
    TaskCompletionSource? Applied = null);

/// <summary>
///     Op 118 <c>HeroRanking</c> throttle-timestamp mirror -- the ranking query itself is read-only and answered
///     directly by the handler.
/// </summary>
public readonly record struct HeroRankingQueryZoneCommand(int CharacterId, bool Previous, TimeSpan QueriedAtZoneClock);

/// <summary>
///     Op 103/104/105 (<c>FishingLine</c>/<c>FishingProgress</c>/<c>FishingCatch</c>) shared state-machine
///     mirror -- the handler has already decided the outcome (water/geometry check, elapsed-time gate, RNG
///     roll all happen on the request thread reading <see cref="PlayerRuntimeState" /> directly); this command
///     only carries the already-resolved values across to the tick for the single-writer mutation + optional
///     AOI broadcast. <see cref="CastAtUtc" /> is null unless this specific call restamps the cast clock
///     (legacy <c>mFishingTickCount</c>).
/// </summary>
public readonly record struct FishingZoneCommand(
    int CharacterId,
    int NewFishingState,
    int NewFishingStep,
    bool CatchingFish,
    bool Broadcast,
    int? ActionSort,
    DateTime? CastAtUtc = null,
    TaskCompletionSource? Applied = null);

/// <summary>
///     Which AvatarStateFlag/AvatarStatUpdate pair (if any) <see cref="Zone.ApplyMountCommand" /> sends after
///     mirroring a <see cref="MountZoneCommand" />.
/// </summary>
public enum MountBroadcastKind : byte
{
    None,
    Mount,
    Dismount,
    AbsorbToggle
}

/// <summary>
///     Op 87/113 (<c>MountState</c>/<c>MountAbsorb</c>) self-mutation mirror. Nullable/optional-field shape,
///     same convention as <see cref="TribeProgressZoneCommand" />.
/// </summary>
public readonly record struct MountZoneCommand(
    int CharacterId,
    int? AnimalIndex = null,
    int? AnimalNumber = null,
    int? AnimalAbsorbState = null,
    int? Life = null,
    int? Mana = null,
    EffectiveStats? UpdatedStats = null,
    MountBroadcastKind Broadcast = MountBroadcastKind.None,
    TaskCompletionSource? Applied = null);

/// <summary>
///     Which AvatarStateFlag broadcast (if any) <see cref="Zone.ApplyCostumeCommand" /> sends for a
///     <see cref="CostumeZoneCommand" /> (op 90 only -- op 139 uses
///     <see cref="CostumeZoneCommand.FullActionRebroadcast" /> instead).
/// </summary>
public enum CostumeBroadcastKind : byte
{
    None,
    Equip,
    Remove
}

/// <summary>
///     Op 90/139 (<c>CostumeState</c>/<c>CostumeVisibility</c>) self-mutation mirror, same shape as
///     <see cref="MountZoneCommand" />.
/// </summary>
public readonly record struct CostumeZoneCommand(
    int CharacterId,
    int? CostumeIndex = null,
    int? CostumeNumber = null,
    int? CostumeState = null,
    int? WardrobeSlotCleared = null,
    int? Life = null,
    int? Mana = null,
    EffectiveStats? UpdatedStats = null,
    CostumeBroadcastKind Broadcast = CostumeBroadcastKind.None,
    bool FullActionRebroadcast = false,
    TaskCompletionSource? Applied = null);

/// <summary>
///     Which AvatarStateFlag broadcast (if any) <see cref="Zone.ApplyStellarCoreCommand" /> sends for a
///     <see cref="StellarCoreZoneCommand" />.
/// </summary>
public enum StellarCoreBroadcastKind : byte
{
    None,
    Equip,
    Remove
}

/// <summary>Op 153 <c>StellarCoreState</c> self-mutation mirror, same shape as <see cref="CostumeZoneCommand" />.</summary>
public readonly record struct StellarCoreZoneCommand(
    int CharacterId,
    int? CoreIndex = null,
    int? CoreNumber = null,
    int? WardrobeSlotCleared = null,
    int? Life = null,
    int? Mana = null,
    EffectiveStats? UpdatedStats = null,
    StellarCoreBroadcastKind Broadcast = StellarCoreBroadcastKind.None,
    TaskCompletionSource? Applied = null);

/// <summary>
///     Op 97/111 (<c>PlaytimeBuff</c>/<c>RankBuff</c>) self-mutation mirror, same shape as
///     <see cref="MountZoneCommand" />.
/// </summary>
public readonly record struct AvatarBuffZoneCommand(
    int CharacterId,
    int? StateTimeEffect = null,
    int? RankBuffType = null,
    int? Life = null,
    int? Mana = null,
    EffectiveStats? UpdatedStats = null,
    TaskCompletionSource? Applied = null);

/// <summary>
///     Op 157 <c>RuneSocket</c> self-mutation mirror; economy-adjacent, always posted via
///     <see cref="Zone.PostRuneSocketCommandAndWaitAsync" />.
/// </summary>
public readonly record struct RuneSocketZoneCommand(
    int CharacterId,
    int RuneIndex,
    int? RuneItemId,
    int? RuneStat,
    EffectiveStats? UpdatedStats,
    TaskCompletionSource? Applied = null);

/// <summary>Op 94/95 (<c>ContinueSkillStat</c>/<c>ContinueSkillUse</c>) auto-buff registration + activation mirror.</summary>
public readonly record struct AutoBuffZoneCommand(
    int CharacterId,
    ImmutableArray<(int SkillId, int Grade)>? RegisteredSkills = null,
    int? ManaAfterActivation = null,
    int? ActionSort = null,
    bool Broadcast = false,
    TaskCompletionSource? Applied = null);
