# Quick Setup Guide

## 1. First Time Setup

### Step 1: Configure Environment
```bash
cd src/MangaWhisper.BackgroundWorker.Api
cp .env.example .env
```

Edit `.env` and update with your database credentials:
```
DefaultConnection=Host=YOUR_HOST;Database=mangawhisper;Username=YOUR_USER;Password=YOUR_PASSWORD;Port=5432
```

### Step 2: Restore and Build
```bash
# From repository root
dotnet restore
dotnet build
```

### Step 3: Run Locally (Test)
```bash
cd src/MangaWhisper.BackgroundWorker.Api
dotnet run
```

## 2. Deploy to VPS

### Option A: Linux (Ubuntu/Debian) with systemd

1. **Copy files to VPS**
   ```bash
   scp -r . user@your-vps:/opt/mangawhisper-worker/
   ```

2. **Install dependencies**
   ```bash
   ssh user@your-vps
   cd /opt/mangawhisper-worker
   sudo apt-get update
   sudo apt-get install -y chromium-browser dotnet-sdk-9.0
   ```

3. **Configure .env**
   ```bash
   cd src/MangaWhisper.BackgroundWorker.Api
   cp .env.example .env
   nano .env  # Edit with your database credentials
   ```

4. **Create systemd service**
   ```bash
   sudo nano /etc/systemd/system/mangawhisper-worker.service
   ```

   Paste:
   ```ini
   [Unit]
   Description=MangaWhisper Background Worker
   After=network.target

   [Service]
   Type=notify
   User=YOUR_USER
   WorkingDirectory=/opt/mangawhisper-worker/src/MangaWhisper.BackgroundWorker.Api
   ExecStart=/usr/bin/dotnet run
   Restart=always
   RestartSec=10
   Environment="DOTNET_ROOT=/usr/share/dotnet"

   [Install]
   WantedBy=multi-user.target
   ```

5. **Start service**
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable mangawhisper-worker
   sudo systemctl start mangawhisper-worker
   ```

6. **Check status**
   ```bash
   sudo systemctl status mangawhisper-worker
   sudo journalctl -u mangawhisper-worker -f
   ```

### Option B: Linux with Docker (Alternative)

1. **Create Dockerfile** (in repository root):
   ```dockerfile
   FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
   WORKDIR /src
   COPY . .
   RUN dotnet restore
   RUN dotnet publish -c Release -o /app

   FROM mcr.microsoft.com/dotnet/aspnet:9.0
   RUN apt-get update && apt-get install -y chromium chromium-driver
   WORKDIR /app
   COPY --from=build /app .
   ENTRYPOINT ["dotnet", "MangaWhisper.BackgroundWorker.Api.dll"]
   ```

2. **Build and run**:
   ```bash
   docker build -t mangawhisper-worker .
   docker run -d --name mangawhisper-worker \
     -e DefaultConnection="Host=YOUR_HOST;Database=mangawhisper;Username=YOUR_USER;Password=YOUR_PASSWORD;Port=5432" \
     --restart unless-stopped \
     mangawhisper-worker
   ```

### Option C: Windows Server

1. **Copy files to server**
2. **Edit `.env` file**
3. **Install as Windows Service**:
   ```powershell
   sc.exe create MangaWhisperWorker binPath= "C:\path\to\dotnet.exe run --project C:\path\to\Api"
   sc.exe start MangaWhisperWorker
   ```

## 3. Monitoring

### View Logs
```bash
# Linux systemd
sudo journalctl -u mangawhisper-worker -f

# Docker
docker logs -f mangawhisper-worker

# Log files (all platforms)
tail -f src/MangaWhisper.BackgroundWorker.Api/logs/background-worker-*.log
```

### Check if it's working
Monitor your database for new chapters being added to the `Chapters` table.

## 4. Troubleshooting

### Selenium not working
```bash
# Check if Chrome is installed
chromium-browser --version

# Test headless mode
chromium-browser --headless --disable-gpu --dump-dom https://www.google.com
```

### Database connection issues
```bash
# Test connection from VPS
psql -h YOUR_HOST -U YOUR_USER -d mangawhisper
```

### Service won't start
```bash
# Check logs
sudo journalctl -u mangawhisper-worker -n 50 --no-pager

# Check if port is in use
sudo netstat -tulpn | grep dotnet

# Check permissions
ls -la /opt/mangawhisper-worker
```

## 5. Updating the Worker

```bash
# On VPS
cd /opt/mangawhisper-worker
git pull
dotnet build
sudo systemctl restart mangawhisper-worker
```

## 6. Configuration Changes

### Change check interval
Edit `src/MangaWhisper.BackgroundWorker.Infrastructure/Services/ChapterCheckingBackgroundService.cs`:
```csharp
private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6); // Change this
```

Then rebuild and restart:
```bash
dotnet build
sudo systemctl restart mangawhisper-worker
```

## Need Help?

Check the main [README.md](README.md) for more detailed information.
