using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Admin;

public interface IMuteRepository
{
    /// <summary>Called once at world entry to seed <c>PlayerRuntimeState.IsMuted</c> (EnterWorldService).</summary>
    public ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct);

    /// <summary>
    ///     admin.usp_Mute_GetActiveForCharacters: one round trip covering every id in <paramref name="characterIds" />
    ///     -- the batched sibling <see cref="IsActiveForCharacterAsync" /> reads per-character. Returns only the
    ///     subset that is currently muted (character-level or via the owning account); an id with no active mute
    ///     is simply absent from the result, never a paired boolean. Called by <c>MuteRefreshPollHost</c>
    ///     (Application/Fenrir.Application.Game.Hosting) on a fixed interval for every online character, the
    ///     bounded-staleness approximation of legacy's per-message live mute recheck -- see that host's own
    ///     remarks for why a literal per-message requery is the wrong shape for Fenrir's
    ///     <c>IInlinePacketHandler&lt;T&gt;</c> chat-send handlers.
    /// </summary>
    public ValueTask<ImmutableArray<int>> GetActiveCharacterIdsAsync(IReadOnlyCollection<int> characterIds,
        CancellationToken ct);
}
