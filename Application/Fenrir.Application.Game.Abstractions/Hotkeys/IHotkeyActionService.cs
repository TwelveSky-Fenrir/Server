using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Hotkeys;

public interface IHotkeyActionService
{
    public ValueTask<GenericActionResult> BindSkillAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int skillSlotIndex, int requestedGrade, int destinationPage, int destinationIndex,
        CancellationToken cancellationToken);

    public ValueTask<GenericActionResult> BindEmoticonAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int emoticonCode, int destinationPage, int destinationIndex, CancellationToken cancellationToken);

    public ValueTask<GenericActionResult> UnbindAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int page, int index, CancellationToken cancellationToken);

    public ValueTask<GenericActionResult> BindItemAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, CancellationToken cancellationToken);

    public ValueTask<GenericActionResult> WithdrawItemAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, CancellationToken cancellationToken);

    public ValueTask<GenericActionResult> RearrangeAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, CancellationToken cancellationToken);
}
