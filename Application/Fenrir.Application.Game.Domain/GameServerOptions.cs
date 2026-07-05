namespace Fenrir.Application.Game.Domain;

/// <summary>
///     Bound from the <c>Game</c> configuration section. One process = one shard hosting the disjoint set of maps
///     assigned to <see cref="ShardId" />.
/// </summary>
public sealed class GameServerOptions
{
    public int Port { get; set; } = 1100;

    /// <summary>
    ///     Identifies this process's row in <c>runtime.GameServerDirectory</c> and the key looked up in
    ///     <c>admin.ShardMapAssignments</c>.
    /// </summary>
    public byte ShardId { get; set; } = 1;

    /// <summary>
    ///     Dev-only NTFS junction onto the legacy DATA tree (not committed -- multi-hundred-MB external asset). Resolved
    ///     against cwd, not <c>AppContext.BaseDirectory</c>.
    /// </summary>
    public string GameDataDirectory { get; set; } = "GameData";

    /// <summary>Advertised to LoginServer via the directory heartbeat; not a client-visible legacy value.</summary>
    public string PublicHost { get; set; } = "127.0.0.1";

    public int TickRateHz { get; set; } = 20;

    /// <summary>Interest-management cell size (view radius).</summary>
    public float AoiCellSize { get; set; } = 75f;

    /// <summary>
    ///     Anti-speed-hack budget. No legacy source documents an exact speed for M1's map, so this is a generous
    ///     placeholder, not real game-balance tuning.
    /// </summary>
    public float MaxPlausibleSpeedPerSecond { get; set; } = 20f;

    /// <summary>How often this shard refreshes its <c>runtime.GameServerDirectory</c> heartbeat row.</summary>
    public int HeartbeatIntervalSeconds { get; set; } = 5;

    /// <summary>
    ///     How often this shard polls whether the weekly hero-ranking Current-&gt;Previous rollover
    ///     (<c>game.usp_HeroRanking_Rollover</c>) is due. The proc itself gates on a 7-day sentinel and is
    ///     safe to call redundantly from every shard, so an hourly poll is plenty -- this is a detection
    ///     cadence, not the rollover period itself.
    /// </summary>
    public int HeroRankingRolloverCheckIntervalMinutes { get; set; } = 60;

    /// <summary>Informational only in M1, not enforced as a hard connection cap.</summary>
    public int Capacity { get; set; } = 300;
}
