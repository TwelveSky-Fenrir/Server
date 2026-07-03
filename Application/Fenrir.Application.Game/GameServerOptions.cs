namespace Fenrir.Application.Game;

/// <summary>
///     Bound from the <c>Game</c> configuration section. One process = one shard hosting the DISJOINT set of
///     maps in <see cref="Maps" /> — one <see cref="World.Zone" /> actor per entry (ADR-0012). M1's deployment
///     is a single shard hosting map 1, but that is configuration, not code.
/// </summary>
public sealed class GameServerOptions
{
    /// <summary>
    ///     <c>[Zone.Server].Port</c> in the legacy ini (base port; M1 runs a single zone process, so no +number offset is
    ///     needed).
    /// </summary>
    public int Port { get; init; } = 1100;

    /// <summary>
    ///     Identifies this process's row in <c>runtime.GameServerDirectory</c> (ADR-0005-adjacent: not a wire-visible
    ///     value).
    /// </summary>
    public byte ShardId { get; init; } = 1;

    /// <summary>
    ///     Map ids hosted by this shard — <see cref="World.ZoneRegistry" /> builds one zone actor per entry.
    ///     Bound from <c>Game:Maps</c> (env form: <c>Game__Maps__0=1</c>, <c>Game__Maps__1=2</c>, ...). Must be
    ///     non-empty, positive, and duplicate-free (validator-enforced); across SHARDS the sets must also be
    ///     disjoint (ADR-0012 — not machine-checked yet, single-shard today).
    /// </summary>
    /// <remarks>
    ///     The compiled-in default is deliberately EMPTY: the runtime configuration binder CONCATENATES bound
    ///     array items onto whatever the property already returns (it never replaces), so a <c>[1]</c> default
    ///     here plus <c>Game__Maps__0=1</c> from AppHost would bind to <c>[1, 1]</c> and fail the duplicate
    ///     check at startup. The effective "nothing configured → map 1" default therefore lives in
    ///     <c>Servers/Fenrir.GameServer/appsettings.json</c> (<c>"Game": { "Maps": [ 1 ] }</c>), which the env
    ///     overrides KEY-BY-KEY (<c>Game:Maps:0</c>) before binding — no concatenation between providers.
    /// </remarks>
    public short[] Maps { get; init; } = [];

    /// <summary>
    ///     Legacy game-data folder (region/world files: <c>WORLD/Z0NN.WM</c>, etc.) -- a dev-only NTFS junction at
    ///     <c>Servers/Fenrir.GameServer/GameData</c> onto <c>Server/BuildEU33/DATA</c> by default (see
    ///     <c>.gitignore</c>: neither is committed, the legacy tree is a multi-hundred-megabyte external asset).
    ///     Resolved against the process's current working directory, not <c>AppContext.BaseDirectory</c> -- see
    ///     <see cref="World.Zone" />'s loader for why.
    /// </summary>
    public string GameDataDirectory { get; init; } = "GameData";

    /// <summary>Advertised to LoginServer via the directory heartbeat; not a client-visible legacy value.</summary>
    public string PublicHost { get; init; } = "127.0.0.1";

    /// <summary>Zone tick rate (architecture reference §10.1: 20 Hz).</summary>
    public int TickRateHz { get; init; } = 20;

    /// <summary>Interest-management cell size (architecture reference §10.3: "cellules 75 m" = view radius).</summary>
    public float AoiCellSize { get; init; } = 75f;

    /// <summary>
    ///     Anti-speed-hack budget (wire contract §7.1, "plausibilité de déplacement", architecture reference §6.5):
    ///     max straight-line distance a single tick's move may cover before it is rejected as implausible. No
    ///     legacy source documents an exact movement speed for M1's minimal map, so this is a generous, documented
    ///     placeholder (20 world units/s) meant to catch obviously-teleporting clients, not to enforce exact
    ///     game-balance movement speed (a gameplay-tuning concern, out of M1 scope).
    /// </summary>
    public float MaxPlausibleSpeedPerSecond { get; init; } = 20f;

    /// <summary>How often this shard refreshes its <c>runtime.GameServerDirectory</c> heartbeat row.</summary>
    public int HeartbeatIntervalSeconds { get; init; } = 5;

    /// <summary>
    ///     Max concurrent players this shard advertises to the directory (informational only in M1, not enforced as a
    ///     hard connection cap).
    /// </summary>
    public int Capacity { get; init; } = 300;
}
