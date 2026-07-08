namespace Fenrir.Application.Login.Domain.Avatars;

/// <summary>
///     One occupied character-select roster slot's fully resolved data -- everything
///     <see cref="Fenrir.Application.Login.Domain.LoginTrain.BuildAvatarSlots" /> needs to overlay onto one
///     AvatarRosterResponse, pre-resolved by <c>LoginService</c> (the only place with I/O access) so
///     <c>LoginTrain</c> itself stays I/O-free. See <c>BuildAvatarSlots</c>' own remarks for the full
///     "Account-login avatar roster population" legacy-behavior-translator contract this projects.
/// </summary>
/// <param name="Character">RS0 row for this character (usp_Character_GetAccountRoster).</param>
/// <param name="Items">
///     RS1 rows for this character only -- the caller (LoginService) has already filtered
///     <see cref="CharacterAccountRosterBundle.Items" /> down to this character's own <c>CharacterId</c>.
/// </param>
/// <param name="GuildName">
///     Live guild-membership lookup result (<c>IGuildRepository.GetByCharacterAsync</c>), "" if guildless --
///     same live-lookup posture <c>LoginTrain.BuildAvatarSlots</c>' own remarks already documented before this
///     type existed.
/// </param>
/// <param name="FriendNameBySlot">
///     Live friend-list lookup (<c>IFriendRepository.GetByCharacterAsync</c>), keyed by the friend's own
///     client-chosen slot index -- sparse by design, unfilled slots stay "".
/// </param>
/// <param name="Teacher">Live mentor-bond lookup (<c>IMentorRepository.GetForCharacterAsync</c>), "" if this character has no teacher.</param>
/// <param name="Student">Same lookup as <paramref name="Teacher" />, "" if this character has no student.</param>
public sealed record AvatarRosterEntry(
    CharacterRosterDto Character,
    IReadOnlyList<CharacterRosterItemDto> Items,
    string GuildName,
    IReadOnlyDictionary<byte, string> FriendNameBySlot,
    string Teacher,
    string Student);
