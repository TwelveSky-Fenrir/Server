using System.Collections.Concurrent;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     This character's guild, if any -- loaded once at world entry, same one-shot-cache posture as
    ///     <see cref="IsMuted" />. Null means "no guild". <see cref="GuildRoleDb" /> is the DB-side enum
    ///     (0 member/1 sub-master/2 master); see <see cref="Social.GuildRoleCodec" /> for the legacy wire's
    ///     inverted encoding.
    /// </summary>
    public int? GuildId { get; set; }

    public string GuildName { get; set; } = "";

    public byte GuildRoleDb { get; set; }

    /// <summary>
    ///     Cosmetic in-guild title (legacy gMemberCall, game.GuildMembers.CallName) -- loaded once at world entry
    ///     alongside <see cref="GuildId" />.
    /// </summary>
    public string GuildCallName { get; set; } = "";

    /// <summary>
    ///     Live guild-buff on/off gate for this session -- the primary field live combat/stat-formula
    ///     calculations are meant to read directly on every relevant calculation (multiple distinct legacy
    ///     formula sites gate their bonus application on this exact field: Server/Header/Protocol/MyFactor.cpp:
    ///     3151, :3323, :4102, :4443, :4574). Flipped false by <see cref="Zone.ApplyGuildBuffExpiryCommand" />
    ///     the instant a guild's buff reserve reaches exhaustion (see that method's own remarks) -- Fenrir has
    ///     no formula site wired to actually READ this yet (a separate, not-yet-implemented gap: applying the
    ///     guild-buff bonus itself), so this field is currently write-only within this cluster, same
    ///     "real but currently unreachable" posture as <see cref="VerifyEchoedActionState" />.
    /// </summary>
    public bool GuildBuffActive { get; set; }

    /// <summary>
    ///     Second in-memory mirror of the exact same on/off state as <see cref="GuildBuffActive" /> -- legacy
    ///     flips two distinct session fields together on every guild-buff expiry
    ///     (Server/ts25zone/UpperCom/S06_MyUpperCom02.cpp:359-378), not one; kept as a separate property here
    ///     rather than folded into <see cref="GuildBuffActive" /> so a future consumer of the second legacy
    ///     field has a faithful, independently-settable Fenrir counterpart to bind to. No current Fenrir
    ///     consumer reads this one either.
    /// </summary>
    public bool GuildBuffActiveMirror { get; set; }

    /// <summary>
    ///     This character's tribe role -- loaded once at world entry, matching <c>ReturnTribeRole</c>'s own
    ///     encoding directly (0 = regular, 1 = master, 2 = sub-master, 3 = elected tribe-council member
    ///     seated via the tribe-vote mechanism; see <c>Server/Header/function.h:92-114</c>).
    /// </summary>
    public byte TribeRole { get; set; }

    /// <summary>
    ///     This character's own friend list (slot -&gt; friend CharacterId), mutated directly by friend-add/
    ///     remove handlers on the request thread -- a deliberate exception to the single-writer invariant. A
    ///     zone-transfer handoff carries this same dictionary instance to the target zone and enumerates it on
    ///     that zone's own tick thread, so a concurrent Add/Remove must not throw during enumeration --
    ///     <see cref="ConcurrentDictionary{TKey,TValue}" /> makes that race safe.
    /// </summary>
    public ConcurrentDictionary<byte, int> Friends { get; } = new();

    /// <summary>
    ///     This character's teacher (master), if any -- mutated live by mentor start/end handlers (same request-thread
    ///     exception as <see cref="Friends" />). Null = no teacher.
    /// </summary>
    public int? TeacherCharacterId { get; set; }

    /// <summary>
    ///     This character's student, if any (only meaningful for a master) -- same posture as
    ///     <see cref="TeacherCharacterId" />.
    /// </summary>
    public int? StudentCharacterId { get; set; }
}
