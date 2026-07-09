using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Application.Game.Abstractions.Gm;

/// <summary>
///     Business logic for the Elevated-tier (<c>GmCommandTier.Elevated</c>) "grant money" command: legacy
///     PROCESS_DATA_SEND (opcode 19, <c>GenericActionRequest</c>) sub-command 504,
///     Server/ts25zone/S04_MyWork04.cpp:1006-1035 -- there is no dedicated legacy wire opcode for this action.
///     Per the source behavior contract for this command: every line of the compiled legacy handler body past
///     the tier gate is dead/commented-out source (the would-be <c>GM_MONEY_RECV</c> payload read, the
///     [1,100000000] clamp, the <c>ProcessForDropItem</c> credit call, and the <c>GL_601_GM_CREATE_MONEY</c> log
///     call all never execute) -- this type therefore does not, and must not, credit any money to any
///     character; doing so would be adding new functionality relative to the compiled legacy binary, not
///     reproducing existing behavior. The one live effect is the tier gate itself, plus this project's own
///     mandatory GM-action audit-log write. The acknowledgment's result code is left at legacy's own
///     function-entry default (not the success value every other command in this tier uses) as a direct
///     consequence of that dead code never assigning it.
/// </summary>
public interface IGmGrantMoneyService
{
    /// <summary><paramref name="data" /> is the raw, unmodified 130-byte tData blob to echo back verbatim (never read for this sub-command).</summary>
    ValueTask HandleAsync(byte[] data, ZoneClientSession zoneSession, CancellationToken cancellationToken);
}
