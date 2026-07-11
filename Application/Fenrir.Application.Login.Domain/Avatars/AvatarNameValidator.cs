namespace Fenrir.Application.Login.Domain.Avatars;

public static class AvatarNameValidator
{
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
