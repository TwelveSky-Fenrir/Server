namespace Fenrir.Application.Login.Domain.Avatars;

/// <summary>
///     Character-set whitelist shared by avatar creation and avatar rename: an avatar name may contain only
///     ASCII digits, uppercase letters, and lowercase letters. Pure inspection -- no I/O, no mutation, and no
///     length enforcement of its own (the wire layer's fixed 13-byte <c>[FixedString(13)]</c> buffer already
///     bounds length before this ever runs).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/safestring.h:43-81 (<c>CheckNameString</c> -- empty-string rejection at
///     l.47-51; active byte-range whitelist loop for 0-9/A-Z/a-z at l.63-79). The wide-character/CJK
///     acceptance branch at l.52-61 is commented out in every build configuration and is deliberately not
///     reproduced here -- no non-ASCII name of any kind passes the shipped check.
///     <c>CheckNameString</c> is also invoked, in the same legacy source file, against the login-ID/password
///     fields (Server/ts25login/S04_MyWork02.cpp:170-177), but that call site is compiled only under a build
///     variant (<c>#ifndef LNW33EU</c>) that excludes it from the shipped production build -- out of scope
///     here. This type governs only the two call sites that are unconditionally compiled in every build:
///     <c>CreateAvatarHandler</c> (Server/ts25login/S04_MyWork02.cpp:582-662) and <c>RenameAvatarHandler</c>
///     (Server/ts25login/S04_MyWork02.cpp:1287-1349).
/// </remarks>
public static class AvatarNameValidator
{
    /// <summary>
    ///     True only if <paramref name="name" /> is non-empty and every character is one of 0-9, A-Z, or a-z.
    ///     Checked per UTF-16 character rather than per byte: every character this whitelist accepts is a
    ///     single-byte ASCII value under any encoding the wire layer could plausibly use, so a char-wise scan
    ///     of the already-decoded string rejects exactly the same inputs the legacy byte-wise scan does (any
    ///     space, punctuation, accented byte, or byte belonging to a multi-byte sequence never decodes to one
    ///     of these three ranges).
    /// </summary>
    /// <remarks>
    ///     The empty-string rejection here is dead-in-practice at both call sites this type governs: both
    ///     <c>CreateAvatarHandler</c> and <c>RenameAvatarHandler</c> already reject an empty candidate name
    ///     earlier in their own precondition sequence, before this method is ever reached. Kept anyway so this
    ///     method matches <c>CheckNameString</c>'s own documented contract byte-for-byte, in case a future
    ///     caller reaches it without that precondition already having run.
    /// </remarks>
    public static bool HasOnlyWhitelistedCharacters(string name)
    {
        if (name.Length == 0)
            return false;

        foreach (var character in name)
            if (character is not (>= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
                return false;

        return true;
    }
}
