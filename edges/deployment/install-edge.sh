#!/bin/bash
set -euo pipefail

# MAK3R Edge Deployment Script
# This script installs and configures the MAK3R Edge runtime

EDGE_VERSION="${1:-latest}"
INSTALL_DIR="/opt/mak3r/edge"
SERVICE_USER="mak3r"
CONFIG_DIR="/etc/mak3r"
LOG_DIR="/var/log/mak3r"

echo "=== MAK3R Edge Deployment Script ==="
echo "Version: $EDGE_VERSION"
echo "Install Directory: $INSTALL_DIR"

# Check if running as root
if [[ $EUID -ne 0 ]]; then
   echo "This script must be run as root (use sudo)" 
   exit 1
fi

# Create service user
if ! id "$SERVICE_USER" &>/dev/null; then
    echo "Creating service user: $SERVICE_USER"
    useradd --system --home-dir $INSTALL_DIR --shell /bin/false --comment "MAK3R Edge Service" $SERVICE_USER
fi

# Create directories
echo "Creating directories..."
mkdir -p $INSTALL_DIR/{data,logs,config}
mkdir -p $CONFIG_DIR
mkdir -p $LOG_DIR

# Set permissions
chown -R $SERVICE_USER:$SERVICE_USER $INSTALL_DIR
chown -R $SERVICE_USER:$SERVICE_USER $LOG_DIR
chown -R root:$SERVICE_USER $CONFIG_DIR
chmod 750 $CONFIG_DIR

# Install .NET 8.0 runtime if not present
if ! command -v dotnet &> /dev/null; then
    echo "Installing .NET 8.0 runtime..."
    wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
    apt-get update
    apt-get install -y aspnetcore-runtime-8.0
fi

# Download and extract Edge runtime
echo "Downloading MAK3R Edge runtime..."
DOWNLOAD_URL="https://releases.mak3r.ai/edge/$EDGE_VERSION/mak3r-edge-linux-x64.tar.gz"

# For development, use local build
if [ "$EDGE_VERSION" = "local" ]; then
    echo "Using local build..."
    if [ -d "../../MAK3R.Edge/bin/Release/net8.0/publish" ]; then
        cp -r ../../MAK3R.Edge/bin/Release/net8.0/publish/* $INSTALL_DIR/
    else
        echo "Local build not found. Please run 'dotnet publish -c Release' first."
        exit 1
    fi
else
    wget -O mak3r-edge.tar.gz "$DOWNLOAD_URL"
    tar -xzf mak3r-edge.tar.gz -C $INSTALL_DIR --strip-components=1
    rm mak3r-edge.tar.gz
fi

# Make binary executable
chmod +x $INSTALL_DIR/MAK3R.Edge

# Install systemd service
echo "Installing systemd service..."
cp systemd/mak3r-edge.service /etc/systemd/system/
systemctl daemon-reload
systemctl enable mak3r-edge

# Create default configuration
echo "Creating default configuration..."
cat > $CONFIG_DIR/edge-config.json << EOF
{
  "SiteId": "SITE-001",
  "Timezone": "UTC",
  "Uplink": {
    "HubUrl": "https://cluster.mak3r.ai/hubs/machines",
    "ReconnectDelayMs": 5000,
    "Batch": {
      "MaxEvents": 50,
      "MaxSizeBytes": 32768,
      "FlushIntervalMs": 5000
    }
  },
  "AdminApi": {
    "Listen": "http://0.0.0.0:9080"
  },
  "Storage": {
    "Root": "/opt/mak3r/edge/data",
    "Sqlite": {
      "Path": "/opt/mak3r/edge/data/netdiag.db"
    }
  },
  "Queue": {
    "Capacity": 10000
  },
  "LoadGen": {
    "Enabled": false,
    "Machines": 0
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "MAK3R": "Debug"
    }
  }
}
EOF

cat > $CONFIG_DIR/machines.json << EOF
{
  "machines": [
    {
      "machineId": "FANUC-TC-01",
      "make": "FANUC",
      "model": "30i-B",
      "ipAddress": "10.10.20.11",
      "protocol": "FOCAS",
      "enabled": true,
      "settings": {
        "Port": 8193,
        "IsSimulator": false,
        "PollIntervalMs": 500
      }
    },
    {
      "machineId": "SIEMENS-TC-02", 
      "make": "SIEMENS",
      "model": "840D sl",
      "ipAddress": "10.10.20.12",
      "protocol": "OPC UA",
      "enabled": true,
      "settings": {
        "EndpointUrl": "opc.tcp://10.10.20.12:4840",
        "IsSimulator": false,
        "SecurityPolicy": "None"
      }
    },
    {
      "machineId": "HAAS-MILL-03",
      "make": "HAAS", 
      "model": "VF-2SS",
      "ipAddress": "10.10.20.13",
      "protocol": "MTConnect",
      "enabled": true,
      "settings": {
        "BaseUrl": "http://10.10.20.13:8082/VF2SS",
        "IsSimulator": false,
        "SampleIntervalMs": 1000
      }
    },
    {
      "machineId": "MAZAK-5X-04",
      "make": "MAZAK",
      "model": "VARIAXIS j-600",
      "ipAddress": "10.10.20.14", 
      "protocol": "MTConnect",
      "enabled": true,
      "settings": {
        "BaseUrl": "http://10.10.20.14:5000/MAZAK",
        "IsSimulator": false,
        "SampleIntervalMs": 1000
      }
    }
  ]
}
EOF

# Set configuration permissions
chown root:$SERVICE_USER $CONFIG_DIR/edge-config.json $CONFIG_DIR/machines.json
chmod 640 $CONFIG_DIR/edge-config.json $CONFIG_DIR/machines.json

# Create environment file
cat > /etc/default/mak3r-edge << EOF
# MAK3R Edge Environment Configuration
EDGE_CONFIG_PATH=$CONFIG_DIR/edge-config.json
MACHINES_CONFIG_PATH=$CONFIG_DIR/machines.json
EOF

# Install log rotation
cat > /etc/logrotate.d/mak3r-edge << EOF
$LOG_DIR/*.log {
    daily
    rotate 30
    compress
    delaycompress
    missingok
    notifempty
    create 644 $SERVICE_USER $SERVICE_USER
    postrotate
        systemctl reload mak3r-edge || true
    endscript
}
EOF

# Create health check script
cat > $INSTALL_DIR/healthcheck.sh << 'EOF'
#!/bin/bash
# MAK3R Edge Health Check

ADMIN_URL="${EDGE_ADMIN_URL:-http://localhost:9080}"

# Check if service is running
if ! systemctl is-active --quiet mak3r-edge; then
    echo "CRITICAL: MAK3R Edge service is not running"
    exit 2
fi

# Check health endpoint
if ! curl -sf "$ADMIN_URL/health" > /dev/null; then
    echo "WARNING: Admin API health check failed"
    exit 1
fi

# Check metrics
METRICS=$(curl -sf "$ADMIN_URL/metrics" | jq -r '.queueDepth // "unknown"')
if [ "$METRICS" = "unknown" ]; then
    echo "WARNING: Unable to retrieve metrics"
    exit 1
fi

echo "OK: MAK3R Edge is healthy (queue depth: $METRICS)"
exit 0
EOF

chmod +x $INSTALL_DIR/healthcheck.sh

echo ""
echo "=== Installation Complete ==="
echo ""
echo "Next steps:"
echo "1. Edit configuration: $CONFIG_DIR/edge-config.json"
echo "2. Configure machines: $CONFIG_DIR/machines.json" 
echo "3. Start the service: sudo systemctl start mak3r-edge"
echo "4. Check status: sudo systemctl status mak3r-edge"
echo "5. View logs: sudo journalctl -u mak3r-edge -f"
echo "6. Access admin UI: http://localhost:9080"
echo ""
echo "Health check: $INSTALL_DIR/healthcheck.sh"
echo ""