namespace Fenrir.Application.Game.Domain.Social.Mentor;

/// <summary>
///     Posted after both characters are already durably bonded, to mirror TeacherCharacterId onto the
///     student's live PlayerRuntimeState -- the master's own StudentCharacterId is a direct self-write, but
///     writing across to a different character (possibly on a different zone/tick thread) must route through
///     that character's own zone instead.
/// </summary>
/// <param name="CharacterId">The student -- a no-op if they already left this zone.</param>
public readonly record struct MentorZoneCommand(int CharacterId, int TeacherCharacterId);
