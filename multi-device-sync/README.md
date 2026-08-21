# Multi-Device Synchronization

Extracted primarily from `omega-6-pc`.

Source: https://github.com/sarkarshivaditya-lab/omega-6-pc

## Included implementation
- ASP.NET Core sync service
- API-key middleware
- Push/pull synchronization endpoints
- Shared SQLite/EF Core sync context
- Sync data models
- Idempotent correlation handling
- Entity-specific conflict and permission rules
- Sync log and outbox tables
- Health endpoint

## Source mapping
- `UrbanDiagnosticCentre.SyncService/Program.cs`
- `UrbanDiagnosticCentre.SyncService/ApiKeyMiddleware.cs`
- `UrbanDiagnosticCentre.SyncService/SyncController.cs`
- `UrbanDiagnosticCentre.SyncService/SyncDbContext.cs`
- `UrbanDiagnosticCentre.SyncService/UrbanDiagnosticCentre.SyncService.csproj`
- `UrbanDiagnosticCentre.SyncService/Models/SyncModels.cs`
- `UrbanDiagnosticCentre/Models/AppSyncLogEntry.cs`
- `UrbanDiagnosticCentre/Models/SyncCursor.cs`
- `UrbanDiagnosticCentre/Models/SyncIdentity.cs`
- `UrbanDiagnosticCentre/Models/SyncOutboxEntry.cs`

The service was designed to operate against the same SQLite database used by the PC application. The original WPF migrations remain owned by the application rather than the sync service.

## Scope
This module demonstrates reusable same-server multi-device synchronization. It is intentionally separated from the WPF UI and other application-specific screens.