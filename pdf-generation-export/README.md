# PDF Generation & Export

Derived from both Omega 6.0 application codebases.

Sources:
- https://github.com/sarkarshivaditya-lab/omega-6-pc
- https://github.com/sarkarshivaditya-lab/omega-6-hackathon

## Android implementation

The Android implementation contains native PDF receipt generation using `android.graphics.pdf.PdfDocument`, plus reusable data export functions for database files, JSON backups, and CSV records.

Source files:
- `app/src/main/java/com/udc/collection/util/PdfReceiptGenerator.kt`
- `app/src/main/java/com/udc/collection/data/repository/BackupRepository.kt`

The implementation uses Android's Storage Access Framework through `Uri` for user-selected export destinations.

## PC implementation

The PC application contains QuestPDF-based PDF reporting. The current extracted example is the financial PDF generator.

Source file:
- `UrbanDiagnosticCentre/Services/FinancialPdfService.cs`

The PC implementation uses QuestPDF and targets .NET 9/WPF. Its original application project references QuestPDF 2024.10.4.

## Scope

This module demonstrates reusable PDF generation and portable data export across Android and Windows environments. UI screens and domain workflows remain outside this module so the core generation/export logic can be adapted to other applications.