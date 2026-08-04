namespace Fenrir.Domain.Login.Avatars;

public static class AvatarNameValidator
{
    public const int MaxNameLength = 12;

    public static bool HasOnlyWhitelistedCharacters(string name)
    {
        if (name.Length is 0 or > MaxNameLength)
            return false;

        foreach (var character in name)
            if (character is not (>= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
                return false;

        return true;
    }
}
