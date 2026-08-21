# Omega 6.0 Sellable Modular Features

Reusable modular capabilities extracted from the Omega 6.0 PC and Android application codebases.

## Source repositories
- [omega-6-pc](https://github.com/sarkarshivaditya-lab/omega-6-pc)
- [omega-6-hackathon](https://github.com/sarkarshivaditya-lab/omega-6-hackathon)

## Features

### 1. Multi-Device Synchronization
Source: `omega-6-pc`

Server-backed synchronization for keeping application data consistent across multiple devices on the same server. Includes the sync API, data model layer, authentication middleware, shared SQLite access, idempotency, conflict rules, push/pull flows, and sync logging.

### 2. PDF Generation & Export
Sources: `omega-6-pc`, `omega-6-hackathon`

Cross-platform report/receipt generation and export. The PC implementation uses QuestPDF for structured PDF documents; the Android implementation uses the native Android PDF API for receipt generation. The Android extraction also includes database, JSON, database-file, and CSV export capabilities that support portable data handling.

### 3. Logging & Logistics Management
Sources: `omega-6-pc`, `omega-6-hackathon`

Reusable operational logging and data-handling components covering error/info logging, synchronization logs, backup/export workflows, local database backup and restore, and operational records required for moving data through the application workflow.

## Repository structure

```text
multi-device-sync/
pdf-generation-export/
logging-logistics/
```

Each module is kept independent from the original application wherever practical. Source attribution is documented inside each module.

## Attribution

These modules are extracted from working Omega 6.0 application code. The original repositories remain the primary application codebases; this repository packages selected capabilities as reusable modular assets.