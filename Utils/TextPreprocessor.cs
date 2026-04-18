using System.Text.RegularExpressions;

namespace SpamClassifierAPI.Utils
{
    public static class TextPreprocessor
    {
        public static string Clean(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = Regex.Replace(text, "<.*?>", string.Empty); // remove HTML
            text = Regex.Replace(text, @"[^a-zA-Z0-9\s]", " "); // remove special chars
            text = text.ToLower();

            return text.Trim();
        }
    }
}