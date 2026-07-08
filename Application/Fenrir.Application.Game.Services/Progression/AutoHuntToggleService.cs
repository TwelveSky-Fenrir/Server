using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Progression;

/// <summary>Business logic extracted from <c>AutoHuntToggleHandler</c> (CZ_AUTO_CONFIG_SEND, opcode 99).</summary>
public sealed class AutoHuntToggleService(ICharacterRepository characters, ILogger<AutoHuntToggleService> logger)
    : IAutoHuntToggleService
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

    public async ValueTask<AutoHuntToggleResult> ToggleAsync(int characterId, Zone zone, PlayerRuntimeState state,
        AutoHuntToggleRequest packet, CancellationToken cancellationToken)
    {
        if (packet.Sort is not (0 or 1))
            return new AutoHuntToggleResult(true, false);

        if (packet.Sort == 1)
        {
            var hasWeapon = state.Inventory.GetSlot(ContainerMatrix.Equipment, WeaponSlot) is not null;
            // Legacy's `!(!A && !B)` is `A || B`: either attack-type slot alone is a valid setup.
            var hasAttackSkill = packet.AutoHunt.AttackType.Length >= 3 &&
                                 (packet.AutoHunt.AttackType[0] != 0 || packet.AutoHunt.AttackType[2] != 0);

            if (DisallowedZones.Contains(zone.MapId) || !hasWeapon || !hasAttackSkill)
            {
                logger.LogDebug(
                    "Auto-hunt enable rejected for character {CharacterId} on map {MapId}: disallowedZone={DisallowedZone} hasWeapon={HasWeapon} hasAttackSkill={HasAttackSkill}",
                    characterId, zone.MapId, DisallowedZones.Contains(zone.MapId), hasWeapon, hasAttackSkill);
                return new AutoHuntToggleResult(true, false);
            }
        }

        var enabled = packet.Sort == 1;
        var configBytes = new byte[AutoHunt.WireSize];
        packet.AutoHunt.Write(configBytes);

        await characters.SetAutoHuntAsync(characterId, enabled, configBytes, cancellationToken);

        state.AutoHuntEnabled = enabled;
        state.AutoHuntConfig = packet.AutoHunt;

        return new AutoHuntToggleResult(false, enabled);
    }
}
