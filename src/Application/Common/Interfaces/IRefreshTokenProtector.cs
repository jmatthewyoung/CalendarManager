namespace CalendarManager.Application.Common.Interfaces;

/// <summary>
/// Encrypts OAuth refresh tokens at rest via ASP.NET Core Data Protection.
/// </summary>
public interface IRefreshTokenProtector
{
    string Protect(string plaintext);

    string Unprotect(string ciphertext);
}
