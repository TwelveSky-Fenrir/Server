using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Login.Abstractions.CreateAvatar;

public enum CreateAvatarOutcome
{
    InvalidWeapon,
    Success,
    Failure,

    /// <summary>
    ///     The requested tribe is the single currently-dominant tribe (its own point total has reached the
    ///     100-point floor) -- see <see cref="Fenrir.Application.Login.Domain.Avatars.TribeDominanceGate" />.
    /// </summary>
    DominantTribeBlocked,

    /// <summary>
    ///     Tribe value 3 (the fourth faction) was requested while
    ///     <see cref="Fenrir.Application.Login.Domain.LoginServerOptions.EnableFourthFaction" /> is in its
    ///     default/legacy-matching disabled state -- see
    ///     <see cref="Fenrir.Application.Login.Domain.Avatars.FourthFactionGate" />. Same disconnect treatment
    ///     as <see cref="InvalidWeapon" />: the legacy source terminates the session outright here, with no
    ///     create-avatar response sent.
    /// </summary>
    FourthFactionDisabled,

    /// <summary>
    ///     The requested slot already holds a character for this account -- Server/ts25login/S04_MyWork02.cpp:
    ///     625-635's combined slot-occupied/name-empty test. Same disconnect treatment as
    ///     <see cref="InvalidWeapon" />: no create-avatar response is sent, the session is simply terminated.
    ///     (The name-empty half of that same combined test is handled earlier, structurally, by
    ///     CreateAvatarHandler before this service is ever called.)
    /// </summary>
    SlotOccupied
}

public readonly record struct CreateAvatarResult(CreateAvatarOutcome Outcome, AvatarInfo AvatarInfo);

public interface ICreateAvatarService
{
    public ValueTask<CreateAvatarResult> CreateAvatarAsync(
        int accountId,
        byte avatarPost,
        string avatarName,
        byte tribe,
        byte previousTribe,
        byte gender,
        byte head,
        byte face,
        int weapon,
        CancellationToken cancellationToken);
}
