namespace Fenrir.Application.Game.Domain.Mounts;

/// <summary>
///     The <c>aAnimalExpActivity</c> packed integer: a single value holding both a mount's activity ("absorb"
///     fuel) and its accumulated experience together, as <c>activity * 1,000,000 + experience</c>. This is the
///     exact shape persisted in <c>game.Characters.MountExpActivity</c> (slot 0) -- the runtime keeps the two
///     halves decoded on <see cref="World.PlayerRuntimeState" /> (<see cref="World.PlayerRuntimeState.MountActivity" />
///     / <see cref="World.PlayerRuntimeState.MountAccumulatedExp" />), so this codec is the boundary translation:
///     decode on world-entry hydration, re-encode on the rare persistence write.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame03.cpp:6854-6877 (<c>ChangeAnimalActivityAndExp</c> -- the encoder:
///     activity clamped to 0-100, multiplied by 1,000,000, experience clamped to 0-100,000, summed) ;
///     Server/Header/function.h:2425-2432 (<c>GetAnimalActivity</c> = value / 1,000,000, <c>GetAnimalExp</c> =
///     value modulo 1,000,000) ; Server/Header/Protocol/DEFINE.h:612 (<c>MAX_PAT_ACTIVITY_SIZE</c> = 100) ;
///     Server/Header/Protocol/DEFINE.h:617 (<c>MAX_MOUNT_EXP</c> = 100,000).
///     <para>
///         Because experience is capped at 100,000 -- well under the 1,000,000 activity scale -- the two halves
///         can never collide, so the pack/unpack round-trips exactly. The legacy encoder's "anything below 1
///         becomes 0" phrasing is, for whole numbers, exactly the lower clamp bound applied here via
///         <see cref="Math.Clamp(int, int, int)" />.
///     </para>
/// </remarks>
public static class MountActivityExpCodec
{
    /// <summary>The activity half's positional scale in the packed value (<c>activity * 1,000,000</c>).</summary>
    public const int ActivityScale = 1_000_000;

    /// <summary><c>MAX_PAT_ACTIVITY_SIZE</c> -- the inclusive upper bound on a mount's activity.</summary>
    public const int MaxActivity = 100;

    /// <summary><c>MAX_MOUNT_EXP</c> -- the inclusive upper bound on a mount's accumulated experience.</summary>
    public const int MaxExp = 100_000;

    /// <summary>Activity clamped to 0-<see cref="MaxActivity" /> (anything below 1 collapses to 0).</summary>
    public static int ClampActivity(int activity)
    {
        return Math.Clamp(activity, 0, MaxActivity);
    }

    /// <summary>Experience clamped to 0-<see cref="MaxExp" /> (anything below 1 collapses to 0).</summary>
    public static int ClampExp(int experience)
    {
        return Math.Clamp(experience, 0, MaxExp);
    }

    /// <summary>
    ///     <c>ChangeAnimalActivityAndExp</c>: clamps both halves, then packs them as
    ///     <c>ClampActivity(activity) * 1,000,000 + ClampExp(experience)</c>.
    /// </summary>
    public static int Pack(int activity, int experience)
    {
        return ClampActivity(activity) * ActivityScale + ClampExp(experience);
    }

    /// <summary><c>GetAnimalActivity</c>: the activity half (packed value integer-divided by the scale).</summary>
    public static int Activity(int packed)
    {
        return packed / ActivityScale;
    }

    /// <summary><c>GetAnimalExp</c>: the experience half (packed value modulo the scale).</summary>
    public static int Exp(int packed)
    {
        return packed % ActivityScale;
    }

    /// <summary>
    ///     Feeds a mount-activity potion's activity value onto a current activity, clamped so the total never
    ///     exceeds <see cref="MaxActivity" /> (Server/ts25zone/S04_MyWork02.cpp:2363-2384,2476-2480 -- the
    ///     hotkey-item mount-activity feed path, potion type 16).
    /// </summary>
    public static int FeedActivity(int currentActivity, int addedActivity)
    {
        return ClampActivity(currentActivity + addedActivity);
    }
}
