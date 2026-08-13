using System.Globalization;
using System.Text;

namespace QuakeReport.ApiService.Text;

/// <summary>
/// Folds text into a comparable search key: strips diacritics and uppercases,
/// so "Cráter" and "CRATER" match. Used both to build the SearchText column
/// on write and to normalize the incoming query on read.
/// </summary>
public static class SearchTextNormalizer
{
    public static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new(value.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark &&
                    (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)))
                .Select(char.ToUpperInvariant)
                .ToArray());
}
