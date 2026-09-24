using System.Text;

namespace SkillBridge.BuildingBlocks.Security;

public class PasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash)
            || Encoding.UTF8.GetByteCount(password) > 72)
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (Exception exception) when (exception is BCrypt.Net.SaltParseException or ArgumentException or FormatException)
        {
            return false;
        }
    }
}
