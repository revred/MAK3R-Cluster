# MAK3R Edge Windows Deployment Script
param(
    [string]$SiteId = "SITE-001",
    [string]$HubUrl = "https://localhost:7228/hubs/machines",
    [string]$InstallPath = "C:\MAK3R\Edge",
    [string]$ServiceName = "MAK3REdge",
    [switch]$Development
)

Write-Host "=== MAK3R Edge Windows Deployment ===" -ForegroundColor Green
Write-Host "Site ID: $SiteId"
Write-Host "Hub URL: $HubUrl" 
Write-Host "Install Path: $InstallPath"

# Check if running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator"
    exit 1
}

# Create directories
Write-Host "Creating directories..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path "$InstallPath\data"
New-Item -ItemType Directory -Force -Path "$InstallPath\logs" 
New-Item -ItemType Directory -Force -Path "$InstallPath\config"

# Download .NET 8 if not installed
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Installing .NET 8.0..." -ForegroundColor Yellow
    $dotnetUrl = "https://download.microsoft.com/download/7/8/b/78b16d5c-acff-4d62-8b8b-72dea91a2c9d/dotnet-hosting-8.0.0-win.exe"
    Invoke-WebRequest -Uri $dotnetUrl -OutFile "$env:TEMP\dotnet-hosting.exe"
    Start-Process -FilePath "$env:TEMP\dotnet-hosting.exe" -ArgumentList "/quiet" -Wait
    Remove-Item "$env:TEMP\dotnet-hosting.exe"
}

# Copy application files
Write-Host "Copying application files..." -ForegroundColor Yellow
if ($Development) {
    $sourcePath = "..\..\MAK3R.Edge\bin\Release\net8.0\publish"
    if (Test-Path $sourcePath) {
        Copy-Item -Path "$sourcePath\*" -Destination $InstallPath -Recurse -Force
    } else {
        Write-Error "Development build not found. Please run 'dotnet publish -c Release' first."
        exit 1
    }
} else {
    # In production, would download from release artifacts
    Write-Host "Production deployment not implemented - use Development switch for now"
    exit 1
}

# Create configuration files
Write-Host "Creating configuration files..." -ForegroundColor Yellow

$edgeConfig = @{
    SiteId = $SiteId
    Timezone = [TimeZoneInfo]::Local.Id
    Uplink = @{
        HubUrl = $HubUrl
        ReconnectDelayMs = 5000
        Batch = @{
            MaxEvents = 50
            MaxSizeBytes = 32768
            FlushIntervalMs = 5000
        }
    }
    AdminApi = @{
        Listen = "http://localhost:9080"
    }
    Storage = @{
        Root = "$InstallPath\data"
        Sqlite = @{
            Path = "$InstallPath\data\netdiag.db"
        }
    }
    Queue = @{
        Capacity = 10000
    }
    LoadGen = @{
        Enabled = $true
        Machines = 4
    }
    Logging = @{
        LogLevel = @{
            Default = "Information"
            MAK3R = "Debug"
        }
    }
} | ConvertTo-Json -Depth 10

$edgeConfig | Out-File -FilePath "$InstallPath\config\edge-config.json" -Encoding UTF8

$machinesConfig = @{
    machines = @(
        @{
            machineId = "FANUC-TC-01"
            make = "FANUC"
            model = "30i-B"
            ipAddress = "10.10.20.11"
            protocol = "FOCAS"
            enabled = $true
            settings = @{
                Port = 8193
                IsSimulator = $true
                PollIntervalMs = 500
            }
        },
        @{
            machineId = "SIEMENS-TC-02"
            make = "SIEMENS"
            model = "840D sl"
            ipAddress = "10.10.20.12"
            protocol = "OPC UA"
            enabled = $true
            settings = @{
                EndpointUrl = "opc.tcp://10.10.20.12:4840"
                IsSimulator = $true
                SecurityPolicy = "None"
            }
        },
        @{
            machineId = "HAAS-MILL-03"
            make = "HAAS"
            model = "VF-2SS"
            ipAddress = "10.10.20.13"
            protocol = "MTConnect"
            enabled = $true
            settings = @{
                BaseUrl = "http://10.10.20.13:8082/VF2SS"
                IsSimulator = $true
                SampleIntervalMs = 1000
            }
        },
        @{
            machineId = "MAZAK-5X-04"
            make = "MAZAK"
            model = "VARIAXIS j-600"
            ipAddress = "10.10.20.14"
            protocol = "MTConnect"
            enabled = $true
            settings = @{
                BaseUrl = "http://10.10.20.14:5000/MAZAK"
                IsSimulator = $true
                SampleIntervalMs = 1000
            }
        }
    )
} | ConvertTo-Json -Depth 10

$machinesConfig | Out-File -FilePath "$InstallPath\config\machines.json" -Encoding UTF8

# Install Windows Service
Write-Host "Installing Windows Service..." -ForegroundColor Yellow

$serviceExists = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($serviceExists) {
    Stop-Service -Name $ServiceName -Force
    & sc.exe delete $ServiceName
    Start-Sleep -Seconds 2
}

& sc.exe create $ServiceName binpath= "$InstallPath\MAK3R.Edge.exe" DisplayName= "MAK3R Edge Runtime" start= auto
& sc.exe description $ServiceName "MAK3R Edge Runtime for Industrial Machine Connectivity"

# Configure service recovery
& sc.exe failure $ServiceName reset= 300 actions= restart/5000/restart/5000/restart/10000

# Set service to run as Local System (in production, use dedicated service account)
# & sc.exe config $ServiceName obj= ".\MAK3REdgeService" password= "SecurePassword123"

Write-Host "Starting service..." -ForegroundColor Yellow
Start-Service -Name $ServiceName

# Add Windows Firewall rules
Write-Host "Configuring Windows Firewall..." -ForegroundColor Yellow
New-NetFirewallRule -DisplayName "MAK3R Edge Admin API" -Direction Inbound -Port 9080 -Protocol TCP -Action Allow -ErrorAction SilentlyContinue

Write-Host "=== Deployment Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Service Status:"
Get-Service -Name $ServiceName | Format-Table -AutoSize
Write-Host ""
Write-Host "Admin API: http://localhost:9080"
Write-Host "Health Check: http://localhost:9080/health"
Write-Host "Configuration: $InstallPath\config\"
Write-Host "Logs: $InstallPath\logs\"
Write-Host ""
Write-Host "To manage the service:"
Write-Host "  Start:   Start-Service $ServiceName"
Write-Host "  Stop:    Stop-Service $ServiceName"
Write-Host "  Restart: Restart-Service $ServiceName"
Write-Host "  Status:  Get-Service $ServiceName"