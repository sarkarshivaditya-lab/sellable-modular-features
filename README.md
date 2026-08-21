# Omega 6.0 Sellable Modular Features

This repository contains **full-file source extractions**, not snippets, from the two Omega 6.0 source repositories.

## Source repositories

- [omega-6-pc](https://github.com/sarkarshivaditya-lab/omega-6-pc)
- [omega-6-hackathon](https://github.com/sarkarshivaditya-lab/omega-6-hackathon)

## Baseline

The extraction standard for this repository is:

> "I dont want tiny snippets, I want you to copy paste entire code bases, every file depending on the feature, from the original two repos."

The `extracted/` trees are the authoritative copies. Source files are copied as complete files from the originals, with their original source structure/namespaces retained wherever possible. Supporting dependency files are included when the extracted implementation directly depends on them.

## Features

### 1. Multi-Device Synchronization

Source: `omega-6-pc`

Includes the complete server-side sync service and the Reception-side sync implementation: HTTP push/pull API, API-key middleware, SQLite/EF Core sync context, sync models, outbox, cursor/changelog, background worker, status reporting, database sync-capture logic, and the application model dependencies used by that implementation.

### 2. PDF Generation & Export

Sources: `omega-6-pc`, `omega-6-hackathon`

Includes the complete Android receipt generator and its domain model dependency, plus the complete PC financial PDF generator, accounting/export service, and the model types required by those implementations.

### 3. Logging & Logistics Management

Sources: `omega-6-pc`, `omega-6-hackathon`

Includes the complete PC backup/restore and error logging implementations and the Android database backup/export implementation, including the Room database, entities, DAOs, domain models, and database dependency module used by the backup repository.

## Important distinction

The module-level `extracted/` directories are source snapshots. They are intentionally not rewritten into simplified pseudo-packages. This preserves the actual implementation and makes it possible to trace each capability back to the original working application.

The original repositories remain the primary application codebases. This repository is the feature-oriented source collection.
