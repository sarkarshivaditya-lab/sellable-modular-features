using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UrbanDiagnosticCentre.Models;

namespace UrbanDiagnosticCentre.Services;

public static class FinancialPdfService
{
    static FinancialPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static string Generate(
        DateTime                    from,
        DateTime                    to,
        List<IncomeEntry>           incomeEntries,
        List<FinancialTransaction>  expenses,
        AppSettings                 settings)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UrbanDiagnosticCentre", "FinancialReports");
        Directory.CreateDirectory(folder);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var path  = Path.Combine(folder, $"FinancialReport_{stamp}.pdf");

        var totalIncome   = incomeEntries.Sum(e => e.Amount);
        var totalExpenses = expenses.Sum(e => e.Amount);
        var net           = totalIncome - totalExpenses;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Arial"));

                page.Header().Element(c => ComposeHeader(c, from, to, settings));
                page.Content().Element(c => ComposeContent(c, from, to, incomeEntries, expenses, totalIncome, totalExpenses, net));
                page.Footer().Element(c => ComposeFooter(c, settings));
            });
        }).GeneratePdf(path);

        return path;
    }

    private static void ComposeHeader(IContainer c, DateTime from, DateTime to, AppSettings settings)
    {
        var centreName = string.IsNullOrWhiteSpace(settings.CentreName)
            ? "Urban Diagnostic Centre"
            : settings.CentreName;

        c.BorderBottom(1).BorderColor("#CFD8DC").PaddingBottom(12).Column(col =>
        {
            col.Item().AlignCenter().Text(centreName).FontSize(20).Bold().FontColor("#0D47A1");
            col.Item().AlignCenter().Text("FINANCIAL SUMMARY REPORT")
               .FontSize(13).Bold().FontColor("#1A237E").LetterSpacing(1);
            col.Item().PaddingTop(6).AlignCenter()
               .Text($"Reporting Period:  {from:dd MMM yyyy} – {to:dd MMM yyyy}")
               .FontSize(10).FontColor("#546E7A");
        });
    }

    private static void ComposeContent(
        IContainer                 c,
        DateTime                   from,
        DateTime                   to,
        List<IncomeEntry>          incomeEntries,
        List<FinancialTransaction> expenses,
        decimal                    totalIncome,
        decimal                    totalExpenses,
        decimal                    net)
    {
        c.PaddingTop(16).Column(col =>
        {
            // ── Summary box ───────────────────────────────────────────────────
            col.Item()
               .Background("#F5F7FA").Border(1).BorderColor("#CFD8DC").Padding(14)
               .Column(box =>
               {
                   box.Item().Text("FINANCIAL OVERVIEW")
                      .FontSize(8).Bold().FontColor("#757575").LetterSpacing(1);
                   box.Item().PaddingTop(8).Table(t =>
                   {
                       t.ColumnsDefinition(cols => { cols.RelativeColumn(); cols.RelativeColumn(); });
                       SummaryRow(t, "Total Income",   $"₹ {totalIncome:N2}",   "#1B5E20");
                       SummaryRow(t, "Total Expenses", $"₹ {totalExpenses:N2}", "#B71C1C");
                       SummaryRow(t, "Net Profit / Loss",
                           $"₹ {net:N2}",
                           net >= 0 ? "#1B5E20" : "#B71C1C");
                   });
               });

            // ── Income summary ────────────────────────────────────────────────
            col.Item().PaddingTop(20).Text("INCOME SUMMARY — DIAGNOSTIC REVENUE")
               .FontSize(8).Bold().FontColor("#757575").LetterSpacing(1);

            if (incomeEntries.Count == 0)
            {
                col.Item().PaddingTop(6).Text("No income recorded for this period.")
                   .FontSize(10).FontColor("#9E9E9E").Italic();
            }
            else
            {
                col.Item().PaddingTop(6).Table(t =>
                {
                    t.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1.2f);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(1.5f);
                        cols.RelativeColumn(1);
                    });
                    TableHeader(t, ["Report Code", "Patient", "Date", "Amount"]);
                    bool shade = false;
                    foreach (var e in incomeEntries)
                    {
                        var bg = shade ? "#F9FAFB" : "#FFFFFF"; shade = !shade;
                        t.Cell().Background(bg).Padding(6).Text(e.ReportCode).FontSize(9);
                        t.Cell().Background(bg).Padding(6).Text(e.PatientName).FontSize(9);
                        t.Cell().Background(bg).Padding(6).Text(e.Date.ToString("dd MMM yyyy")).FontSize(9);
                        t.Cell().Background(bg).Padding(6).Text($"₹ {e.Amount:N2}").FontSize(9).Bold();
                    }
                    TotalRow(t, 3, $"₹ {totalIncome:N2}");
                });
            }

            // ── Expense summary ───────────────────────────────────────────────
            col.Item().PaddingTop(20).Text("EXPENSE SUMMARY")
               .FontSize(8).Bold().FontColor("#757575").LetterSpacing(1);

            if (expenses.Count == 0)
            {
                col.Item().PaddingTop(6).Text("No expenses recorded for this period.")
                   .FontSize(10).FontColor("#9E9E9E").Italic();
            }
            else
            {
                // Category sub-totals
                var byCategory = expenses
                    .GroupBy(e => e.Category)
                    .OrderBy(g => g.Key)
                    .Select(g => (Category: g.Key, Total: g.Sum(e => e.Amount)))
                    .ToList();

                col.Item().PaddingTop(6).Table(t =>
                {
                    t.ColumnsDefinition(cols => { cols.RelativeColumn(3); cols.RelativeColumn(1); });
                    TableHeader(t, ["Category", "Total"]);
                    bool shade = false;
                    foreach (var (cat, amt) in byCategory)
                    {
                        var bg = shade ? "#F9FAFB" : "#FFFFFF"; shade = !shade;
                        t.Cell().Background(bg).Padding(6).Text(cat).FontSize(9);
                        t.Cell().Background(bg).Padding(6).Text($"₹ {amt:N2}").FontSize(9).Bold();
                    }
                    TotalRow(t, 1, $"₹ {totalExpenses:N2}");
                });
            }

            // ── Chronological transaction table ───────────────────────────────
            col.Item().PaddingTop(20).Text("TRANSACTION LEDGER (CHRONOLOGICAL)")
               .FontSize(8).Bold().FontColor("#757575").LetterSpacing(1);

            var allRows = BuildLedger(incomeEntries, expenses);
            if (allRows.Count == 0)
            {
                col.Item().PaddingTop(6).Text("No transactions in this period.")
                   .FontSize(10).FontColor("#9E9E9E").Italic();
            }
            else
            {
                col.Item().PaddingTop(6).Table(t =>
                {
                    t.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1.2f);
                        cols.RelativeColumn(0.8f);
                        cols.RelativeColumn(1.2f);
                        cols.RelativeColumn(2.5f);
                        cols.RelativeColumn(1);
                    });
                    TableHeader(t, ["Date", "Type", "Category", "Description", "Amount"]);
                    bool shade = false;
                    foreach (var row in allRows)
                    {
                        var bg = shade ? "#F9FAFB" : "#FFFFFF"; shade = !shade;
                        var amtColor = row.Type == "Income" ? "#1B5E20" : "#B71C1C";
                        t.Cell().Background(bg).Padding(5).Text(row.Date.ToString("dd MMM yyyy")).FontSize(8);
                        t.Cell().Background(bg).Padding(5).Text(row.Type).FontSize(8)
                         .FontColor(row.Type == "Income" ? "#1B5E20" : "#B71C1C");
                        t.Cell().Background(bg).Padding(5).Text(row.Category).FontSize(8);
                        t.Cell().Background(bg).Padding(5).Text(row.Description).FontSize(8);
                        t.Cell().Background(bg).Padding(5).Text($"₹ {row.Amount:N2}").FontSize(8)
                         .Bold().FontColor(amtColor);
                    }
                });
            }
        });
    }

    private static void ComposeFooter(IContainer c, AppSettings settings)
    {
        var centreName = string.IsNullOrWhiteSpace(settings.CentreName)
            ? "Urban Diagnostic Centre"
            : settings.CentreName;

        c.AlignCenter().Text(t =>
        {
            t.Span($"{centreName}  |  Financial Report  |  Page ").FontColor("#757575");
            t.CurrentPageNumber().FontColor("#757575");
            t.Span(" of ").FontColor("#757575");
            t.TotalPages().FontColor("#757575");
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<(DateTime Date, string Type, string Category, string Description, decimal Amount)>
        BuildLedger(List<IncomeEntry> income, List<FinancialTransaction> expenses)
    {
        var rows = new List<(DateTime, string, string, string, decimal)>();
        foreach (var e in income)
            rows.Add((e.Date, "Income", "Diagnostic Revenue", $"{e.PatientName} — {e.ReportCode}", e.Amount));
        foreach (var e in expenses)
            rows.Add((e.TransactionDate, "Expense", e.Category, e.Description, e.Amount));
        return rows.OrderBy(r => r.Item1).ToList();
    }

    private static void SummaryRow(TableDescriptor t, string label, string value, string color)
    {
        t.Cell().Padding(6).Text(label).FontSize(10).Bold();
        t.Cell().Padding(6).Text(value).FontSize(10).Bold().FontColor(color);
    }

    private static void TableHeader(TableDescriptor t, string[] headers)
    {
        foreach (var h in headers)
            t.Cell().Background("#1565C0").Padding(7).Text(h).FontSize(9).Bold().FontColor("#FFFFFF");
    }

    private static void TotalRow(TableDescriptor t, int emptySpan, string total)
    {
        t.Cell().ColumnSpan((uint)emptySpan).Background("#E8EAF6").Padding(7)
         .AlignRight().Text("Total:").FontSize(10).Bold().FontColor("#1A237E");
        t.Cell().Background("#E8EAF6").Padding(7)
         .Text(total).FontSize(10).Bold().FontColor("#1A237E");
    }
}
