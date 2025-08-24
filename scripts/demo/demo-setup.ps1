# MAK3R-Cluster Demo Setup Script
# This script sets up a complete demo environment with sample data

param(
    [switch]$SkipBuild,
    [switch]$GenerateData,
    [string]$ApiPort = "5137",
    [string]$PwaPort = "7228"
)

Write-Host "🏭 Setting up MAK3R-Cluster Demo Environment" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

# Check prerequisites
Write-Host "📋 Checking prerequisites..." -ForegroundColor Yellow

# Check .NET 8 SDK
try {
    $dotnetVersion = dotnet --version
    Write-Host "✅ .NET SDK: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ .NET 8 SDK not found. Please install from https://dotnet.microsoft.com/download" -ForegroundColor Red
    exit 1
}

# Check if ports are available
$apiPortInUse = Get-NetTCPConnection -LocalPort $ApiPort -ErrorAction SilentlyContinue
$pwaPortInUse = Get-NetTCPConnection -LocalPort $PwaPort -ErrorAction SilentlyContinue

if ($apiPortInUse) {
    Write-Host "⚠️ Port $ApiPort is already in use. API may fail to start." -ForegroundColor Yellow
}

if ($pwaPortInUse) {
    Write-Host "⚠️ Port $PwaPort is already in use. PWA may fail to start." -ForegroundColor Yellow
}

# Navigate to solution root
$scriptPath = Split-Path $MyInvocation.MyCommand.Path -Parent
$solutionRoot = Join-Path $scriptPath "..\\.."
Set-Location $solutionRoot

Write-Host "📁 Working directory: $PWD" -ForegroundColor White

# Build solution if not skipped
if (-not $SkipBuild) {
    Write-Host "🔨 Building solution..." -ForegroundColor Yellow
    
    dotnet restore
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to restore packages" -ForegroundColor Red
        exit 1
    }
    
    dotnet build -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✅ Build completed successfully" -ForegroundColor Green
} else {
    Write-Host "⏭️ Skipping build (--SkipBuild specified)" -ForegroundColor Yellow
}

# Run tests to ensure everything works
Write-Host "🧪 Running tests..." -ForegroundColor Yellow
dotnet test tests/MAK3R.UnitTests --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "⚠️ Some tests failed, but continuing with demo setup" -ForegroundColor Yellow
} else {
    Write-Host "✅ All tests passed" -ForegroundColor Green
}

# Generate demo data if requested
if ($GenerateData) {
    Write-Host "📊 Generating demo data..." -ForegroundColor Yellow
    
    # Create sample products CSV
    $sampleProductsCsv = @"
Name,SKU,Price,Description,Active
CNC Precision Mill XZ-3000,CNC-XZ-3000,125000.00,"High-precision 3-axis CNC mill for complex machining operations",true
Hydraulic Press HP-500,PRESS-HP-500,85000.00,"500-ton hydraulic press for metal forming and stamping",true
Industrial Robot ARM-6D,ROBOT-ARM-6D,95000.00,"6-axis industrial robot arm for automated assembly",true
Quality Inspection Station QIS-Pro,QIS-PRO-001,35000.00,"Automated quality inspection with vision system",true
Conveyor Belt System CBS-2000,CBS-2000,15000.00,"2000mm wide conveyor belt for material handling",false
"@
    
    $demoDataPath = "wwwroot/sample-data"
    New-Item -Path "apps/MAK3R.PWA/$demoDataPath" -ItemType Directory -Force | Out-Null
    $sampleProductsCsv | Out-File -FilePath "apps/MAK3R.PWA/$demoDataPath/sample-products.csv" -Encoding UTF8
    
    # Create sample machines CSV
    $sampleMachinesCsv = @"
Equipment_ID,Model_Number,Serial_Number,Location,Status,Temperature,RPM
CNC-001,XZ-3000,SN123456,Factory Floor A,Running,75,2100
PRESS-001,HP-500,SN789012,Factory Floor A,Idle,68,0
ROBOT-001,ARM-6D,SN345678,Assembly Line 1,Running,72,0
QIS-001,QIS-Pro,SN901234,Quality Control,Running,70,0
CBS-001,CBS-2000,SN567890,Material Handling,Maintenance,65,150
"@
    
    $sampleMachinesCsv | Out-File -FilePath "apps/MAK3R.PWA/$demoDataPath/sample-machines.csv" -Encoding UTF8
    
    Write-Host "✅ Demo data generated in $demoDataPath/" -ForegroundColor Green
}

# Create demo launch script
$launchScript = @"
@echo off
echo Starting MAK3R-Cluster Demo...
echo.

echo Starting API server (Port: $ApiPort)...
start "MAK3R API" powershell -Command "cd '$PWD'; dotnet run --project services/MAK3R.Api --urls http://localhost:$ApiPort"

timeout /t 5 /nobreak >nul

echo Starting PWA application (Port: $PwaPort)...
start "MAK3R PWA" powershell -Command "cd '$PWD'; dotnet run --project apps/MAK3R.PWA --urls https://localhost:$PwaPort;http://localhost:$([int]$PwaPort - 2000)"

timeout /t 8 /nobreak >nul

echo Opening browser...
start https://localhost:$PwaPort

echo.
echo Demo environment is starting...
echo API: http://localhost:$ApiPort
echo PWA: https://localhost:$PwaPort
echo.
echo Press any key to stop demo servers...
pause >nul

echo Stopping demo servers...
taskkill /f /im dotnet.exe
"@

$launchScript | Out-File -FilePath "scripts/demo/start-demo.bat" -Encoding ASCII

Write-Host "📋 Demo Setup Complete!" -ForegroundColor Green
Write-Host "======================" -ForegroundColor Green
Write-Host "🌐 API will be available at: http://localhost:$ApiPort" -ForegroundColor White
Write-Host "🖥️ PWA will be available at: https://localhost:$PwaPort" -ForegroundColor White
Write-Host "" -ForegroundColor White
Write-Host "📖 Demo Features:" -ForegroundColor Cyan
Write-Host "  • Landing page with progressive digital twin messaging" -ForegroundColor White
Write-Host "  • Machine Wall with real-time telemetry simulation" -ForegroundColor White
Write-Host "  • Shopfront Builder with product management" -ForegroundColor White
Write-Host "  • File Ingestion with intelligent schema inference" -ForegroundColor White
Write-Host "  • Anomaly Workbench for data quality monitoring" -ForegroundColor White
Write-Host "" -ForegroundColor White

if ($GenerateData) {
    Write-Host "📊 Sample data files created:" -ForegroundColor Cyan
    Write-Host "  • apps/MAK3R.PWA/wwwroot/sample-data/sample-products.csv" -ForegroundColor White
    Write-Host "  • apps/MAK3R.PWA/wwwroot/sample-data/sample-machines.csv" -ForegroundColor White
    Write-Host "" -ForegroundColor White
}

Write-Host "🚀 Ready to start demo!" -ForegroundColor Green
Write-Host "Run: .\scripts\demo\start-demo.bat" -ForegroundColor Yellow
Write-Host "" -ForegroundColor White

# Provide manual start instructions
Write-Host "🔧 Manual Start Instructions:" -ForegroundColor Cyan
Write-Host "1. Start API: dotnet run --project services/MAK3R.Api --urls http://localhost:$ApiPort" -ForegroundColor White
Write-Host "2. Start PWA: dotnet run --project apps/MAK3R.PWA --urls https://localhost:$PwaPort;http://localhost:$([int]$PwaPort - 2000)" -ForegroundColor White
Write-Host "3. Open browser to: https://localhost:$PwaPort" -ForegroundColor White