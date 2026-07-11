namespace Fenrir.Application.Login.Domain.Avatars;

public sealed record AvatarRosterEntry(
    CharacterRosterDto Character,
    IReadOnlyList<CharacterRosterItemDto> Items,
    string GuildName,
    IReadOnlyDictionary<byte, string> FriendNameBySlot,
    string Teacher,
    string Student);
