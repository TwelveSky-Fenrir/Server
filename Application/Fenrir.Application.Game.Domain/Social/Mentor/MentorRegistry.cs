namespace Fenrir.Application.Game.Domain.Social.Mentor;

/// <summary>Soft outcomes of CZ_TEACHER_ASK_SEND -- mirrors ZC_TEACHER_ANSWER_RECV's pre-check codes.</summary>
public enum MentorAskOutcome
{
    Sent,
    AskerBusy, // 3
    TargetBusy, // 5
    TargetAlreadyHasTeacher, // 6
    TargetAlreadyHasStudent // 7
}

/// <summary>
///     Process-wide teacher/student ("mentor") negotiation authority (named Mentor to avoid colliding with
///     the wire's own Teacher/Student AvatarInfo properties). The durable bond lives in
///     game.Characters.TeacherCharacterId/StudentCharacterId; this registry only tracks the
///     ask/cancel/answer handshake. Accepted state is currently remembered keyed by master (the original
///     asker) only, so only the asker side can consume <see cref="TryConsumeStart" /> -- see the
///     <c>LEGACY-PARITY RISK</c> remark on that method before relying on this as confirmed legacy behavior.
/// </summary>
public sealed class MentorRegistry
{
    /// <summary>master characterId -> accepted student characterId, awaiting CZ_TEACHER_START_SEND.</summary>
    private readonly Dictionary<int, int> _acceptedByMaster = new();

    /// <summary>
    ///     WS1.4: this family's own cross-shard ASK-PUBLISH-ONLY half, folded into <see cref="IsNegotiating" />
    ///     -- see <see cref="CrossShardNegotiationTracker" />'s own remarks. Unlike Party/Friend, no
    ///     <c>ISocialCrossShardRelayHandler</c> is registered for <c>SocialCrossShardRelayKind.Mentor</c> yet
    ///     (see <c>MentorAskService.AskAsync</c>'s own remarks for why), so only the OUTBOUND half of this
    ///     tracker is ever populated -- an inbound cross-shard mentor ask can never be delivered here today.
    ///     <see cref="TryCancel" /> still consumes an outbound entry so a master who published a cross-shard
    ///     ask that will never be answered is not stuck busy forever.
    /// </summary>
    private readonly CrossShardNegotiationTracker _crossShard = new();

    private readonly Lock _lock = new();
    private readonly Dictionary<int, int> _pendingByMaster = new();
    private readonly Dictionary<int, int> _pendingByStudent = new();

    /// <summary>
    ///     Mentor-family half of the legacy <c>CheckCommunityWork</c> exclusivity check. Public so sibling
    ///     negotiation families (e.g. Guild ask, see <c>GuildInviteService</c>) can compose a cross-family busy
    ///     check without duplicating this registry's own state. Also includes a still-open outbound
    ///     cross-shard ask.
    /// </summary>
    public bool IsNegotiating(int characterId)
    {
        lock (_lock)
        {
            return _pendingByMaster.ContainsKey(characterId) || _pendingByStudent.ContainsKey(characterId) ||
                   _acceptedByMaster.ContainsKey(characterId) || _acceptedByMaster.ContainsValue(characterId) ||
                   _crossShard.IsPending(characterId);
        }
    }

    /// <summary>
    ///     WS1.4 master-side cross-shard ask registration (ASK-PUBLISH-ONLY -- see <see cref="_crossShard" />'s
    ///     own remarks). Same negotiating gate as <see cref="TryAsk" />; the target-already-has-teacher/student
    ///     checks are not evaluable here (no local target state), so they are omitted -- caller-side risk
    ///     accepted the same way the level-gap check is for Party's own cross-shard path.
    /// </summary>
    public MentorAskOutcome TryAskCrossShard(int masterId, CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            if (IsNegotiating(masterId))
                return MentorAskOutcome.AskerBusy;

            return _crossShard.TryRegisterOutbound(masterId, ask) ? MentorAskOutcome.Sent : MentorAskOutcome.AskerBusy;
        }
    }

    public MentorAskOutcome TryAsk(int masterId, int studentId, bool targetAlreadyHasTeacher,
        bool targetAlreadyHasStudent)
    {
        lock (_lock)
        {
            if (IsNegotiating(masterId))
                return MentorAskOutcome.AskerBusy;
            if (IsNegotiating(studentId))
                return MentorAskOutcome.TargetBusy;
            if (targetAlreadyHasTeacher)
                return MentorAskOutcome.TargetAlreadyHasTeacher;
            if (targetAlreadyHasStudent)
                return MentorAskOutcome.TargetAlreadyHasStudent;

            _pendingByMaster[masterId] = studentId;
            _pendingByStudent[studentId] = masterId;
            return MentorAskOutcome.Sent;
        }
    }

    public bool TryCancel(int masterId, out int studentId)
    {
        lock (_lock)
        {
            if (_pendingByMaster.Remove(masterId, out studentId))
            {
                _pendingByStudent.Remove(studentId);
                return true;
            }

            if (_crossShard.TryConsumeOutbound(masterId, out var crossShardAsk))
            {
                studentId = crossShardAsk.TargetCharacterId;
                return true;
            }

            return false;
        }
    }

    public bool TryAnswer(int studentId, bool accepted, out int masterId)
    {
        lock (_lock)
        {
            if (!_pendingByStudent.Remove(studentId, out masterId))
                return false;

            _pendingByMaster.Remove(masterId);

            if (accepted)
                _acceptedByMaster[masterId] = studentId;

            return true;
        }
    }

    /// <summary>CZ_TEACHER_START_SEND -- consumes the accepted negotiation; only the master may call this.</summary>
    /// <remarks>
    ///     <b>LEGACY-PARITY RISK (open, not resolved by inference):</b> this master-only restriction traces
    ///     back to a research finding claiming teacher/student roles are fixed at ask-time. A direct re-read
    ///     of Server/ts25zone/S04_MyWork02.cpp:9406-9457 (MentorAnswer) shows BOTH the answering recipient
    ///     (line 9415) and the original asker (line 9447) are symmetrically promoted to negotiation state 3
    ///     on accept, and Server/ts25zone/S04_MyWork02.cpp:9459-9499 (MentorStart) only requires the SENDING
    ///     connection's own state to equal 3, with no check anywhere in that handler for which side sent the
    ///     original ask. Read literally, either mutually-accepted party could become teacher by transmitting
    ///     CZ_TEACHER_START_SEND first, with the resulting role determined by transmission order rather than
    ///     ask direction -- see lines 9489-9497 for the sender-determines-role field writes. It remains
    ///     possible the legacy CLIENT only ever sends this opcode from the original asker by convention
    ///     (client source is out of scope for this repository), so this has NOT been confirmed either way.
    ///     Do not widen this registry to accept a student-side <c>TryConsumeStart</c> without a fresh
    ///     <c>cpp-zone-gameplay-analyst</c> / <c>legacy-behavior-translator</c> finding confirming which
    ///     reading is legacy-accurate -- tracked here as an explicit open item, not a silently-kept guess.
    /// </remarks>
    public bool TryConsumeStart(int masterId, out int studentId)
    {
        lock (_lock)
        {
            return _acceptedByMaster.Remove(masterId, out studentId);
        }
    }
}
