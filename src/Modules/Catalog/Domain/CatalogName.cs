using System.Globalization;
using System.Text;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Catalog.Domain;

internal static class CatalogName
{
    public static Result<string> CreateSlug(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150 || name.Any(char.IsControl))
            return Error.Validation("Catalog.InvalidName", "Tên phải có từ 1 đến 150 ký tự và không chứa ký tự điều khiển.");

        string normalized;
        try
        {
            normalized = name.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        }
        catch (ArgumentException)
        {
            return Error.Validation("Catalog.InvalidName", "Tên chứa ký tự Unicode không hợp lệ.");
        }

        var slug = new StringBuilder();
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            var value = character == 'đ' ? 'd' : character;
            if (value is >= 'a' and <= 'z' or >= '0' and <= '9')
                slug.Append(value);
            else if (slug.Length > 0 && slug[^1] != '-')
                slug.Append('-');
        }

        var result = slug.ToString().TrimEnd('-');
        return result.Length == 0
            ? Error.Validation("Catalog.InvalidName", "Tên phải có ít nhất một chữ cái Latin hoặc chữ số để tạo slug.")
            : Result.Success(result);
    }

    public static bool IsValidDescription(string? description) =>
        description is null || (description.Length <= 4000 && !description.Contains('\0'));
}
