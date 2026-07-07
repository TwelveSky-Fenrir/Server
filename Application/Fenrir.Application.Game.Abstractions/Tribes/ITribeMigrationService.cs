using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Tribes;

/// <summary>
///     CZ_CHANGE_TO_TRIBE4_SEND (op37) -- the fourth-tribe (Fujin) conversion/return behavior. See
///     <see cref="TribeMigrationOutcome" /> for the full outcome taxonomy and
///     <see cref="TribeMigrationOutcomeExtensions.RepliesWithFailure" /> for which outcomes reply with a
///     generic failure versus silently disconnect the session -- both are the caller (Handler)'s
///     responsibility, not this service's.
/// </summary>
public interface ITribeMigrationService
{
    public ValueTask<TribeMigrationOutcome> ConvertAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct);
}
