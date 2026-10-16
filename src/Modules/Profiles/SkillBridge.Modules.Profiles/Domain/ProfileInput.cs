namespace SkillBridge.Modules.Profiles.Domain;

internal static class ProfileInput
{
    public const decimal MaximumStoredMoney = 9_999_999_999_999_999.99m;

    public static bool IsValidText(string? value, int maximumLength, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return !required;
        }

        return value.Length <= maximumLength && !value.Any(char.IsControl);
    }

    public static bool IsValidOptionalHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return value.Length <= 2048 &&
            !value.Any(char.IsControl) &&
            Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https";
    }

    public static bool IsSupportedCurrency(string? currency) =>
        currency is not null && currency.Trim().ToUpperInvariant() is "VND" or "USD";
}
