namespace Fenrir.Data.WriteBehind;

// Vitals and Progression are raised via PlayerRuntimeState.MarkProgressDirty (combat, death/XP-loss, revive,
// skill cast, quest/pet/rebirth/CP mirrors) and flushed by Fenrir.Application.Game.Hosting.World's
// ProgressWriteBehindHost, called from PositionWriteBehindHost's single write-behind loop -- see
// ProgressWriteBehindHost's remarks for why Position and Vitals/Progression share one DirtyTracker<int> drain
// instead of each having their own independently-timed flusher.
[Flags]
public enum DirtyFlags : byte
{
    None = 0,
    Position = 1 << 0,
    Vitals = 1 << 1,
    Progression = 1 << 2
}
