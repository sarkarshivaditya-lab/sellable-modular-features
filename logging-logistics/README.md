# Logging & Logistics Management

Derived from both Omega 6.0 applications.

Sources:
- https://github.com/sarkarshivaditya-lab/omega-6-pc
- https://github.com/sarkarshivaditya-lab/omega-6-hackathon

## Included capabilities

- General application error and information logging on PC
- Synchronization activity logging and operational audit records
- Local database export/import and CSV/JSON data export on Android
- Full PC backup creation and restoration, including database and generated PDF reports
- Backup metadata, retention queries, and safe pre-restore backups

## Source mapping
PC:
- `UrbanDiagnosticCentre/Services/ErrorLoggingService.cs`
- `UrbanDiagnosticCentre/Services/BackupService.cs`
- `UrbanDiagnosticCentre/Models/BackupRecord.cs`
- `UrbanDiagnosticCentre/Models/AppSyncLogEntry.cs`
- `UrbanDiagnosticCentre/Models/SyncOutboxEntry.cs`

Android:
- `app/src/main/java/com/udc/collection/data/repository/BackupRepository.kt`
- `app/src/main/java/com/udc/collection/data/local/AppDatabase.kt`
- `app/src/main/java/com/udc/collection/data/local/dao/PatientDao.kt`
- `app/src/main/java/com/udc/collection/data/local/entity/Entities.kt`

The module focuses on the reusable operational plumbing behind logging, backup, export, restore, and workflow data handling rather than the application-specific UI.