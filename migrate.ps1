#!/usr/bin/env pwsh
# ──────────────────────────────────────────────────────────────────
# LexiVocab EF Core Migration Helper
# Runs migration inside Docker SDK container to bypass file locks.
#
# Usage:
#   .\migrate.ps1 <MigrationName>         # Create a new migration
#   .\migrate.ps1 <MigrationName> -Remove # Remove last migration
#
# Examples:
#   .\migrate.ps1 AddAuditLogs
#   .\migrate.ps1 AddUserAvatar
#   .\migrate.ps1 AddUserAvatar -Remove
# ──────────────────────────────────────────────────────────────────

param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$MigrationName,

    [switch]$Remove
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  LexiVocab EF Migration Tool (Docker)" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Check Docker is running
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "[-] Docker is not installed or not in PATH!" -ForegroundColor Red
    exit 1
}

$SDK_IMAGE = "mcr.microsoft.com/dotnet/sdk:10.0-alpine"
$PROJECT = "src/LexiVocab.Infrastructure"
$STARTUP = "src/LexiVocab.API"

if ($Remove) {
    Write-Host "[*] Removing the last migration..." -ForegroundColor Yellow
    $efCommand = "dotnet ef migrations remove --project $PROJECT --startup-project $STARTUP --force"
} else {
    Write-Host "[*] Creating migration: " -NoNewline -ForegroundColor Green
    Write-Host "$MigrationName" -ForegroundColor White
    $efCommand = "dotnet ef migrations add $MigrationName --project $PROJECT --startup-project $STARTUP"
}

Write-Host "[*] Starting Docker SDK container..." -ForegroundColor DarkGray
Write-Host ""

# Run inside Docker SDK container:
# 1. Install dotnet-ef tool
# 2. Restore NuGet packages
# 3. Run EF migration command
docker run --rm `
    -v "${PWD}:/app" `
    -w /app `
    $SDK_IMAGE `
    sh -c "dotnet tool install --global dotnet-ef --verbosity quiet 2>/dev/null; export PATH=`$PATH:/root/.dotnet/tools; dotnet restore --verbosity quiet; $efCommand"

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "══════════════════════════════════════════════════════" -ForegroundColor Green
    if ($Remove) {
        Write-Host "  [+] Last migration was successfully removed!" -ForegroundColor Green
    } else {
        Write-Host "  [+] Migration '$MigrationName' created successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "  [?] Next steps:" -ForegroundColor Yellow
        Write-Host "     docker-compose up -d --build" -ForegroundColor White
        Write-Host "     (Migrations are automatically applied on container startup)" -ForegroundColor DarkGray
    }
    Write-Host "══════════════════════════════════════════════════════" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "  [-] Migration failed! Check the error log above." -ForegroundColor Red
    exit 1
}
