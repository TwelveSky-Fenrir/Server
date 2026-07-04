namespace Fenrir.Application.Game.Social.Mentor;

/// <summary>
///     Posted by <c>MentorStartHandler</c> AFTER it has already durably bonded both characters
///     (<c>MentorRepository.BondAsync</c>) to mirror ONE field onto ONE character's live
///     <c>Fenrir.Application.Game.World.PlayerRuntimeState</c> -- the cross-character twin of that same
///     handler's own same-character <c>PlayerRuntimeState.StudentCharacterId</c> write (which stays a direct,
///     self-only mutation, the same accepted posture <c>FriendRegistry</c>'s own callers use).
/// </summary>
/// <remarks>
///     WHY THIS TYPE EXISTS (review finding, Phase C/V6 Social): <c>MentorStartHandler</c> used to set
///     <c>student.TeacherCharacterId = masterId;</c> directly from the MASTER's own request thread -- unlike
///     the master's OWN <c>StudentCharacterId</c> write (always self, narrow, already-accepted), this reaches
///     across to a DIFFERENT character's live state, one that may be hosted by an entirely different
///     <c>Fenrir.Application.Game.World.Zone</c> (a different tick thread) than the one the master's own
///     request is running against. That is a genuine single-writer-invariant violation, not merely a narrow
///     documented exception -- this command routes the write through the STUDENT's own hosting zone instead,
///     exactly like <c>Fenrir.Application.Game.Inventory.InventoryZoneCommand</c>/
///     <c>Fenrir.Application.Game.Skills.SkillZoneCommand</c> already do for other cross-thread mirrors.
/// </remarks>
/// <param name="CharacterId">The character whose TeacherCharacterId this sets -- a no-op if they already left this zone.</param>
/// <param name="TeacherCharacterId">The new teacher (master) id.</param>
public readonly record struct MentorZoneCommand(int CharacterId, int TeacherCharacterId);
