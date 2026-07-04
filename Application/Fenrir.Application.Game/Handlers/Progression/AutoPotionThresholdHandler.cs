using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Progression;

/// <summary>
///     CZ_CHANGE_AUTO_INFO (opcode 86, verified <c>S04_MyWork02.cpp:11792</c>) -- persists the auto-potion
///     HP/MP thresholds. Any value outside 0..5 on either field is Quit()-worthy; a valid pair is a
///     completely silent handler (no ZC reply at all, verified).
/// </summary>
/// <remarks>
///     <see cref="PlayerRuntimeState.AutoLifeRatio" />/<see cref="PlayerRuntimeState.AutoManaRatio" /> are
///     written directly from this request thread, NOT via a <c>Zone</c> tick-mirrored command -- the same
///     "own-character, non-economy scalar preference" exception <see cref="PlayerRuntimeState.Friends" />/
///     <c>TeacherCharacterId</c> already document: nothing else ever reads or writes these two fields for
///     ANY other character, and no item/money value object is involved, so the strict single-writer
///     posture <see cref="PlayerRuntimeState.EconomyActionLock" /> exists to protect does not apply here.
/// </remarks>
public sealed class AutoPotionThresholdHandler(CharacterRepository characters)
    : IAsyncPacketHandler<AutoPotionThresholdRequest>
{
    public async ValueTask HandleAsync(AutoPotionThresholdRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (packet.Value01 is < 0 or > 5 || packet.Value02 is < 0 or > 5)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var lifeRatio = (byte)packet.Value01;
        var manaRatio = (byte)packet.Value02;

        await characters.SetAutoPotionThresholdAsync(characterId, lifeRatio, manaRatio, cancellationToken);

        state.AutoLifeRatio = lifeRatio;
        state.AutoManaRatio = manaRatio;
    }
}
