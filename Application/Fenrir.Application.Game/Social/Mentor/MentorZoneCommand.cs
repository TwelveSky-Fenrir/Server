namespace Fenrir.Application.Game.Social.Mentor;

/// <summary>
///     Posted by <c>MentorStartHandler</c> after durably bonding both characters
///     (<c>MentorRepository.BondAsync</c>), to mirror TeacherCharacterId onto the STUDENT's live
///     <c>PlayerRuntimeState</c> -- the master's own <c>StudentCharacterId</c> stays a direct self-write,
///     but writing across to a different character (possibly hosted by a different
///     <see cref="Zone" />/tick thread) would violate the single-writer invariant, so this routes the
///     write through the student's own zone instead (same pattern as <c>InventoryZoneCommand</c>/
///     <c>SkillZoneCommand</c>).
/// </summary>
/// <param name="CharacterId">The character whose TeacherCharacterId this sets -- a no-op if they already left this zone.</param>
/// <param name="TeacherCharacterId">The new teacher (master) id.</param>
public readonly record struct MentorZoneCommand(int CharacterId, int TeacherCharacterId);
