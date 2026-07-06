namespace Fenrir.Application.Login.Domain.Avatars;

/// <summary>
///     Per-login avatar Life/Mana floor clamp -- one of two independent corrections the legacy
///     <c>LOGIN_SEND</c> tail applies to every occupied avatar slot on every successful login (the other,
///     logout-zone/tribe realignment, is deliberately not implemented yet -- see
///     <c>Fenrir.Application.Login.Services.ZoneTransfer.ZoneTransferService</c>'s own remarks for why).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/function.h:242-245 (<c>SetIntegerLow</c>:
///     <c>
///         value = value &lt; floor ? floor :
///         value
///     </c>
///     ) ; Server/ts25login/S04_MyWork02.cpp:357-358 (the two call sites -- <c>aLogoutInfo[4]</c> floored
///     to 1, <c>aLogoutInfo[5]</c> floored to 0). Fenrir's <c>AvatarInfoFactory.CreateForCharacter</c> (both Login
///     and Game copies) independently corroborates that these two array slots are Life and Mana, since the wire
///     struct itself only labels the whole six-element array, not each index.
/// </remarks>
public static class AvatarVitalsFloor
{
    private const int MinLife = 1;
    private const int MinMana = 0;

    /// <summary>Floors <paramref name="life" /> to at least 1 and <paramref name="mana" /> to at least 0.</summary>
    public static (int Life, int Mana) Clamp(int life, int mana)
    {
        return (Math.Max(life, MinLife), Math.Max(mana, MinMana));
    }
}
