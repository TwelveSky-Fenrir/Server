namespace Fenrir.Application.Game.Stats.Context;

/// <summary>
///     Ridden-mount contribution snapshot consumed by <c>StatCalculator</c>. Populated at construction
///     (EquipmentService / MountStateService) only for an actively-ridden mount; a default value contributes
///     nothing. <see cref="AnimalNumber" /> keys the per-stat grade markers + absorb value out of the mount
///     base row inside the stat pass; <see cref="RolledPower" /> and <see cref="Activity" /> drive the flat
///     rolled-point bonus (gated on activity &gt; 0). The grade is a per-stat vector resolved from the base
///     row, never a scalar, so no scalar grade field lives here.
/// </summary>
public readonly record struct MountContext(
    int AnimalNumber = 0,
    bool AbsorbActive = false,
    int AbsorbValue = 0,
    int RolledPower = 0,
    int Activity = 0);
