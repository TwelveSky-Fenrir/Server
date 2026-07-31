namespace Fenrir.Cluster.Center.WorldState;

public interface IWorldStateAuthority
{
    public byte? HighTribe { get; }
    public Task InitializeAsync(CancellationToken ct);

    public ValueTask FlushIfDirtyAsync(CancellationToken ct);

    public void SetZone049State(int index, int state);

    public bool SetZoneTypeState(WorldZoneStateKind zone, int index, byte state, bool stampTime);

    public bool SetZone175TypeState(int i1, int i2, byte state);

    public bool SetTribeGuardState(int fromTribe, int toTribe, byte state);

    public bool SetSymbolWinTribe(byte tribe);

    public void StartSymbolBattle();

    public bool SetTribeSymbol(int symbolIndex, byte winnerTribe);

    public bool SetMonsterSymbol(byte winnerTribe);

    public void EndSymbolBattle();

    public bool SetAllianceState(byte tribe1, byte tribe2);

    public bool SetPossibleAlliance(byte tribe1, int date1, byte tribe2, int date2);

    public bool SetTribeVoteState(int tribe, byte state);

    public ValueTask RegisterTribeVoteCandidateAsync(byte tribe, byte slotIndex, int candidateCharacterId,
        short candidateLevel, int killOtherTribeCount, CancellationToken ct);

    public ValueTask ClearTribeVoteCandidateAsync(byte tribe, byte slotIndex, CancellationToken ct);

    public ValueTask AddTribeVotePointsAsync(byte tribe, byte slotIndex, int points, CancellationToken ct);

    public ValueTask PurgeTribeVoteCandidatesAsync(byte tribe, CancellationToken ct);

    public bool SetTribeSubMaster(byte tribe, byte slotIndex, string name);

    public bool ClearTribeSubMaster(byte tribe, byte slotIndex);

    public bool SetTribeRatioBonus(TribeRatioBonusKind kind, byte tribe, int rawValue);

    public bool SetTribeKillOtherTribeAddValueExclusive(byte tribe, int value);

    public bool SetTribeMasterCallAbility(byte tribe, int skillSort);

    public bool SetTribeDtmValue(byte tribe, int dtm);

    public void SetUpdateTribePointFlag(short value);

    public int[] SnapshotTribePoints();

    public void OverwriteTribePoints(ReadOnlySpan<int> totals);

    public void SetHighTribe(byte? tribeId);

    public bool TryConsumeUpdateTribePointFlag(short pendingValue, short consumedValue);
}
