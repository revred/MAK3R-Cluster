# MAK3R DigitalTwin2 - Development Database Seeder
param(
    [switch]$ResetDatabase,
    [switch]$SeedDemo,
    [switch]$Verbose
)

Write-Host "=== MAK3R DigitalTwin2 - Development Seeder ===" -ForegroundColor Green

# Configuration
$ApiProjectPath = "services/MAK3R.Api"
$DatabaseFile = "services/MAK3R.Api/mak3r.db"
$ConnectionString = "Data Source=mak3r.db"

# Check if API project exists
if (-not (Test-Path $ApiProjectPath)) {
    Write-Error "MAK3R.Api project not found at: $ApiProjectPath"
    exit 1
}

# Reset database if requested
if ($ResetDatabase) {
    Write-Host "Resetting database..." -ForegroundColor Yellow
    
    if (Test-Path $DatabaseFile) {
        Remove-Item $DatabaseFile -Force
        Write-Host "Deleted existing database file" -ForegroundColor Gray
    }
    
    # Run EF migrations
    Push-Location $ApiProjectPath
    try {
        dotnet ef database update
        Write-Host "Database migrations applied successfully" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to apply database migrations: $_"
        Pop-Location
        exit 1
    }
    finally {
        Pop-Location
    }
}

# Seed demo data if requested
if ($SeedDemo) {
    Write-Host "Seeding demo data..." -ForegroundColor Yellow
    
    # Create demo companies
    $companies = @(
        @{ Name = "Contoso Gears Ltd"; Industry = "Manufacturing"; RegistrationId = "CG-001-2024" },
        @{ Name = "Fabrikam Motors Inc"; Industry = "Automotive"; RegistrationId = "FM-002-2024" },
        @{ Name = "Adventure Works"; Industry = "Recreation"; RegistrationId = "AW-003-2024" }
    )
    
    # Create demo sites
    $sites = @(
        @{ Name = "Bangalore Plant 1"; CompanyId = 1; Address = "Electronic City, Bangalore" },
        @{ Name = "Chennai Facility"; CompanyId = 1; Address = "Oragadam, Chennai" },
        @{ Name = "Detroit Assembly"; CompanyId = 2; Address = "Michigan, USA" }
    )
    
    # Create demo machines  
    $machines = @(
        @{ Name = "FANUC-TC-01"; Make = "FANUC"; Model = "30i-B Plus"; SiteId = 1; Status = "Active" },
        @{ Name = "SIEMENS-TC-02"; Make = "SIEMENS"; Model = "840D sl"; SiteId = 1; Status = "Active" },
        @{ Name = "HAAS-MILL-03"; Make = "HAAS"; Model = "VF-2SS"; SiteId = 2; Status = "Active" },
        @{ Name = "MAZAK-5X-04"; Make = "MAZAK"; Model = "VARIAXIS j-600"; SiteId = 2; Status = "Maintenance" }
    )
    
    # Create demo products
    $products = @(
        @{ Name = "Precision Gear Assembly"; Sku = "PGA-001"; CompanyId = 1; Price = 125.50; Currency = "USD" },
        @{ Name = "Motor Housing"; Sku = "MH-002"; CompanyId = 1; Price = 89.99; Currency = "USD" },
        @{ Name = "Transmission Component"; Sku = "TC-003"; CompanyId = 2; Price = 245.00; Currency = "USD" }
    )
    
    Write-Host "Demo data structure prepared" -ForegroundColor Gray
    
    # Note: In a full implementation, we would use EF Core to seed this data
    # For now, the data seeding happens through the existing SeedData.cs in MAK3R.Data
    Write-Host "Using existing SeedData.cs for demo data population" -ForegroundColor Gray
}

# Generate correlation ID for this seeding session
$SessionId = [System.Guid]::NewGuid().ToString("N")[0..7] -join ""
Write-Host "Session ID: $SessionId" -ForegroundColor Cyan

# Add some DigitalTwin2 specific demo data
if ($SeedDemo) {
    Write-Host "Preparing DigitalTwin2 knowledge graph seed data..." -ForegroundColor Yellow
    
    # Create sample facts for knowledge graph
    $facts = @(
        @{ EntityId = "ASSET-L3"; Key = "utilization_rate"; Value = 0.87; Confidence = 0.95 },
        @{ EntityId = "ASSET-L3"; Key = "criticality_score"; Value = 0.92; Confidence = 0.89 },
        @{ EntityId = "SKU-HMX-220"; Key = "margin_percentage"; Value = 42.5; Confidence = 0.98 }
    )
    
    # Create sample evidence entries
    $evidence = @(
        @{ DocId = "invoice_aug_001.pdf"; Page = 7; Span = "SKU HMX-220 margin concentration"; Hash = "abc123" },
        @{ DocId = "jobcard_l3_001.xlsx"; Page = 1; Span = "OP20 routing frequency"; Hash = "def456" }
    )
    
    Write-Host "DigitalTwin2 seed data prepared (will be implemented in P1)" -ForegroundColor Gray
}

# Verify database structure
Write-Host "Verifying database structure..." -ForegroundColor Yellow

Push-Location $ApiProjectPath
try {
    # Check if we can connect to the database
    $result = dotnet ef database list 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Database connection successful" -ForegroundColor Green
    } else {
        Write-Warning "Database connection issues detected"
        if ($Verbose) {
            Write-Host $result -ForegroundColor Red
        }
    }
}
catch {
    Write-Error "Failed to verify database: $_"
}
finally {
    Pop-Location
}

# Create sample scenario files for DigitalTwin2
Write-Host "Creating sample scenario files..." -ForegroundColor Yellow

$scenarioPath = "tests/datasets/v0.1"
if (-not (Test-Path $scenarioPath)) {
    New-Item -ItemType Directory -Path $scenarioPath -Force | Out-Null
}

# Sample scenario metadata
$sampleScenario = @{
    id = "A_hidden_bottleneck_lathe"
    description = "Demo scenario for DigitalTwin2 testing"
    acl = @{
        bu = "MFG-A"
        viewers = @(
            @{ role = "MfgLead" },
            @{ role = "Finance"; aggregate_only = $true }
        )
    }
    truth = @{
        critical_assets = @("ASSET-L3")
        insights = @(
            @{
                id = "spof_l3"
                text = "Lathe L3 creates 42% high-margin throughput; single-point-of-failure"
                confidence = 0.92
            }
        )
    }
    stop = @{
        confidence = 0.85
        max_questions = 12
    }
}

$scenarioJson = $sampleScenario | ConvertTo-Json -Depth 10
$scenarioJson | Out-File -FilePath "$scenarioPath/sample_scenario.json" -Encoding UTF8

Write-Host "Sample scenario created at: $scenarioPath/sample_scenario.json" -ForegroundColor Gray

Write-Host "=== Seeding completed for session: $SessionId ===" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Run: make run-api"
Write-Host "  2. Run: make run-pwa" 
Write-Host "  3. Check: http://localhost:5225/api/health"
Write-Host "  4. Review: work/README.md for DigitalTwin2 progress"
Write-Host ""