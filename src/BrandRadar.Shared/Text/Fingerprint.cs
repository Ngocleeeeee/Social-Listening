using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BrandRadar.Shared.Text;

/// <summary>
/// Content fingerprint for near-duplicate detection: the same story republished across many
/// outlets (Google News, VnExpress, Tuổi Trẻ…) collapses to one fingerprint so it can be clustered.
/// </summary>
public static class Fingerprint
{
    public static string Of(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        var norm = Strip(title).ToLowerInvariant();
        var words = Regex.Split(norm, "[^a-z0-9]+").Where(w => w.Length > 1).Take(12).ToList();
        if (words.Count == 0) return "";
        var key = string.Join(' ', words);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private static string Strip(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
    }
}
