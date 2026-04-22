using Jobuler.Application.Exports;
using Jobuler.Application.Exports.Commands;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Jobuler.Infrastructure.Exports;

/// <summary>
/// Renders a schedule PDF using QuestPDF.
/// QuestPDF community license is free for open-source and small commercial use.
/// </summary>
public class QuestPdfRenderer : IPdfRenderer
{
    static QuestPdfRenderer()
    {
        // Required: declare license type before generating any document
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(SchedulePdfModel model)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text($"{model.SpaceName} — Schedule v{model.VersionNumber}")
                        .FontSize(14).Bold();
                    col.Item().Text($"Generated: {model.GeneratedAt:yyyy-MM-dd HH:mm} UTC")
                        .FontSize(8).FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2.5f); // Person
                        cols.RelativeColumn(2f);   // Task
                        cols.RelativeColumn(1.2f); // Burden
                        cols.RelativeColumn(1.8f); // Starts
                        cols.RelativeColumn(1.8f); // Ends
                        cols.RelativeColumn(1.5f); // Location
                    });

                    // Header row
                    static IContainer HeaderCell(IContainer c) =>
                        c.Background(Colors.Grey.Darken2).Padding(4);

                    table.Header(header =>
                    {
                        foreach (var h in new[] { "Person", "Task", "Burden", "Starts", "Ends", "Location" })
                        {
                            header.Cell().Element(HeaderCell)
                                .Text(h).FontColor(Colors.White).Bold().FontSize(8);
                        }
                    });

                    // Data rows
                    var even = false;
                    foreach (var row in model.Rows)
                    {
                        even = !even;
                        var bg = even ? Colors.White : Colors.Grey.Lighten4;

                        static IContainer DataCell(IContainer c, string color) =>
                            c.Background(color).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4);

                        table.Cell().Element(c => DataCell(c, bg)).Text(row.PersonName);
                        table.Cell().Element(c => DataCell(c, bg)).Text(row.TaskName);
                        table.Cell().Element(c => DataCell(c, bg)).Text(row.BurdenLevel);
                        table.Cell().Element(c => DataCell(c, bg)).Text(row.StartsAt.ToString("MM-dd HH:mm"));
                        table.Cell().Element(c => DataCell(c, bg)).Text(row.EndsAt.ToString("MM-dd HH:mm"));
                        table.Cell().Element(c => DataCell(c, bg)).Text(row.Location ?? "");
                    }

                    if (model.Rows.Count == 0)
                    {
                        table.Cell().ColumnSpan(6).Padding(12)
                            .Text("No assignments in this version.")
                            .FontColor(Colors.Grey.Medium).Italic();
                    }
                });

                page.Footer().AlignRight()
                    .Text(x =>
                    {
                        x.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        x.Span(" of ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
            });
        }).GeneratePdf();
    }
}
