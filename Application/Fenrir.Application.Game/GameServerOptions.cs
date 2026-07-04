namespace Fenrir.Application.Game;

/// <summary>
///     Bound from the <c>Game</c> configuration section. One process = one shard hosting the disjoint set of maps
///     assigned to <see cref="ShardId" />.
/// </summary>
public sealed class GameServerOptions
{
    public int Port { get; init; } = 1100;

    /// <summary>
    ///     Identifies this process's row in <c>runtime.GameServerDirectory</c> and the key looked up in
    ///     <c>admin.ShardMapAssignments</c>.
    /// </summary>
    public byte ShardId { get; init; } = 1;

    /// <summary>
    ///     Dev-only NTFS junction onto the legacy DATA tree (not committed -- multi-hundred-MB external asset). Resolved
    ///     against cwd, not <c>AppContext.BaseDirectory</c>.
    /// </summary>
    public string GameDataDirectory { get; init; } = "GameData";

    /// <summary>Advertised to LoginServer via the directory heartbeat; not a client-visible legacy value.</summary>
    public string PublicHost { get; init; } = "127.0.0.1";

    public int TickRateHz { get; init; } = 20;

    /// <summary>Interest-management cell size (view radius).</summary>
    public float AoiCellSize { get; init; } = 75f;

    /// <summary>
    ///     Anti-speed-hack budget. No legacy source documents an exact speed for M1's map, so this is a generous
    ///     placeholder, not real game-balance tuning.
    /// </summary>
    public float MaxPlausibleSpeedPerSecond { get; init; } = 20f;

    /// <summary>How often this shard refreshes its <c>runtime.GameServerDirectory</c> heartbeat row.</summary>
    public int HeartbeatIntervalSeconds { get; init; } = 5;

    /// <summary>Informational only in M1, not enforced as a hard connection cap.</summary>
    public int Capacity { get; init; } = 300;
}
