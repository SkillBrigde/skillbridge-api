namespace SkillBridge.BuildingBlocks.Security;

public class PasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (Exception exception) when (exception is BCrypt.Net.SaltParseException or FormatException or ArgumentException)
        {
            // A missing or malformed stored credential must never authenticate or break the login request.
            return false;
        }
    }
}
