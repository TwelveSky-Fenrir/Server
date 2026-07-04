using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Progression;

/// <summary>
///     CZ_AUTO_CONFIG_SEND (opcode 99, verified <c>S04_MyWork02.cpp:13466-13614</c>) -- the auto-hunt
///     on/off toggle. <see cref="AutoHuntToggleRequest.Sort" /> must be 0 (disable) or 1 (enable), else
///     Quit(). Enabling is also refused in <see cref="DisallowedZones" />, without an equipped weapon, or
///     without a configured attack skill (<c>AttackType[0]</c>/<c>[2]</c> either non-zero). OPEN ISSUE: the
///     source also mentions unenumerated "level-gated battle zones" -- not guessed here, only the explicit
///     zone list is enforced. On success the 112-byte <see cref="Fenrir.Contracts.Packets.Shared.AutoHunt" />
///     blob is stored verbatim with no content validation; the bot loop itself (auto-attack/loot/potion once
///     enabled) is out of scope for this pass.
/// </summary>
/// <remarks>
///     Same "own-character, non-economy scalar preference, direct mutation" posture as
///     <see cref="AutoPotionThresholdHandler" /> -- see that type's own remarks.
/// </remarks>
public sealed class AutoHuntToggleHandler(ICharacterRepository characters) : IAsyncPacketHandler<AutoHuntToggleRequest>
{
    /// <summary>
    ///     Zones where enabling auto-hunt is refused (verified S04_MyWork02.cpp:13508-13520). CORRECTED: the
    ///     source's <c>mCheckZone241TypeServer</c> flag is not literally "zone 241" -- it's a per-server
    ///     boolean true for 20 distinct server numbers (verified <c>S07_MyGame01.cpp:1232-1256</c>: 241-249,
    ///     292-294, 311-312, 325-330), all of which must be refused, not just 241.
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
            // CORRECTED: source's `!(!A && !B)` (S04_MyWork02.cpp:13548) is `A || B`, not AND -- a character
            // with only ONE attack-type slot configured (a legitimate setup) was wrongly refused otherwise.
            var hasAttackSkill = packet.AutoHunt.AttackType.Length >= 3 &&
                                 (packet.AutoHunt.AttackType[0] != 0 || packet.AutoHunt.AttackType[2] != 0);

            if (DisallowedZones.Contains(zone.MapId) || !hasWeapon || !hasAttackSkill)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }
        }

        var enabled = packet.Sort == 1;
        var configBytes = new byte[Fenrir.Contracts.Packets.Shared.AutoHunt.WireSize];
        packet.AutoHunt.Write(configBytes);

        await characters.SetAutoHuntAsync(characterId, enabled, configBytes, cancellationToken);

        state.AutoHuntEnabled = enabled;
        state.AutoHuntConfig = packet.AutoHunt;

        session.Send(new AutoHuntToggleResponse
        {
            ServerIndex = characterId, UniqueNumber = state.UniqueNumber, AutoState = packet.Sort
        });
    }

    /// <summary><c>FEQUIP_TYPE::EWEAPON</c> (STRUCT.h:1662-1676).</summary>
    private const byte WeaponSlot = 7;
}
