using CalendarManager.Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace CalendarManager.Infrastructure.Security;

public class DataProtectionRefreshTokenProtector : IRefreshTokenProtector
{
    private const string Purpose = "CalendarManager.CalendarConnection.RefreshToken";

    private readonly IDataProtector _protector;

    public DataProtectionRefreshTokenProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
