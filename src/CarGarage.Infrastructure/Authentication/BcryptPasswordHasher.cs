using System.Text;
using CarGarage.Application.Abstractions;
using CarGarage.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CarGarage.Infrastructure.Authentication;

public sealed class BcryptPasswordHasher(IOptions<PasswordOptions> options) : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, options.Value.WorkFactor);

    public bool Verify(string password, string hash)
    {
        if (Encoding.UTF8.GetByteCount(password) > 72) return false;
        try { return BCrypt.Net.BCrypt.Verify(password, hash); }
        catch (BCrypt.Net.SaltParseException) { return false; }
    }
}
