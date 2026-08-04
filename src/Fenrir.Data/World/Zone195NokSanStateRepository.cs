using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Data.World;

public sealed record Zone195NokSanStateRepository(ICaeriusNetDbContext Db) : IZone195NokSanStateRepository
{
    private const int AvatarNameLength = 13;

    public async ValueTask<(Zone195NokSanStateRowDto? State, ImmutableArray<Zone195NokSanCaptureRowDto> Captures)>
        LoadAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Zone195NokSanState_Get", 18).Build();
        var (states, captures) = await Db.QueryMultipleImmutableArrayAsync<Zone195NokSanStateRowDto,
            Zone195NokSanCaptureRowDto>(sp, ct);

        if (states.Length > 1)
            throw new InvalidOperationException("Nok-San singleton storage returned more than one state row.");

        return (states.Length == 0 ? null : states[0], captures);
    }

    public async ValueTask<bool> TrySaveAsync(Zone195NokSanStateRowDto state,
        ImmutableArray<Zone195NokSanCaptureRowDto> captures, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateState(state);

        var capture99 = GetCapture(captures, 99);
        var capture100 = GetCapture(captures, 100);
        var capture196 = GetCapture(captures, 196);

        var sp = new StoredProcedureParametersBuilder("game", "usp_Zone195NokSanState_TrySave", 1)
            .AddParameter("ExpectedRevision", state.Revision, SqlDbType.BigInt)
            .AddParameter("OwnerSlot0", state.OwnerSlot0, SqlDbType.TinyInt)
            .AddParameter("OwnerSlot2", state.OwnerSlot2, SqlDbType.TinyInt)
            .AddParameter("OwnerSlot3", state.OwnerSlot3, SqlDbType.TinyInt)
            .AddParameter("StonesHeld0", state.StonesHeld0, SqlDbType.TinyInt)
            .AddParameter("StonesHeld1", state.StonesHeld1, SqlDbType.TinyInt)
            .AddParameter("StonesHeld2", state.StonesHeld2, SqlDbType.TinyInt)
            .AddParameter("StonesHeld3", state.StonesHeld3, SqlDbType.TinyInt)
            .AddParameter("Capture99Phase", capture99.Phase, SqlDbType.TinyInt)
            .AddParameter("Capture99CharacterId", capture99.CapturerCharacterId, SqlDbType.Int)
            .AddParameter("Capture99Tribe", capture99.CapturerTribe, SqlDbType.TinyInt)
            .AddParameter("Capture99Name", capture99.CapturerName, SqlDbType.NVarChar, AvatarNameLength)
            .AddParameter("Capture99RemainingTime", capture99.RemainingTime, SqlDbType.Int)
            .AddParameter("Capture99PhaseAccumulatorTicks", capture99.PhaseAccumulatorTicks, SqlDbType.Int)
            .AddParameter("Capture100Phase", capture100.Phase, SqlDbType.TinyInt)
            .AddParameter("Capture100CharacterId", capture100.CapturerCharacterId, SqlDbType.Int)
            .AddParameter("Capture100Tribe", capture100.CapturerTribe, SqlDbType.TinyInt)
            .AddParameter("Capture100Name", capture100.CapturerName, SqlDbType.NVarChar, AvatarNameLength)
            .AddParameter("Capture100RemainingTime", capture100.RemainingTime, SqlDbType.Int)
            .AddParameter("Capture100PhaseAccumulatorTicks", capture100.PhaseAccumulatorTicks, SqlDbType.Int)
            .AddParameter("Capture196Phase", capture196.Phase, SqlDbType.TinyInt)
            .AddParameter("Capture196CharacterId", capture196.CapturerCharacterId, SqlDbType.Int)
            .AddParameter("Capture196Tribe", capture196.CapturerTribe, SqlDbType.TinyInt)
            .AddParameter("Capture196Name", capture196.CapturerName, SqlDbType.NVarChar, AvatarNameLength)
            .AddParameter("Capture196RemainingTime", capture196.RemainingTime, SqlDbType.Int)
            .AddParameter("Capture196PhaseAccumulatorTicks", capture196.PhaseAccumulatorTicks, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<bool>(sp, ct);
    }

    private static Zone195NokSanCaptureRowDto GetCapture(
        ImmutableArray<Zone195NokSanCaptureRowDto> captures, short mapId)
    {
        if (captures.Length != 3)
            throw new ArgumentException("A Nok-San save must include exactly maps 99, 100, and 196.", nameof(captures));

        Zone195NokSanCaptureRowDto? match = null;
        foreach (var capture in captures)
        {
            ValidateCapture(capture);
            if (capture.MapId != mapId)
                continue;

            if (match is not null)
                throw new ArgumentException("Each active Nok-San map may appear only once.", nameof(captures));

            match = capture;
        }

        return match ??
               throw new ArgumentException($"Nok-San map {mapId} is missing from this save.", nameof(captures));
    }

    private static void ValidateState(Zone195NokSanStateRowDto state)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(state.Revision);
        if (state.OwnerSlot0 > 4 || state.OwnerSlot2 > 4 || state.OwnerSlot3 > 4 ||
            state.StonesHeld0 > 4 || state.StonesHeld1 > 4 || state.StonesHeld2 > 4 || state.StonesHeld3 > 4)
            throw new ArgumentOutOfRangeException(nameof(state), "Nok-San owner and count values must be in 0..4.");
    }

    private static void ValidateCapture(Zone195NokSanCaptureRowDto capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        if (capture.MapId is not 99 and not 100 and not 196)
            throw new ArgumentOutOfRangeException(nameof(capture),
                "Only active Nok-San maps 99, 100, and 196 persist.");
        if (capture.Phase > 2 || capture.CapturerCharacterId < -1 || capture.CapturerTribe > 3 ||
            capture.CapturerName is null || capture.CapturerName.Length > AvatarNameLength ||
            capture.RemainingTime < 0 || capture.PhaseAccumulatorTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(capture),
                "The Nok-San capture state is structurally invalid.");
    }
}
