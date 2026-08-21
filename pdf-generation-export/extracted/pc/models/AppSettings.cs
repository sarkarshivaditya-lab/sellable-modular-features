namespace UrbanDiagnosticCentre.Models;

public class AppSettings
{
    public int    Id    { get; set; } = 1; // singleton row

    // ── Organisation ──────────────────────────────────────────────────────────
    public string CentreName    { get; set; } = string.Empty;
    public string CentreAddress { get; set; } = string.Empty;
    public string CentrePhone   { get; set; } = string.Empty;
    public string CentreEmail   { get; set; } = string.Empty;
    public string? LogoPath     { get; set; }

    // ── Storage ───────────────────────────────────────────────────────────────
    public string? ReportsRootPath { get; set; }
    public string? BackupsRootPath { get; set; }

    // ── Pricing ───────────────────────────────────────────────────────────────
    public string DefaultPriceTier { get; set; } = string.Empty;

    // ── PDF branding ─────────────────────────────────────────────────────────
    public string GstTaxId            { get; set; } = string.Empty;
    public string SignatureFooterText { get; set; } = string.Empty;
    public string WatermarkText       { get; set; } = string.Empty;

    // ── Signatures ────────────────────────────────────────────────────────────
    public string  LabInchargeName                    { get; set; } = string.Empty;
    public string? LabInchargeSignaturePath           { get; set; }
    public string  ConsultantPathologistName          { get; set; } = string.Empty;
    public string? ConsultantPathologistSignaturePath { get; set; }

    // ── Metadata ──────────────────────────────────────────────────────────────
    public DateTime? UpdatedAt { get; set; }
}
