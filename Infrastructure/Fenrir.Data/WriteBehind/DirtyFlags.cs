namespace Fenrir.Data.WriteBehind;

// Vitals and Progression are raised (Zone.GrantMonsterKillExperience / Zone.ApplyDeath) but have no flush reader yet.
[Flags]
public enum DirtyFlags : byte
{
    None = 0,
    Position = 1 << 0,
    Vitals = 1 << 1,
    Progression = 1 << 2
}
