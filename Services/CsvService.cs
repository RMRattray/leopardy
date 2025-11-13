using Leopardy.Models;
using System.Text;

namespace Leopardy.Services;

public class CsvService
{
    public string ExportToCsv(List<Category> categories)
    {
        if (categories == null || categories.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();

        // Header row: Category names
        var categoryNames = categories.Select(c => c.Name).ToList();
        sb.AppendLine(string.Join(", ", categoryNames));

        // Get maximum number of clues
        var maxClues = categories.Max(c => c.Clues?.Count ?? 0);

        // Alternate between clue rows and answer rows
        for (int i = 0; i < maxClues; i++)
        {
            // Clue row
            var clueRow = new List<string>();
            foreach (var category in categories)
            {
                if (category.Clues != null && i < category.Clues.Count)
                {
                    clueRow.Add(category.Clues[i].Question);
                }
                else
                {
                    clueRow.Add("");
                }
            }
            sb.AppendLine(string.Join(", ", clueRow));

            // Answer row
            var answerRow = new List<string>();
            foreach (var category in categories)
            {
                if (category.Clues != null && i < category.Clues.Count)
                {
                    answerRow.Add(category.Clues[i].Answer);
                }
                else
                {
                    answerRow.Add("");
                }
            }
            sb.AppendLine(string.Join(", ", answerRow));
        }

        return sb.ToString();
    }

    public List<Category> ImportFromCsv(string csvContent)
    {
        var categories = new List<Category>();

        if (string.IsNullOrWhiteSpace(csvContent))
            return categories;

        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count == 0)
            return categories;

        // Parse header row (category names)
        var headerLine = lines[0];
        var categoryNames = ParseCsvLine(headerLine);
        
        // Initialize categories
        foreach (var name in categoryNames)
        {
            categories.Add(new Category
            {
                Name = name.Trim(),
                Clues = new List<Clue>()
            });
        }

        // Parse clue/answer pairs
        for (int i = 1; i < lines.Count; i += 2)
        {
            if (i >= lines.Count) break;

            var clueLine = lines[i];
            var clueValues = ParseCsvLine(clueLine);

            // Check if there's an answer line
            List<string> answerValues = new();
            if (i + 1 < lines.Count)
            {
                var answerLine = lines[i + 1];
                answerValues = ParseCsvLine(answerLine);
            }

            // Add clues to each category
            for (int j = 0; j < categories.Count && j < clueValues.Count; j++)
            {
                var clueText = clueValues[j].Trim();
                if (string.IsNullOrWhiteSpace(clueText))
                    continue;

                var answerText = j < answerValues.Count ? answerValues[j].Trim() : "";
                var value = (i / 2 + 1) * 200; // 200, 400, 600, 800, 1000...

                categories[j].Clues.Add(new Clue
                {
                    Question = clueText,
                    Answer = answerText,
                    Value = value
                });
            }
        }

        return categories;
    }

    private List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    current.Append('"');
                    i++;
                }
                else
                {
                    // Toggle quote state
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        values.Add(current.ToString());
        return values;
    }
}

