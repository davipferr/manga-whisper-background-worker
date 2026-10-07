# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached as long as the project files don't change)
COPY src/MangaWhisper.BackgroundWorker.Api/MangaWhisper.BackgroundWorker.Api.csproj src/MangaWhisper.BackgroundWorker.Api/
COPY src/MangaWhisper.BackgroundWorker.Application/MangaWhisper.BackgroundWorker.Application.csproj src/MangaWhisper.BackgroundWorker.Application/
COPY src/MangaWhisper.BackgroundWorker.Common/MangaWhisper.BackgroundWorker.Common.csproj src/MangaWhisper.BackgroundWorker.Common/
COPY src/MangaWhisper.BackgroundWorker.Domain/MangaWhisper.BackgroundWorker.Domain.csproj src/MangaWhisper.BackgroundWorker.Domain/
COPY src/MangaWhisper.BackgroundWorker.Infrastructure/MangaWhisper.BackgroundWorker.Infrastructure.csproj src/MangaWhisper.BackgroundWorker.Infrastructure/
RUN dotnet restore src/MangaWhisper.BackgroundWorker.Api/MangaWhisper.BackgroundWorker.Api.csproj

COPY . .
RUN dotnet publish src/MangaWhisper.BackgroundWorker.Api/MangaWhisper.BackgroundWorker.Api.csproj -c Release -o /app/publish --no-restore

# ---- runtime ----
# The official .NET 10 images are Ubuntu-based, where `apt install chromium` is a snap stub that
# doesn't work in containers. Debian ships real chromium + chromium-driver packages (always the
# same version), so the runtime is Debian with the ASP.NET runtime copied from the official image.
FROM debian:trixie-slim AS runtime

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        libicu76 \
        tzdata \
        chromium \
        chromium-driver \
        fonts-liberation \
    && rm -rf /var/lib/apt/lists/*

COPY --from=mcr.microsoft.com/dotnet/aspnet:10.0 /usr/share/dotnet /usr/share/dotnet
RUN ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet

ENV DOTNET_RUNNING_IN_CONTAINER=true \
    CHROME_BINARY=/usr/bin/chromium \
    CHROMEDRIVER_PATH=/usr/bin/chromedriver

RUN useradd --create-home --uid 1654 app
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/logs && chown -R app:app /app

USER app
ENTRYPOINT ["dotnet", "MangaWhisper.BackgroundWorker.Api.dll"]
