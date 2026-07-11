namespace Fenrir.Application.Login.Services.AccountSecurity;

internal static class AccountSecurityEventCodes
{

        public const short MousePinMismatch = 1;

        public const short MousePinLockout = 2;

        public const short AvatarRenamed = 3;

        public const short MousePinChangeMismatch = 4;

        public const short MousePinChangeLockout = 5;

        public const short MousePinAttemptRejectedLocked = 6;
}
