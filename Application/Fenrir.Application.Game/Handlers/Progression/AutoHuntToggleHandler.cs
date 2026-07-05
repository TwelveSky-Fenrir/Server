using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Progression;

/// <summary>
///     CZ_AUTO_CONFIG_SEND (opcode 99) -- auto-hunt on/off toggle. Enabling requires an equipped weapon and a
///     configured attack skill. OPEN ISSUE: legacy also mentions unenumerated "level-gated battle zones", not
///     modeled here.
/// </summary>
public sealed class AutoHuntToggleHandler(ICharacterRepository characters) : IAsyncPacketHandler<AutoHuntToggleRequest>
{
    /// <summary>FEQUIP_TYPE::EWEAPON slot index.</summary>
    private const byte WeaponSlot = 7;

    /// <summary>
    ///     Zones where enabling auto-hunt is refused. Legacy's "zone 241" flag is actually 20 distinct server
    ///     numbers (241-249, 292-294, 311-312, 325-330), all refused here, not just 241.
    /// </summary>
    private static readonly HashSet<short> DisallowedZones =
    [
        38, 319, 320, 321, 322, 323,
        241, 242, 243, 244, 245, 246, 247, 248, 249,
        292, 293, 294,
        311, 312,
        325, 326, 327, 328, 329, 330
    ];

    public async ValueTask HandleAsync(AutoHuntToggleRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (packet.Sort is not (0 or 1))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (packet.Sort == 1)
        {
            var hasWeapon = state.Inventory.GetSlot(ContainerMatrix.Equipment, WeaponSlot) is not null;
            // Legacy's `!(!A && !B)` is `A || B`: either attack-type slot alone is a valid setup.
            var hasAttackSkill = packet.AutoHunt.AttackType.Length >= 3 &&
                                 (packet.AutoHunt.AttackType[0] != 0 || packet.AutoHunt.AttackType[2] != 0);

            if (DisallowedZones.Contains(zone.MapId) || !hasWeapon || !hasAttackSkill)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }
        }

        var enabled = packet.Sort == 1;
        var configBytes = new byte[AutoHunt.WireSize];
        packet.AutoHunt.Write(configBytes);

        await characters.SetAutoHuntAsync(characterId, enabled, configBytes, cancellationToken);

        state.AutoHuntEnabled = enabled;
        state.AutoHuntConfig = packet.AutoHunt;

        session.Send(new AutoHuntToggleResponse
        {
            ServerIndex = characterId, UniqueNumber = state.UniqueNumber, AutoState = packet.Sort
        });
    }
}
