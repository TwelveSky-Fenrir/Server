namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     Kill-feed leaderboard ranking key (<c>Zone.RecordEnemyKillForFeed</c>). Deliberately survives
    ///     <c>Zone.ClearKillFeedLeaderboard</c>, but is silently zeroed on every <c>Zone.HandleEnter</c>
    ///     (login AND in-process zone transfer) because it is absent from <c>PlayerEnterData</c>. Whether
    ///     legacy actually resets this on zone entry is unconfirmed against <c>Server/</c> — do not thread
    ///     this through <c>PlayerEnterData</c> without that citation.
    /// </summary>
    public int SessionKillCount { get; set; }
}
