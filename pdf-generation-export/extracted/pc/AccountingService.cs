using Microsoft.EntityFrameworkCore;
using UrbanDiagnosticCentre.Data;
using UrbanDiagnosticCentre.Models;

namespace UrbanDiagnosticCentre.Services;

// ── Query result types ────────────────────────────────────────────────────────

public record AccountingSummary(
    decimal TodayIncome,
    decimal TodayExpenses,
    decimal MonthIncome,
    decimal MonthExpenses,
    decimal NetProfitLoss,
    int     TotalTransactions);

public record IncomeEntry(
    int      ReportId,
    string   ReportCode,
    string   PatientName,
    DateTime Date,
    decimal  Amount);

public class LedgerRow
{
    public DateTime Date        { get; init; }
    public string   Type        { get; init; } = string.Empty;
    public string   Category    { get; init; } = string.Empty;
    public string   Description { get; init; } = string.Empty;
    public decimal  Amount      { get; init; }
    public string?  Notes       { get; init; }
    public int?     TransactionId { get; init; }
}

// ── Service ───────────────────────────────────────────────────────────────────

public class AccountingService
{
    private readonly AppDbContext _db;

    public static readonly string[] BuiltInCategories =
    [
        "Rent", "Salaries", "Electricity", "Consumables",
        "Equipment", "Maintenance", "Miscellaneous"
    ];

    public AccountingService(AppDbContext db) => _db = db;

    // ── Summary ───────────────────────────────────────────────────────────────

    public AccountingSummary GetSummary()
    {
        var today      = DateTime.Today;
        var tomorrow   = today.AddDays(1);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var monthEnd   = monthStart.AddMonths(1);

        var todayIncome  = GetReportIncome(today, tomorrow);
        var todayExp     = GetExpenseTotal(today, tomorrow);
        var monthIncome  = GetReportIncome(monthStart, monthEnd);
        var monthExp     = GetExpenseTotal(monthStart, monthEnd);

        var totalTx = _db.Reports.Count(r =>
                          !r.IsArchived &&
                          (r.Status == ReportStatus.Completed || r.Status == ReportStatus.Printed) &&
                          ((r.BillingMode == BillingMode.Package && r.PackageTotalPrice.HasValue) ||
                           (r.BillingMode == BillingMode.Normal  && r.Entries.Any(e => e.ChargedPrice.HasValue))))
                      + _db.FinancialTransactions.Count(ft => !ft.IsDeleted);

        return new AccountingSummary(
            TodayIncome:       todayIncome,
            TodayExpenses:     todayExp,
            MonthIncome:       monthIncome,
            MonthExpenses:     monthExp,
            NetProfitLoss:     monthIncome - monthExp,
            TotalTransactions: totalTx);
    }

    // ── Income (derived from completed reports) ───────────────────────────────

    public List<IncomeEntry> GetIncomeEntries(DateTime from, DateTime to)
    {
        var toExclusive = to.Date.AddDays(1);

        // Materialize reports with all data needed to compute totals in .NET.
        // Avoids complex nested-sum SQL; keeps query simple and SQLite-safe.
        var raw = _db.Reports
            .Where(r =>
                !r.IsArchived &&
                (r.Status == ReportStatus.Completed || r.Status == ReportStatus.Printed) &&
                r.TestDate >= from.Date && r.TestDate < toExclusive)
            .Select(r => new
            {
                r.Id,
                r.ReportCode,
                PatientName       = r.Patient.FullName,
                r.TestDate,
                r.BillingMode,
                PackageTotalPrice = r.PackageTotalPrice,
                Entries           = r.Entries.Select(e => new { e.IsFromPackage, e.ChargedPrice })
            })
            .ToList();

        return raw
            .Select(r =>
            {
                decimal total = r.BillingMode == BillingMode.Package
                    ? (r.PackageTotalPrice ?? 0m)
                      + r.Entries.Where(e => !e.IsFromPackage && e.ChargedPrice.HasValue)
                                 .Sum(e => e.ChargedPrice!.Value)
                    : r.Entries.Where(e => e.ChargedPrice.HasValue)
                               .Sum(e => e.ChargedPrice!.Value);
                return (r.Id, r.ReportCode, r.PatientName, r.TestDate, total);
            })
            .Where(x => x.total > 0)
            .OrderByDescending(x => x.TestDate)
            .Select(x => new IncomeEntry(x.Id, x.ReportCode, x.PatientName, x.TestDate, x.total))
            .ToList();
    }

    // ── Expenses (manual entries) ─────────────────────────────────────────────

    public List<FinancialTransaction> GetExpenses(DateTime from, DateTime to)
    {
        var toExclusive = to.Date.AddDays(1);
        return _db.FinancialTransactions
            .Where(ft => ft.Type == TransactionType.Expense &&
                         !ft.IsDeleted &&
                         ft.TransactionDate >= from.Date &&
                         ft.TransactionDate < toExclusive)
            .OrderByDescending(ft => ft.TransactionDate)
            .ThenByDescending(ft => ft.CreatedAt)
            .ToList();
    }

    public void AddExpense(FinancialTransaction expense)
    {
        expense.Type      = TransactionType.Expense;
        expense.CreatedAt = DateTime.Now;
        _db.FinancialTransactions.Add(expense);
        _db.SaveChanges();
    }

    /// <summary>
    /// Soft-deletes the expense so synced machines learn of the deletion.
    /// Hard-delete is disallowed for synced accounting records.
    /// </summary>
    public void DeleteExpense(int id)
    {
        var tx = _db.FinancialTransactions.Find(id);
        if (tx is null) return;
        tx.IsDeleted = true;
        tx.DeletedAt = DateTime.UtcNow;
        _db.SaveChanges();
    }

    // ── Unified ledger ────────────────────────────────────────────────────────

    public List<LedgerRow> GetLedger(
        DateTime  from,
        DateTime  to,
        string?   typeFilter     = null,
        string?   categoryFilter = null)
    {
        var rows = new List<LedgerRow>();

        if (typeFilter is null or "Income")
        {
            var incomeEntries = GetIncomeEntries(from, to);
            foreach (var e in incomeEntries)
            {
                if (categoryFilter is not null && categoryFilter != "Diagnostic Revenue") continue;
                rows.Add(new LedgerRow
                {
                    Date        = e.Date,
                    Type        = "Income",
                    Category    = "Diagnostic Revenue",
                    Description = $"{e.PatientName} — {e.ReportCode}",
                    Amount      = e.Amount,
                    Notes       = null,
                    TransactionId = null
                });
            }
        }

        if (typeFilter is null or "Expense")
        {
            var expenses = GetExpenses(from, to);
            foreach (var ft in expenses)
            {
                if (categoryFilter is not null && ft.Category != categoryFilter) continue;
                rows.Add(new LedgerRow
                {
                    Date          = ft.TransactionDate,
                    Type          = "Expense",
                    Category      = ft.Category,
                    Description   = ft.Description,
                    Amount        = ft.Amount,
                    Notes         = ft.Notes,
                    TransactionId = ft.Id
                });
            }
        }

        return rows.OrderByDescending(r => r.Date).ToList();
    }

    // ── For PDF export ────────────────────────────────────────────────────────

    public (List<IncomeEntry> Income, List<FinancialTransaction> Expenses) GetForExport(
        DateTime from, DateTime to)
        => (GetIncomeEntries(from, to), GetExpenses(from, to));

    // ── Helpers ───────────────────────────────────────────────────────────────

    private decimal GetReportIncome(DateTime from, DateTime toExclusive)
    {
        var baseQ = _db.Reports.Where(r =>
            !r.IsArchived &&
            (r.Status == ReportStatus.Completed || r.Status == ReportStatus.Printed) &&
            r.TestDate >= from && r.TestDate < toExclusive);

        // Package fixed price
        var packageIncome = baseQ
            .Where(r => r.BillingMode == BillingMode.Package && r.PackageTotalPrice.HasValue)
            .Sum(r => (decimal?)r.PackageTotalPrice) ?? 0m;

        // Manually-added tests billed alongside a package (IsFromPackage == false)
        var packageManualExtras = baseQ
            .Where(r => r.BillingMode == BillingMode.Package)
            .SelectMany(r => r.Entries)
            .Where(e => !e.IsFromPackage && e.ChargedPrice.HasValue)
            .Sum(e => (decimal?)e.ChargedPrice) ?? 0m;

        // Normal (non-package) reports: all entry prices
        var normalIncome = baseQ
            .Where(r => r.BillingMode == BillingMode.Normal)
            .SelectMany(r => r.Entries)
            .Where(e => e.ChargedPrice.HasValue)
            .Sum(e => (decimal?)e.ChargedPrice) ?? 0m;

        return packageIncome + packageManualExtras + normalIncome;
    }

    private decimal GetExpenseTotal(DateTime from, DateTime toExclusive)
        => _db.FinancialTransactions
            .Where(ft => ft.Type == TransactionType.Expense &&
                         !ft.IsDeleted &&
                         ft.TransactionDate >= from && ft.TransactionDate < toExclusive)
            .Sum(ft => (decimal?)ft.Amount) ?? 0m;
}
