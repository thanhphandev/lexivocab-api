#!/usr/bin/env pwsh
# ──────────────────────────────────────────────────────────────────
# LexiVocab EF Core Migration Helper
# Chạy migration bên trong Docker SDK container để bypass file lock.
#
# Usage:
#   .\migrate.ps1 <MigrationName>         # Tạo migration mới
#   .\migrate.ps1 <MigrationName> -Remove # Xóa migration cuối
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

# Kiểm tra Docker đang chạy
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "❌ Docker chưa được cài đặt hoặc chưa có trong PATH!" -ForegroundColor Red
    exit 1
}

$SDK_IMAGE = "mcr.microsoft.com/dotnet/sdk:10.0-alpine"
$PROJECT = "src/LexiVocab.Infrastructure"
$STARTUP = "src/LexiVocab.API"

if ($Remove) {
    Write-Host "🗑️  Đang xóa migration cuối cùng..." -ForegroundColor Yellow
    $efCommand = "dotnet ef migrations remove --project $PROJECT --startup-project $STARTUP --force"
} else {
    Write-Host "📦 Đang tạo migration: " -NoNewline -ForegroundColor Green
    Write-Host "$MigrationName" -ForegroundColor White
    $efCommand = "dotnet ef migrations add $MigrationName --project $PROJECT --startup-project $STARTUP"
}

Write-Host "🐳 Khởi động Docker SDK container..." -ForegroundColor DarkGray
Write-Host ""

# Chạy trong Docker SDK container:
# 1. Cài dotnet-ef tool
# 2. Restore NuGet packages
# 3. Chạy lệnh EF migration
docker run --rm `
    -v "${PWD}:/app" `
    -w /app `
    $SDK_IMAGE `
    sh -c "dotnet tool install --global dotnet-ef --verbosity quiet 2>/dev/null; export PATH=`$PATH:/root/.dotnet/tools; dotnet restore --verbosity quiet; $efCommand"

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "══════════════════════════════════════════════════════" -ForegroundColor Green
    if ($Remove) {
        Write-Host "  ✅ Migration cuối cùng đã được xóa!" -ForegroundColor Green
    } else {
        Write-Host "  ✅ Migration '$MigrationName' đã tạo thành công!" -ForegroundColor Green
        Write-Host ""
        Write-Host "  📋 Bước tiếp theo:" -ForegroundColor Yellow
        Write-Host "     docker-compose up -d --build" -ForegroundColor White
        Write-Host "     (Migration tự động apply khi container khởi động)" -ForegroundColor DarkGray
    }
    Write-Host "══════════════════════════════════════════════════════" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "  ❌ Migration thất bại! Kiểm tra lỗi ở trên." -ForegroundColor Red
    exit 1
}
