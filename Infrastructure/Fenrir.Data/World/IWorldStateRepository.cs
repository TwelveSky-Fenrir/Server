using System.Collections.ObjectModel;

namespace Fenrir.Data.World;

/// <summary>
///     SQL access surface for the RvR world-state substrate (game.WorldState/WorldStateTribes/
///     WorldStateAllianceOffers/TribeVotes) -- the in-process merge of the legacy ts25center hub's
///     worldinfo/tribeinfo tables. Fenrir.Application.Game's WorldStateService is the only intended caller:
///     it owns the in-memory cache and the write-behind cadence, so nothing else should hit this repository
///     directly on a per-tick basis.
/// </summary>
public interface IWorldStateRepository
{
    /// <summary>Idempotent bootstrap: seeds the WorldState singleton + 4 WorldStateTribes rows on first call only.</summary>
    public ValueTask EnsureInitializedAsync(CancellationToken ct);

    /// <summary>
    ///     All 3 result sets in one round trip. <c>Row</c> is null only if <see cref="EnsureInitializedAsync" />
    ///     was never called -- callers must treat that as a boot-time invariant violation, not a valid state.
    /// </summary>
    public ValueTask<(WorldStateRowDto? Row, ReadOnlyCollection<WorldStateTribeDto> Tribes,
        ReadOnlyCollection<WorldStateAllianceOfferDto> AllianceOffers)> GetAsync(CancellationToken ct);

    /// <summary>Replaces every scalar field of the WorldState singleton row (game.usp_WorldState_Update).</summary>
    public ValueTask UpdateAsync(byte? zone038WinTribe, int? zone038WinTribeTime, bool tribeSymbolBattle,
        byte? monsterSymbol, int? monsterSymbolEndTime, byte? highTribe, short updateTribePoint, CancellationToken ct);

    /// <summary>Replaces one tribe's WorldStateTribes row (game.usp_WorldStateTribe_Update).</summary>
    public ValueTask UpdateTribeAsync(byte tribeId, DateTime? symbolDate, bool hasSymbol, int points, bool isClosed,
        CancellationToken ct);

    /// <summary>Idempotent upsert of one (from, to) alliance offer pair (game.usp_WorldStateAllianceOffer_Set).</summary>
    public ValueTask SetAllianceOfferAsync(byte fromTribeId, byte toTribeId, bool isAccepted, CancellationToken ct);

    /// <summary>The Force Leader candidate slots for one tribe, ordered VotePoint DESC (game.usp_TribeVote_GetByTribe).</summary>
    public ValueTask<ReadOnlyCollection<TribeVoteDto>> GetTribeVotesAsync(byte tribeId, CancellationToken ct);

    /// <summary>
    ///     Registers (or displaces) one Force Leader candidate slot (game.usp_TribeVote_Register). The caller
    ///     must already have verified the displacement is legal and that the candidate holds no other slot in
    ///     this tribe -- see <see cref="Fenrir.Application.Game.World.WorldState.WorldStateService.RegisterTribeVoteCandidateAsync" />.
    /// </summary>
    public ValueTask RegisterTribeVoteCandidateAsync(byte tribeId, byte slotIndex, int candidateCharacterId,
        short candidateLevel, int killOtherTribeCount, CancellationToken ct);

    /// <summary>Adds one voter's points onto their chosen candidate slot (game.usp_TribeVote_AddPoints).</summary>
    public ValueTask AddTribeVotePointsAsync(byte tribeId, byte slotIndex, int points, CancellationToken ct);

    /// <summary>Wipes every candidate slot for one tribe, starting a fresh cycle (game.usp_TribeVote_ClearTribe).</summary>
    public ValueTask ClearTribeVotesAsync(byte tribeId, CancellationToken ct);
}
