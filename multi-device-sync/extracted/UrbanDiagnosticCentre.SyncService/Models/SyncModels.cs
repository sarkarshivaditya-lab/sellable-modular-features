namespace UrbanDiagnosticCentre.SyncService.Models;

// Minimal EF Core models for the Sync Service — only fields needed for sync operations.
// Table names are mapped to the existing WPF app schema via SyncDbContext.OnModelCreating.

public class SyncPatient
{
    public int      Id        { get; set; }
    public Guid     SyncId    { get; set; }
    public string   FullName  { get; set; } = string.Empty;
    public int      Age       { get; set; }
    public string   Gender    { get; set; } = string.Empty;
    public string   PhoneNumber     { get; set; } = string.Empty;
    public string   ReferringDoctor { get; set; } = string.Empty;
    public string   Address   { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool     IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public int?     CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SyncReport
{
    public int      Id              { get; set; }
    public Guid     SyncId          { get; set; }
    public string   ReportCode      { get; set; } = string.Empty;
    public int      PatientId       { get; set; }
    public DateTime TestDate        { get; set; }
    public DateTime ReportDate      { get; set; }
    public string?  PdfPath         { get; set; }
    public DateTime CreatedAt       { get; set; }
    public bool     IsArchived      { get; set; }
    public DateTime? ArchivedAt     { get; set; }
    public string   Status          { get; set; } = "Draft";
    public int?     CreatedByUserId { get; set; }
    public int?     ModifiedByUserId { get; set; }
    public DateTime? ModifiedAt     { get; set; }
    public DateTime? ReportGeneratedAt { get; set; }
    public DateTime? PrintedAt      { get; set; }
    public int      PdfVersion      { get; set; }
    public string?  PriceTierName   { get; set; }
    public string   BillingMode     { get; set; } = "Normal";
    public string?  PackageNameSnapshot { get; set; }
    public decimal? PackageTotalPrice   { get; set; }
    public string   OriginMachineCode   { get; set; } = string.Empty;
    public DateTime UpdatedAt       { get; set; }
}

public class SyncReportEntry
{
    public int      Id                { get; set; }
    public Guid     SyncId            { get; set; }
    public int      ReportId          { get; set; }
    public int      TestDefinitionId  { get; set; }
    public string   ResultValue       { get; set; } = string.Empty;
    public string   ResultFlag        { get; set; } = "Unknown";
    public string   ReferenceRangeUsed { get; set; } = string.Empty;
    public string?  PriceTierName     { get; set; }
    public decimal? ChargedPrice      { get; set; }
    public bool     IsFromPackage     { get; set; }
    public DateTime UpdatedAt         { get; set; }
}

public class SyncTestDefinition
{
    public int      Id              { get; set; }
    public Guid     SyncId          { get; set; }
    public string   TestName        { get; set; } = string.Empty;
    public string   Category        { get; set; } = string.Empty;
    public string   SampleType      { get; set; } = string.Empty;
    public string   Unit            { get; set; } = string.Empty;
    public decimal  MaleMinValue    { get; set; }
    public decimal  MaleMaxValue    { get; set; }
    public decimal  FemaleMinValue  { get; set; }
    public decimal  FemaleMaxValue  { get; set; }
    public decimal  ChildMinValue   { get; set; }
    public decimal  ChildMaxValue   { get; set; }
    public int      DecimalPrecision { get; set; }
    public string   Notes           { get; set; } = string.Empty;
    public DateTime CreatedAt       { get; set; }
    public bool     IsArchived      { get; set; }
    public DateTime? ArchivedAt     { get; set; }
    public DateTime UpdatedAt       { get; set; }
}

public class SyncTestPrice
{
    public int      Id               { get; set; }
    public Guid     SyncId           { get; set; }
    public int      TestDefinitionId { get; set; }
    public string   TierName         { get; set; } = string.Empty;
    public decimal  Price            { get; set; }
    public int      SortOrder        { get; set; }
    public bool     IsActive         { get; set; }
    public DateTime UpdatedAt        { get; set; }
}

public class SyncTestPackage
{
    public int      Id          { get; set; }
    public Guid     SyncId      { get; set; }
    public string   Name        { get; set; } = string.Empty;
    public string   Description { get; set; } = string.Empty;
    public decimal  Price       { get; set; }
    public bool     IsActive    { get; set; }
    public DateTime CreatedAt   { get; set; }
    public DateTime UpdatedAt   { get; set; }
}

public class SyncTestPackageItem
{
    public int      Id               { get; set; }
    public Guid     SyncId           { get; set; }
    public int      PackageId        { get; set; }
    public int      TestDefinitionId { get; set; }
    public int      SortOrder        { get; set; }
    public DateTime UpdatedAt        { get; set; }
}

public class SyncFinancialTransaction
{
    public int             Id              { get; set; }
    public Guid            SyncId          { get; set; }
    public DateTime        TransactionDate { get; set; }
    public string          Type            { get; set; } = "Expense";
    public string          Category        { get; set; } = string.Empty;
    public string          Description     { get; set; } = string.Empty;
    public decimal         Amount          { get; set; }
    public string?         Notes           { get; set; }
    public string?         PaymentMethod   { get; set; }
    public string?         InvoiceNumber   { get; set; }
    public decimal?        TaxAmount       { get; set; }
    public int?            CreatedByUserId { get; set; }
    public DateTime        CreatedAt       { get; set; }
    public int?            SourceReportId  { get; set; }
    public bool            IsDeleted       { get; set; }
    public DateTime?       DeletedAt       { get; set; }
    public DateTime        UpdatedAt       { get; set; }
}

public class SyncUser
{
    public int      Id           { get; set; }
    public Guid     SyncId       { get; set; }
    public string   Username     { get; set; } = string.Empty;
    public string   PasswordHash { get; set; } = string.Empty;
    public string   Role         { get; set; } = "Technician";
    public string   FullName     { get; set; } = string.Empty;
    public DateTime CreatedAt    { get; set; }
    public bool     IsActive     { get; set; }
    public DateTime UpdatedAt    { get; set; }
}

public class SyncIdentityRow
{
    public int      Id             { get; set; }
    public Guid     MachineId      { get; set; }
    public string   MachineCode    { get; set; } = "ADM";
    public DateTime CreatedAt      { get; set; }
    public string?  AdminApiBaseUrl { get; set; }
    public string?  ApiKey         { get; set; }
}

public class AppSyncLogRow
{
    public int      Id                { get; set; }
    public Guid     CorrelationId     { get; set; }
    public string   EntityType        { get; set; } = string.Empty;
    public Guid     EntitySyncId      { get; set; }
    public string   Operation          { get; set; } = string.Empty;
    public string   Payload           { get; set; } = string.Empty;
    public string   SourceMachineCode { get; set; } = string.Empty;
    public DateTime AppliedAt         { get; set; }
}

public class SyncOutboxRow
{
    public int      Id            { get; set; }
    public Guid     CorrelationId { get; set; }
    public string   EntityType    { get; set; } = string.Empty;
    public Guid     EntitySyncId  { get; set; }
    public string   Operation     { get; set; } = string.Empty;
    public string   Payload       { get; set; } = string.Empty;
    public DateTime CreatedAt     { get; set; }
    public bool     IsSent        { get; set; }
    public DateTime? SentAt       { get; set; }
    public int      AttemptCount  { get; set; }
    public string?  LastError     { get; set; }
}
