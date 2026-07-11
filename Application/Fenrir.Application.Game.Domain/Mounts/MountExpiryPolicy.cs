namespace Fenrir.Application.Game.Domain.Mounts;

/// <summary>
///     The expired-mount auto-dismount rule: when a character is in the actively-mounted band and the mount's
///     remaining rental time has run out, force a dismount. Pure -- shared by the discrete world-registration
///     path (the confirmed legacy trigger) and the per-real-minute
///     <see cref="Simulation.MountExpiryCountdownSystem" />.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame03.cpp:6360-6363 (inside <c>MyUtil::CheckRegisterAvatar</c>, the
///     zone-registration refresh) -- if the mount's remaining time (<c>aAnimalTime</c>) is below 1 and
///     <c>aAnimalIndex</c> is in the mounted band (>= 10), subtract 10 from <c>aAnimalIndex</c> (force-dismount).
///     <para>
///         IMPORTANT SCOPE FLAG: the cited legacy check runs on avatar registration into the zone, a discrete
///         event, NOT on a per-minute schedule. No cited line performs a per-minute decrement of
///         <c>aAnimalTime</c>/<c>aAnimalAbsorbTime</c>; the behavior contract explicitly carries the "per-minute
///         absorb/expiry countdown" forward as unconfirmed pending a <c>cpp-zone-gameplay-analyst</c> re-check.
///         This policy therefore only encodes the confirmed check-and-dismount; it deliberately does NOT
///         decrement any timer. Until a grant path and a confirmed decrement path both land, the rule is inert
///         in practice (a mount can only enter the band via op87 Sort 3, which requires <c>aAnimalTime</c> >= 1,
///         and nothing lowers it) -- the same "real but currently unreachable" posture as the rest of the mount
///         family.
///     </para>
/// </remarks>
public static class MountExpiryPolicy
{
    /// <summary>
    ///     True when <paramref name="animalIndex" /> is in the actively-mounted band (>=
    ///     <see cref="MountStateResolver.SlotCount" />)
    ///     and <paramref name="animalTime" /> has run out (below 1).
    /// </summary>
    public static bool IsExpiredWhileMounted(int animalIndex, int animalTime)
    {
        return animalIndex >= MountStateResolver.SlotCount && animalTime < 1;
    }

    /// <summary>The new <c>aAnimalIndex</c> after a forced dismount (leaves the mounted band: index minus 10).</summary>
    public static int Dismounted(int animalIndex)
    {
        return animalIndex - MountStateResolver.SlotCount;
    }
}
