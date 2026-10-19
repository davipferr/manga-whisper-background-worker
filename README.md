# MangaWhisper Background Worker

A standalone background service that checks for new manga chapters by scraping the source sites with Selenium (headless Chromium).

This project was split from the main [MangaWhisper](../manga-whisper) application because the API was hosted on a plan that stops idle apps, and this worker has to be always running. It shares the PostgreSQL database with the API; the two applications never call each other. The API decides what to scrape (the admin creates jobs in the control panel) and this worker only executes it.

## Overview

The worker has no schedule of its own and runs nothing just because it started.

- `JobRunnerBackgroundService` polls the database every 30 seconds. It turns the enabled `ScrapingJobs` whose `NextRunAt` has passed into Pending `JobRuns` (moving `NextRunAt` to the next cron occurrence, in BRT) and then executes the Pending runs one at a time:
  - `CheckNext`: checks once whether chapter `LastKnownChapter + 1` exists and saves it.
  - `Backfill`: walks every chapter from `LastKnownChapter + 1` until one is missing.
- On startup nothing is executed: runs left `Running` by a previous process are marked failed, `Pending` ones are cancelled, and jobs whose next run is already in the past are moved to their next future occurrence (missed runs are skipped).
- Scheduled jobs skip paused checkers (`IsActive = false`); "Run now" requests run anyway.
- `WorkerHeartbeatBackgroundService` writes `WorkerHeartbeats.LastSeenAt` every 30 seconds, so the admin panel can show whether the worker is running.

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

There are no schedule settings: jobs are created in the admin panel and stored in the database.

## Database

The schema is owned by the manga-whisper repository (`database/init/*.sql`); this project has no migrations and never creates tables. The entities here must match those tables.

## Running without Docker (optional)

For debugging from the IDE, copy `src/MangaWhisper.BackgroundWorker.Api/.env.example` to `.env`, point `DefaultConnection` to `localhost` (the Docker PostgreSQL publishes port 5432) and run:

```bash
dotnet run --project src/MangaWhisper.BackgroundWorker.Api
```

Without `CHROME_BINARY` / `CHROMEDRIVER_PATH`, Selenium Manager finds your local Chrome and downloads the matching driver.

## Agent skills

[Agent Skills](https://www.skills.sh/) for Claude Code (`.claude/skills/`) and Codex / GitHub Copilot (`.agents/skills/`), installed with the `skills` CLI and listed in `skills-lock.json`:

| Skill | Source | Use it for |
| --- | --- | --- |
| `optimizing-ef-core-queries` | `dotnet/skills` | Reviewing EF Core queries in the repositories and background jobs (official) |
| `analyzing-dotnet-performance` | `dotnet/skills` | Finding .NET performance anti-patterns (official) |
| `csharp-async` | `github/awesome-copilot` | Async/await best practices for the background services |
| `find-skills` | `vercel-labs/skills` | Finding more skills on skills.sh |

To add another one (installs copies, so the skills work right after a clone):

```bash
npx skills add <owner/repo> --skill <name> --agent claude-code codex --copy --yes
```

Update them with `npx skills update --project --yes`.

## License

Private - All rights reserved
