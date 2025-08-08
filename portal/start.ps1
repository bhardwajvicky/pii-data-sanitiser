# PII Obfuscation Portal Startup Script
Write-Host "Starting PII Obfuscation Portal..." -ForegroundColor Green

# Check if Docker is running
if (-not (Get-Process -Name "Docker Desktop" -ErrorAction SilentlyContinue)) {
    Write-Host "Docker Desktop is not running. Please start Docker Desktop first." -ForegroundColor Red
    exit 1
}

# Navigate to the portal directory
Set-Location $PSScriptRoot

# Start the services using Docker Compose
Write-Host "Starting services with Docker Compose..." -ForegroundColor Yellow
docker-compose up -d

# Wait a moment for services to start
Start-Sleep -Seconds 10

# Check if services are running
Write-Host "Checking service status..." -ForegroundColor Yellow
docker-compose ps

Write-Host "`nApplication URLs:" -ForegroundColor Green
Write-Host "  Web Portal: http://localhost:7002" -ForegroundColor Cyan
Write-Host "  API Documentation: http://localhost:7001/swagger" -ForegroundColor Cyan
Write-Host "  Database: localhost:1433 (sa/YourStrong@Passw0rd)" -ForegroundColor Cyan

Write-Host "`nTo stop the application, run: docker-compose down" -ForegroundColor Yellow
