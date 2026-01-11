# Deployment Guide - Ubuntu 24.04 LTS VPS

Complete step-by-step guide to deploy the MangaWhisper Background Worker on a fresh Ubuntu VPS with local PostgreSQL.

## Prerequisites

- Ubuntu 24.04 LTS VPS with root/sudo access
- SSH access to your VPS
- PostgreSQL credentials ready

## Step 1: Connect to VPS

```bash
# From your local machine (Windows PowerShell or terminal)
ssh root@YOUR_VPS_IP
# Or if you have a user: ssh youruser@YOUR_VPS_IP
```

## Step 2: Update System

```bash
sudo apt update && sudo apt upgrade -y
```

## Step 3: Install Required Packages

```bash
# Install basic packages
sudo apt install -y postgresql postgresql-contrib git curl chromium-browser

# Install .NET SDK 8.0 (LTS - recommended for production)
sudo apt install -y dotnet-sdk-8.0

# Verify installation
dotnet --version
```

**Alternative: If you specifically need .NET 10.0:**

```bash
sudo apt install -y dotnet-sdk-10.0
dotnet --version
```

**Note:** Ubuntu 24.04 doesn't have .NET SDK 9.0 in its repositories. Use .NET 8.0 (LTS) for production stability.

## Step 4: Setup PostgreSQL

```bash
# Switch to postgres user
sudo -u postgres psql

# Inside PostgreSQL shell, run these commands:
```

```sql
-- Create database
CREATE DATABASE manga_whisper;

-- Create dedicated user (change password!)
CREATE USER whisper_user WITH PASSWORD 'your_secure_password_here';

-- Grant privileges
GRANT ALL PRIVILEGES ON DATABASE manga_whisper TO whisper_user;

-- Exit PostgreSQL
\q
```

```bash
echo "✅ PostgreSQL database and user created!"
echo "⚠️  Note: Tables will be created after deploying the code (Step 6)"
```

## Step 5: Create Application User

```bash
# Create a dedicated user for the bot (security best practice)
sudo useradd -r -m -s /bin/bash -d /opt/projects/manga-whisper-background-worker whisper

# Create directory structure
sudo mkdir -p /opt/projects/manga-whisper-background-worker/{logs,data}
sudo chown -R whisper:whisper /opt/projects/manga-whisper-background-worker
```

## Step 6: Deploy Application Code

### Option A: Using git (recommended if your repo is on GitHub/GitLab)

```bash
# Clone your repository
sudo -u whisper git clone https://username:personal_access_token@github.com/yourusername/manga-whisper-background-worker.git /opt/projects/manga-whisper-background-worker/repo

# Verify the clone
sudo -u whisper ls -la /opt/projects/manga-whisper-background-worker/repo
```

### Option B: Upload from your local machine (if not using git)

```bash
# First, create a zip of your project

# Then upload to VPS:
scp manga-whisper-background-worker.zip root@YOUR_VPS_IP:/tmp/

# Back on the VPS:
sudo apt install -y unzip
cd /opt/projects/manga-whisper-background-worker
sudo -u whisper unzip /tmp/manga-whisper-background-worker.zip -d repo
sudo chown -R whisper:whisper /opt/projects/manga-whisper-background-worker
```

**Now create the database tables:**

```bash
# Switch to whisper user and navigate to project
sudo -u whisper -i
cd /opt/projects/manga-whisper-background-worker/repo

# Build the project
dotnet build

# Exit whisper user
exit

# Generate SQL migration script from the main MangaWhisper project
# Note: This project doesn't have its own migrations
# We need to generate the SQL script from the main manga-whisper project
# On your local machine (Windows), run:
cd manga-whisper\back-end\MangaWhisper.Api
dotnet ef migrations script --project ..\MangaWhisper.Infrastructure\MangaWhisper.Infrastructure.csproj --startup-project . --idempotent --output migration_prod.sql

# Transfer the generated SQL file to your VPS
scp migration_prod.sql root@YOUR_VPS_IP:/tmp/

# Back on the VPS, run the SQL script
sudo -u postgres psql -d manga_whisper -f /tmp/migration_prod.sql

# Grant permissions to whisper_user on all tables and sequences
sudo -u postgres psql -d manga_whisper -c "GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO whisper_user;"
sudo -u postgres psql -d manga_whisper -c "GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO whisper_user;"

# Set default privileges for future tables
sudo -u postgres psql -d manga_whisper -c "ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL PRIVILEGES ON TABLES TO whisper_user;"
sudo -u postgres psql -d manga_whisper -c "ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL PRIVILEGES ON SEQUENCES TO whisper_user;"

echo "✅ Database tables created and permissions granted successfully!"
```

## Step 7: Build and Restore Dependencies

```bash
# Switch to whisper user
sudo -u whisper -i
cd /opt/projects/manga-whisper-background-worker/repo

# Install dependencies
dotnet restore

# Verify installation
dotnet build

# Check if build was successful
echo "✅ Project built successfully!"

# Exit whisper user
exit
```

## Step 8: Configure Environment

```bash
# Copy .env.example to .env (if it exists)
sudo -u whisper cp /opt/projects/manga-whisper-background-worker/repo/src/MangaWhisper.BackgroundWorker.Api/.env.example /opt/projects/manga-whisper-background-worker/repo/src/MangaWhisper.BackgroundWorker.Api/.env 2>/dev/null || sudo -u whisper touch /opt/projects/manga-whisper-background-worker/repo/src/MangaWhisper.BackgroundWorker.Api/.env

# Edit the .env file
sudo -u whisper nano /opt/projects/manga-whisper-background-worker/repo/src/MangaWhisper.BackgroundWorker.Api/.env
```

**Update the values with your actual credentials:**

```env
DefaultConnection=Host=YOUR_HOST;Database=mangawhisper;Username=YOUR_USER;Password=YOUR_PASSWORD;Port=5432
CHECK_DAYS_OF_WEEK=4,5
CHECK_START_HOUR=12
CHECK_INTERVAL_HOURS=4
```

**Save and exit:** `Ctrl+X`, then `Y`, then `Enter`

```bash
# Secure the file
sudo chmod 600 /opt/projects/manga-whisper-background-worker/repo/src/MangaWhisper.BackgroundWorker.Api/.env
```

## Step 09: Test the Application Manually

```bash
# Switch to whisper user
sudo -u whisper -i
cd /opt/projects/manga-whisper-background-worker/repo

# Build and run the application
dotnet build
dotnet run --project src/MangaWhisper.BackgroundWorker.Api

# In another terminal, check logs
tail -f /opt/projects/manga-whisper-background-worker/repo/logs/background-worker-*.log

# If everything works, exit
# Ctrl+C to stop
exit
```

## Step 10: Install systemd Service

```bash
# Copy service file
sudo cp /opt/projects/manga-whisper-background-worker/repo/mangawhisper-worker.service /etc/systemd/system/

# Reload systemd
sudo systemctl daemon-reload

# Enable service (start on boot)
sudo systemctl enable mangawhisper-worker

# Start service
sudo systemctl start mangawhisper-worker

# Check status
sudo systemctl status mangawhisper-worker
```

**You should see:** `Active: active (running)`

**If you see some error stop the service:**

```bash
# Stop the failing service
sudo systemctl stop mangawhisper-worker
```

**After fixing the mangawhisper-worker.service follow:**

```bash
# Copy the updated service file
sudo cp /opt/projects/manga-whisper-background-worker/repo/mangawhisper-worker.service /etc/systemd/system/

# Reload systemd
sudo systemctl daemon-reload

# Start the service
sudo systemctl start mangawhisper-worker

# Check status
sudo systemctl status mangawhisper-worker
```

## Step 11: Monitor Logs

```bash
# View live logs
sudo journalctl -u mangawhisper-worker -f

# Or check the application logs
sudo tail -f /opt/projects/manga-whisper-background-worker/repo/logs/background-worker-*.log

# Check systemd stdout/stderr
sudo tail -f /opt/projects/manga-whisper-background-worker/logs/systemd-stdout.log
```

## Step 12: Setup Log Rotation (Important!)

```bash
# Create logrotate config
sudo nano /etc/logrotate.d/mangawhisper-worker
```

**Add this content:**

```txt
/opt/projects/manga-whisper-background-worker/logs/*.log /opt/projects/manga-whisper-background-worker/repo/logs/*.log {
    daily
    rotate 30
    compress
    delaycompress
    missingok
    notifempty
    create 0640 whisper whisper
    sharedscripts
    postrotate
        systemctl reload mangawhisper-worker > /dev/null 2>&1 || true
    endscript
}
```

**Save and exit:**

```bash
`Ctrl+X`, then `Y`, then `Enter`
```

## Step 13: Setup Firewall (Optional but Recommended)

```bash
# Install UFW if not present
sudo apt install -y ufw

# Allow SSH (IMPORTANT - don't lock yourself out!)
sudo ufw allow 22/tcp

# Enable firewall
sudo ufw --force enable

# Check status
sudo ufw status
```

## Useful Commands After Setup

### Start/Stop/Restart Bot

```bash
sudo systemctl start mangawhisper-worker    # Start
sudo systemctl stop mangawhisper-worker     # Stop
sudo systemctl restart mangawhisper-worker  # Restart
sudo systemctl status mangawhisper-worker   # Check status
```

### View Logs

```bash
# Live system logs
sudo journalctl -u mangawhisper-worker -f

# Application logs
sudo tail -f /opt/projects/manga-whisper-background-worker/repo/logs/background-worker-*.log

# Last 100 lines
sudo journalctl -u mangawhisper-worker -n 100
```

### Update the Application

```bash
# Stop service
sudo systemctl stop mangawhisper-worker

# Switch to whisper user
sudo -u whisper -i
cd /opt/projects/manga-whisper-background-worker/repo

# Pull updates (if using git)
git pull

# Or upload new files via scp/WinSCP

# Build project
dotnet build

# Exit whisper user
exit

# Start bot
sudo systemctl start mangawhisper-worker

# Check it started ok
sudo systemctl status mangawhisper-worker
```

### Database Management

```bash
# Connect to database
sudo -u postgres psql -d manga_whisper

# View checkers
SELECT * FROM "MangaCheckers" ORDER BY "CreatedAt" DESC LIMIT 10;

# View recent chapters
SELECT * FROM "Chapters" ORDER BY "CreatedAt" DESC LIMIT 10;

# View all mangas
SELECT * FROM "Mangas" ORDER BY "Title";

# Exit
\q
```

### Check if Bot is Running

```bash
# Quick status
sudo systemctl is-active mangawhisper-worker

# Detailed status
sudo systemctl status mangawhisper-worker

# Check process
ps aux | grep "dotnet"
```

## Troubleshooting

### Selenium not working

```bash
# Check if Chrome is installed
chromium-browser --version

# Test headless mode
chromium-browser --headless --disable-gpu --dump-dom https://www.google.com
```

### Database connection issues

```bash
# Check PostgreSQL is running
sudo systemctl status postgresql

# Test connection
psql -U whisper_user -d manga_whisper -h localhost

# Check pg_hba.conf allows local connections
sudo nano /etc/postgresql/16/main/pg_hba.conf
# Ensure this line exists:
# local   all   all   peer
# host    all   all   127.0.0.1/32   scram-sha-256
```

### Can't connect via SSH

```bash
# Make sure firewall allows SSH before enabling it
sudo ufw allow 22/tcp
sudo ufw enable
```

## Security Best Practices

1. **Never commit `.env` or API keys to git**
2. **Use strong PostgreSQL password**
3. **Keep system updated:** `sudo apt update && sudo apt upgrade`
4. **Monitor logs regularly** for unusual activity
5. **Backup database periodically:**

   ```bash
   sudo -u postgres pg_dump manga_whisper > backup_$(date +%Y%m%d).sql
   ```

6. **Consider setting up SSH key authentication** instead of password

## Backup Strategy

```bash
# Create backup script
sudo nano /opt/projects/manga-whisper-background-worker/backup.sh
```

**Add:**

```bash
#!/bin/bash
BACKUP_DIR="/opt/projects/manga-whisper-background-worker/backups"
mkdir -p $BACKUP_DIR
DATE=$(date +%Y%m%d_%H%M%S)

# Backup database
sudo -u postgres pg_dump manga_whisper > $BACKUP_DIR/db_$DATE.sql

# Backup config and logs
tar -czf $BACKUP_DIR/config_$DATE.tar.gz /opt/projects/manga-whisper-background-worker/repo/.env

# Keep only last 7 days
find $BACKUP_DIR -name "*.sql" -mtime +7 -delete
find $BACKUP_DIR -name "*.tar.gz" -mtime +7 -delete

echo "Backup completed: $DATE"
```

```bash
# Make executable
sudo chmod +x /opt/projects/manga-whisper-background-worker/backup.sh

# Test it
sudo /opt/projects/manga-whisper-background-worker/backup.sh

# Setup daily cron job
sudo crontab -e
# Add this line:
# 0 2 * * * /opt/projects/manga-whisper-background-worker/backup.sh >> /opt/projects/manga-whisper-background-worker/logs/backup.log 2>&1
```

## Done! 🎉

Your MangaWhisper Background Worker is now running 24/7 on your VPS. It will:

- ✅ Start automatically on boot
- ✅ Restart automatically if it crashes
- ✅ Check for new manga chapters based on your configured schedule
- ✅ Log everything to files you can monitor

Check status anytime with: `sudo systemctl status mangawhisper-worker`
