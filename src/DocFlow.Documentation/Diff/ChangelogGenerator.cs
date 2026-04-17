using DocFlow.Documentation.Markdown;

namespace DocFlow.Documentation.Diff;

/// <summary>
/// Renders a <see cref="SpecDiff"/> as a readable Markdown changelog. Changes are grouped by
/// severity (Breaking first) and then by category, with a summary table at the top.
/// </summary>
public sealed class ChangelogGenerator
{
    public string Render(SpecDiff diff)
    {
        var writer = new MarkdownWriter();
        writer.Heading(1, "API Changelog");
        writer.Line();

        writer.Heading(2, "Summary");
        writer.Line();
        writer.Line("| Severity | Count |");
        writer.Line("| --- | --- |");
        writer.Line($"| Breaking | {diff.BreakingCount} |");
        writer.Line($"| Non-breaking | {diff.NonBreakingCount} |");
        writer.Line();

        if (!diff.HasChanges)
        {
            writer.Line("_No differences detected._");
            writer.Line();
            return writer.ToString();
        }

        RenderBySeverity(writer, diff, ChangeSeverity.Breaking, "Breaking Changes");
        RenderBySeverity(writer, diff, ChangeSeverity.NonBreaking, "Non-breaking Changes");

        return writer.ToString();
    }

    private static void RenderBySeverity(MarkdownWriter writer, SpecDiff diff, ChangeSeverity severity, string heading)
    {
        var changesBySeverity = diff.Changes.Where(c => c.Severity == severity).ToList();
        if (changesBySeverity.Count == 0) return;

        writer.Heading(2, heading);
        writer.Line();

        foreach (var category in changesBySeverity
                     .GroupBy(c => c.Category)
                     .OrderBy(g => g.Key))
        {
            writer.Heading(3, CategoryLabel(category.Key));
            writer.Line();

            foreach (var change in category
                         .OrderBy(c => c.Path, StringComparer.Ordinal)
                         .ThenBy(c => c.Description, StringComparer.Ordinal))
            {
                writer.Line($"- {change.Description}");
            }
            writer.Line();
        }
    }

    private static string CategoryLabel(ChangeCategory category) => category switch
    {
        ChangeCategory.Operation => "Operations",
        ChangeCategory.Parameter => "Parameters",
        ChangeCategory.RequestBody => "Request Bodies",
        ChangeCategory.Response => "Responses",
        ChangeCategory.Schema => "Schemas",
        ChangeCategory.Security => "Security",
        _ => category.ToString()
    };
}
