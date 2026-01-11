# MangaWhisper Background Worker

A standalone background service for checking manga chapter updates using Selenium web scraping. This project was extracted from the main MangaWhisper application to run independently on a VPS due to Azure DevOps Selenium limitations.

## Overview

This background worker connects to the same database as the main MangaWhisper API and automatically checks for new manga chapters at configured intervals (default: 4 hours).

## Architecture

The project follows Clean Architecture with layered design:

- **Api Layer**: Entry point, dependency injection configuration
- **Application Layer**: Business logic and services
- **Domain Layer**: Entities, interfaces, domain services
- **Infrastructure Layer**: Data access, repositories, Selenium implementation
- **Common Layer**: Shared enums and constants

## Prerequisites

- .NET 10.0 SDK
- PostgreSQL database (shared with main API)
- Chrome/Chromium browser (for Selenium)
- ChromeDriver (included via NuGet package)

## Setup

1. **Clone the repository**

   ```bash
   git clone <your-repository-url>
   cd MangaWhisper.BackgroundWorker
   ```

2. **Configure environment variables**

   ```bash
   cd src/MangaWhisper.BackgroundWorker.Api
   cp .env.example .env
   ```

3. **Update `.env` with your database connection**

   ```env
   DefaultConnection=Host=your-host;Database=mangawhisper;Username=your-user;Password=your-password;Port=5432
   ```

4. **Restore dependencies**

   ```bash
   cd manga-whisper-background-worker

   dotnet restore
   ```

5. **Build the project**

   ```bash
   dotnet build
   ```

6. **Run the worker**

   ```bash
   cd src/MangaWhisper.BackgroundWorker.Api
   dotnet run
   ```

### Running on Windows

Run as a Windows Service using the included `Microsoft.Extensions.Hosting.WindowsServices` package.

## Configuration

### Check Interval

Set via environment variable in `.env`:

```env
CHECK_INTERVAL_HOURS=6
```

## Logs

Logs are written to:

- Console output
- `logs/background-worker-YYYYMMDD.log` (daily rolling)

## Database

This worker connects to the same PostgreSQL database as the main MangaWhisper API. Ensure:

- Database migrations are applied from the main project
- Connection string has read/write access to tables

## Troubleshooting

### Selenium Issues

- Ensure Chrome/Chromium is installed
- Check ChromeDriver version matches Chrome version
- Verify headless mode works: `chromium-browser --headless --disable-gpu`

### Database Connection

- Test connection string manually
- Verify PostgreSQL allows remote connections
- Check firewall rules

## License

Private - All rights reserved
