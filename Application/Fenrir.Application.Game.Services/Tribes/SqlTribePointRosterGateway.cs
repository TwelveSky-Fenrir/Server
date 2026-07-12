using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Services.Tribes;

public sealed class SqlTribePointRosterGateway(ITribeRosterRepository roster) : ITribePointRosterGateway
{
    public async Task<IReadOnlyList<TribeRosterCharacterSnapshot>?> GetRosterAsync(CancellationToken ct)
    {
        var rows = await roster.GetForTribePointAsync(ct).ConfigureAwait(false);

        var snapshots = new TribeRosterCharacterSnapshot[rows.Length];
        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            snapshots[i] = new TribeRosterCharacterSnapshot(row.TribeId, row.Level1, row.Level2, row.RebirthCount);
        }

        return snapshots;
    }
}
