using Microsoft.ML.Data;

namespace SpamClassifierAPI.Models
{
    public class EmailData
    {
        [LoadColumn(0)]
        public bool Label { get; set; }
        [LoadColumn(1)]
        public string? Message { get; set; } = string.Empty;
    }
}
