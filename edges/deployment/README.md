# MAK3R Edge Deployment Guide

This directory contains deployment scripts and configurations for the MAK3R Edge runtime.

## Deployment Options

### 1. Docker Deployment (Recommended)

```bash
# Development deployment
cp .env.template .env
# Edit .env with your configuration
docker-compose up -d

# Production deployment with custom config
SITE_ID=PROD-PLANT-01 HUB_URL=https://cluster.mak3r.ai/hubs/machines docker-compose up -d
```

### 2. Linux System Service

```bash
# Install as systemd service
cd deployment
sudo chmod +x install-edge.sh
sudo ./install-edge.sh

# Using local build
sudo ./install-edge.sh local

# Configure and start
sudo vim /etc/mak3r/edge-config.json
sudo vim /etc/mak3r/machines.json  
sudo systemctl start mak3r-edge
```

### 3. Windows Service

```powershell
# Run as Administrator
cd deployment
.\deploy.ps1 -SiteId "PLANT-01" -HubUrl "https://localhost:7228/hubs/machines" -Development

# Check service status
Get-Service MAK3REdge
```

## Configuration

### Edge Configuration (`edge-config.json`)

```json
{
  "SiteId": "Your-Site-ID",
  "Timezone": "Your/Timezone", 
  "Uplink": {
    "HubUrl": "https://your-cluster.com/hubs/machines"
  },
  "AdminApi": {
    "Listen": "http://0.0.0.0:9080"
  }
}
```

### Machine Configuration (`machines.json`)

Define your machines with their specific protocols:

- **FANUC**: FOCAS protocol with port configuration
- **Siemens**: OPC UA with endpoint URL and security settings  
- **HAAS**: MTConnect with base URL and sampling intervals
- **Mazak**: MTConnect with base URL and enhanced features

## Monitoring

### Admin API Endpoints

- `GET /health` - Service health check
- `GET /metrics` - Real-time metrics
- `GET /config` - Current configuration
- `GET /netdiag/stats` - Network diagnostics
- `GET /connectors` - Connector status

### Docker Monitoring Stack

The docker-compose includes:
- **Prometheus**: Metrics collection (port 9090)
- **Grafana**: Visualization dashboard (port 3000)

Access Grafana at http://localhost:3000 (admin/admin123)

## Troubleshooting

### Service Issues

```bash
# Linux
sudo systemctl status mak3r-edge
sudo journalctl -u mak3r-edge -f

# Windows  
Get-Service MAK3REdge
Get-EventLog -LogName Application -Source MAK3REdge -Newest 10
```

### Network Connectivity

```bash
# Test cluster connection
curl -v https://your-cluster.com/hubs/machines

# Test machine connectivity
ping 10.10.20.11  # FANUC
curl http://10.10.20.13:8082/VF2SS/current  # HAAS MTConnect
```

### Configuration Validation

```bash
# Validate JSON configuration
cat /etc/mak3r/edge-config.json | jq .
cat /etc/mak3r/machines.json | jq .

# Test admin API
curl http://localhost:9080/health
curl http://localhost:9080/config
```

## Security Considerations

### Production Checklist

- [ ] Change default admin passwords
- [ ] Configure proper firewall rules
- [ ] Use dedicated service accounts
- [ ] Enable TLS for cluster communication
- [ ] Set up log rotation
- [ ] Configure monitoring alerts
- [ ] Regular security updates

### Network Security

- Edge runtime requires outbound HTTPS (443) to cluster
- Admin API listens on configurable port (default 9080)
- Machine protocols require specific firewall rules:
  - FOCAS: TCP/8193
  - OPC UA: TCP/4840
  - MTConnect: HTTP/various ports

## Performance Tuning

### Hardware Recommendations

- **CPU**: 2+ cores (4+ recommended for 10+ machines)
- **Memory**: 4GB RAM minimum (8GB+ recommended)
- **Storage**: 50GB+ for event spooling and logs
- **Network**: Gigabit Ethernet with low latency to machines

### Configuration Tuning

```json
{
  "Queue": {
    "Capacity": 20000  // Increase for high-volume sites
  },
  "Uplink": {
    "Batch": {
      "MaxEvents": 100,     // Larger batches for efficiency
      "FlushIntervalMs": 2000  // Faster flush for low-latency
    }
  }
}
```

## Backup and Recovery

### Important Files

- `/etc/mak3r/` - Configuration files
- `/opt/mak3r/edge/data/` - SQLite diagnostics database  
- `/var/log/mak3r/` - Application logs

### Backup Script

```bash
#!/bin/bash
tar -czf mak3r-edge-backup-$(date +%Y%m%d).tar.gz \
  /etc/mak3r/ \
  /opt/mak3r/edge/data/ \
  /var/log/mak3r/
```