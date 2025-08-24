# MAK3R Test Runner Script
# Runs unit tests, integration tests, and generates coverage reports

param(
    [string]$TestProject = "all",
    [switch]$Coverage = $false,
    [switch]$Watch = $false,
    [string]$Filter = "",
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"

# Colors for output
$Green = [System.ConsoleColor]::Green
$Red = [System.ConsoleColor]::Red
$Yellow = [System.ConsoleColor]::Yellow
$Cyan = [System.ConsoleColor]::Cyan

function Write-ColoredOutput {
    param([string]$Message, [System.ConsoleColor]$Color = [System.ConsoleColor]::White)
    Write-Host $Message -ForegroundColor $Color
}

function Test-Prerequisites {
    Write-ColoredOutput "🔍 Checking prerequisites..." $Cyan
    
    # Check if .NET is installed
    try {
        $dotnetVersion = dotnet --version
        Write-ColoredOutput "✅ .NET SDK: $dotnetVersion" $Green
    }
    catch {
        Write-ColoredOutput "❌ .NET SDK not found. Please install .NET 8.0 SDK." $Red
        exit 1
    }

    # Check if we're in the correct directory
    if (-not (Test-Path "MAK3R-Cluster.sln")) {
        Write-ColoredOutput "❌ Must run from MAK3R-Cluster root directory" $Red
        exit 1
    }

    Write-ColoredOutput "✅ Prerequisites check passed" $Green
    Write-Host ""
}

function Restore-Packages {
    Write-ColoredOutput "📦 Restoring NuGet packages..." $Cyan
    
    try {
        dotnet restore --verbosity quiet
        Write-ColoredOutput "✅ Package restoration completed" $Green
    }
    catch {
        Write-ColoredOutput "❌ Package restoration failed" $Red
        exit 1
    }
    Write-Host ""
}

function Build-Solution {
    Write-ColoredOutput "🔨 Building solution..." $Cyan
    
    try {
        if ($Verbose) {
            dotnet build --no-restore --verbosity normal
        } else {
            dotnet build --no-restore --verbosity quiet
        }
        Write-ColoredOutput "✅ Build completed successfully" $Green
    }
    catch {
        Write-ColoredOutput "❌ Build failed" $Red
        exit 1
    }
    Write-Host ""
}

function Run-UnitTests {
    Write-ColoredOutput "🧪 Running Unit Tests..." $Cyan
    
    $testArgs = @(
        "test"
        "tests/MAK3R.UnitTests/MAK3R.UnitTests.csproj"
        "--no-build"
        "--verbosity"
        "normal"
        "--logger"
        "console;verbosity=detailed"
    )

    if ($Filter) {
        $testArgs += "--filter"
        $testArgs += $Filter
    }

    if ($Coverage) {
        $testArgs += "--collect"
        $testArgs += "XPlat Code Coverage"
        $testArgs += "--results-directory"
        $testArgs += "./TestResults/UnitTests"
    }

    if ($Watch) {
        $testArgs += "--watch"
    }

    try {
        & dotnet $testArgs
        Write-ColoredOutput "✅ Unit tests completed" $Green
    }
    catch {
        Write-ColoredOutput "❌ Unit tests failed" $Red
        return $false
    }
    
    return $true
}

function Run-IntegrationTests {
    Write-ColoredOutput "🔗 Running Integration Tests..." $Cyan
    
    $testArgs = @(
        "test"
        "tests/MAK3R.IntegrationTests/MAK3R.IntegrationTests.csproj"
        "--no-build"
        "--verbosity"
        "normal"
        "--logger"
        "console;verbosity=detailed"
    )

    if ($Filter) {
        $testArgs += "--filter"
        $testArgs += $Filter
    }

    if ($Coverage) {
        $testArgs += "--collect"
        $testArgs += "XPlat Code Coverage"
        $testArgs += "--results-directory"
        $testArgs += "./TestResults/IntegrationTests"
    }

    try {
        & dotnet $testArgs
        Write-ColoredOutput "✅ Integration tests completed" $Green
    }
    catch {
        Write-ColoredOutput "❌ Integration tests failed" $Red
        return $false
    }
    
    return $true
}

function Generate-CoverageReport {
    if (-not $Coverage) {
        return
    }

    Write-ColoredOutput "📊 Generating coverage report..." $Cyan
    
    # Check if reportgenerator is installed
    try {
        dotnet tool list -g | Select-String "dotnet-reportgenerator-globaltool" | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-ColoredOutput "Installing ReportGenerator..." $Yellow
            dotnet tool install -g dotnet-reportgenerator-globaltool
        }
    }
    catch {
        Write-ColoredOutput "Installing ReportGenerator..." $Yellow
        dotnet tool install -g dotnet-reportgenerator-globaltool
    }

    # Generate HTML report
    try {
        $coverageFiles = Get-ChildItem -Path "./TestResults" -Recurse -Filter "coverage.cobertura.xml" | ForEach-Object { $_.FullName }
        
        if ($coverageFiles.Count -gt 0) {
            $coverageInput = $coverageFiles -join ";"
            reportgenerator "-reports:$coverageInput" "-targetdir:./TestResults/Coverage" "-reporttypes:Html;Badges"
            Write-ColoredOutput "✅ Coverage report generated at ./TestResults/Coverage/index.html" $Green
        } else {
            Write-ColoredOutput "⚠️ No coverage files found" $Yellow
        }
    }
    catch {
        Write-ColoredOutput "❌ Coverage report generation failed" $Red
    }
}

function Show-TestSummary {
    param([bool]$UnitTestsPassed, [bool]$IntegrationTestsPassed)
    
    Write-Host ""
    Write-ColoredOutput "📋 Test Summary" $Cyan
    Write-ColoredOutput "═══════════════" $Cyan
    
    if ($TestProject -eq "all" -or $TestProject -eq "unit") {
        $status = if ($UnitTestsPassed) { "✅ PASSED" } else { "❌ FAILED" }
        $color = if ($UnitTestsPassed) { $Green } else { $Red }
        Write-ColoredOutput "Unit Tests:        $status" $color
    }
    
    if ($TestProject -eq "all" -or $TestProject -eq "integration") {
        $status = if ($IntegrationTestsPassed) { "✅ PASSED" } else { "❌ FAILED" }
        $color = if ($IntegrationTestsPassed) { $Green } else { $Red }
        Write-ColoredOutput "Integration Tests: $status" $color
    }
    
    if ($Coverage) {
        Write-ColoredOutput "Coverage Report:   ./TestResults/Coverage/index.html" $Cyan
    }
    
    Write-Host ""
}

# Main execution
Write-ColoredOutput "🚀 MAK3R Test Runner" $Green
Write-ColoredOutput "═══════════════════" $Green
Write-Host ""

Test-Prerequisites
Restore-Packages
Build-Solution

$unitTestsPassed = $true
$integrationTestsPassed = $true

# Run tests based on the selected project
switch ($TestProject.ToLower()) {
    "unit" {
        $unitTestsPassed = Run-UnitTests
    }
    "integration" {
        $integrationTestsPassed = Run-IntegrationTests
    }
    "all" {
        $unitTestsPassed = Run-UnitTests
        Write-Host ""
        $integrationTestsPassed = Run-IntegrationTests
    }
    default {
        Write-ColoredOutput "❌ Invalid test project: $TestProject. Use 'unit', 'integration', or 'all'" $Red
        exit 1
    }
}

Generate-CoverageReport
Show-TestSummary -UnitTestsPassed $unitTestsPassed -IntegrationTestsPassed $integrationTestsPassed

# Exit with error code if any tests failed
if (-not $unitTestsPassed -or -not $integrationTestsPassed) {
    exit 1
}

Write-ColoredOutput "🎉 All tests passed!" $Green