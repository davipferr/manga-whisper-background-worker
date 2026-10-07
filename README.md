# MangaWhisper Background Worker

A standalone background service that checks for new manga chapters by scraping the source sites with Selenium (headless Chromium).

This project was split from the main [MangaWhisper](../manga-whisper) application because the API was hosted on a plan that stops idle apps, and this worker has to be always running. It shares the PostgreSQL database with the API; the two applications never call each other.

## Overview

- `ChapterCheckingBackgroundService` runs on a schedule (days of week, start hour and interval, all in BRT) and, for every active `MangaChecker`, checks whether chapter `LastKnownChapter + 1` exists and saves it.
- `ChapterBatchScrapingBackgroundService` is an optional one-off backfill (`ENABLE_BATCH_SCRAPING=true`) that walks every chapter until one is missing.

## Architecture

The project follows Clean Architecture with layered design:

- **Api Layer**: Entry point, dependency injection configuration
- **Application Layer**: Business logic and services
- **Domain Layer**: Entities, interfaces, domain services
- **Infrastructure Layer**: Data access, repositories, Selenium implementation
- **Common Layer**: Shared enums and constants

## Running locally (Docker)

The worker is started by the `docker-compose.yml` in the **manga-whisper** repository, together with PostgreSQL, the API and the front-end. Both repositories must be sibling folders:

```text
<parent>/
├── manga-whisper/
└── manga-whisper-background-worker/
```

See the manga-whisper README for the commands. The worker container logs are available with:

```bash
docker compose logs -f worker
```

The image is based on Debian with the `chromium` and `chromium-driver` packages, so the browser and the driver always have matching versions (`CHROME_BINARY` / `CHROMEDRIVER_PATH` are set in the `Dockerfile`).

## Configuration

All settings are environment variables (set in the manga-whisper `.env` file and passed by docker-compose):

| Variable | Description |
| --- | --- |
| `DefaultConnection` | PostgreSQL connection string (same database as the API) |
| `CHECK_DAYS_OF_WEEK` | Comma-separated days to run: 0=Sunday ... 6=Saturday |
| `CHECK_START_HOUR` | Start hour in BRT (0-23) |
| `CHECK_INTERVAL_HOURS` | Hours between checks |
| `ENABLE_BATCH_SCRAPING` | `true` to run the one-off backfill on startup |
| `STOP_APP_AFTER_BATCH_SCRAPING` | `true` to stop the app when the backfill ends |

## Database

The schema is owned by the manga-whisper repository (`database/init/*.sql`); this project has no migrations and never creates tables. The entities here must match those tables.

## Running without Docker (optional)

For debugging from the IDE, copy `src/MangaWhisper.BackgroundWorker.Api/.env.example` to `.env`, point `DefaultConnection` to `localhost` (the Docker PostgreSQL publishes port 5432) and run:

```bash
dotnet run --project src/MangaWhisper.BackgroundWorker.Api
```

Without `CHROME_BINARY` / `CHROMEDRIVER_PATH`, Selenium Manager finds your local Chrome and downloads the matching driver.

## License

Private - All rights reserved
