using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace RentMaster.Infrastructure.Security;

public sealed class IdentityNumberProtector(IDataProtectionProvider dataProtectionProvider)
{
    private const string Prefix = "RM-ID-1:";
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(
        "RentMaster.IdentityDocumentNumber.v1");

    public string Protect(string documentNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);
        return Prefix + _protector.Protect(documentNumber);
    }

    public bool TryUnprotect(string storedValue, out string documentNumber)
    {
        documentNumber = string.Empty;
        if (string.IsNullOrWhiteSpace(storedValue))
            return false;

        // Older records may contain only the final four characters. Those values cannot be
        // reconstructed and must not be presented as a complete document number.
        if (!storedValue.StartsWith(Prefix, StringComparison.Ordinal))
        {
            if (storedValue.Length <= 4)
                return false;

            documentNumber = storedValue;
            return true;
        }

        try
        {
            documentNumber = _protector.Unprotect(storedValue[Prefix.Length..]);
            return !string.IsNullOrWhiteSpace(documentNumber);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
