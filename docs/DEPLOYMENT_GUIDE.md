# Immich Album Downloader - Deployment Guide

## Table of Contents

- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Production Deployment](#production-deployment)
- [Environment Configuration](#environment-configuration)
- [SSL/TLS Setup](#ssltls-setup)
- [Database Management](#database-management)
- [Monitoring and Logging](#monitoring-and-logging)
- [Backup and Restore](#backup-and-restore)
- [Performance Tuning](#performance-tuning)
- [Troubleshooting](#troubleshooting)
- [Security Hardening](#security-hardening)
- [Maintenance](#maintenance)

---

## Prerequisites

### System Requirements

**Minimum**:
- CPU: 2 cores
- RAM: 2 GB
- Disk: 20 GB (plus storage for downloads)
- Docker: 24.0+
- Docker Compose: 2.20+

**Recommended**:
- CPU: 4+ cores
- RAM: 4+ GB
- Disk: 100+ GB (SSD recommended for database)
- Docker: Latest stable
- Docker Compose: Latest stable

### Software Dependencies

- **Docker Engine**: 24.0 or later
- **Docker Compose**: 2.20 or later
- **Git**: For cloning the repository
- **OpenSSL**: For generating JWT secrets (optional, script provided)

### Network Requirements

- **Outbound**: Access to Immich server (HTTPS/443)
- **Inbound**: Port 8082 (or configured port)
- **DNS**: Proper DNS resolution for domain name (production)

---

## Quick Start

### Development/Testing Deployment

1. **Clone Repository**:
   ```bash
   git clone https://github.com/dan-jeff/immich-album-downloader.git
   cd immich-album-downloader
   ```

2. **Generate JWT Secret**:
   ```bash
   # Using OpenSSL
   openssl rand -base64 32

   # Using the provided script
   ./scripts/generate-jwt-key.sh
   ```

3. **Configure Environment**:
   ```bash
   cp .env.example .env
   nano .env
   ```

   Minimal `.env` configuration:
   ```env
   JWT_SECRET_KEY=<generated-secret-from-step-2>
   ASPNETCORE_ENVIRONMENT=Development
   ```

4. **Start Services**:
   ```bash
   docker-compose up -d
   ```

5. **Verify Deployment**:
   ```bash
   # Check container status
   docker-compose ps

   # Check health
   curl http://localhost:8082/api/health

   # View logs
   docker-compose logs -f
   ```

6. **Access Application**:
   - Open browser: `http://localhost:8082`
   - Create initial user account
   - Configure Immich connection

### Stopping Services

```bash
# Stop containers (keep data)
docker-compose stop

# Stop and remove containers (keep data)
docker-compose down

# Stop, remove containers, and delete data
docker-compose down -v
```

---

## Production Deployment

### Step 1: Server Preparation

1. **Update System**:
   ```bash
   sudo apt update && sudo apt upgrade -y
   ```

2. **Install Docker**:
   ```bash
   # Install Docker Engine
   curl -fsSL https://get.docker.com -o get-docker.sh
   sudo sh get-docker.sh

   # Add user to docker group
   sudo usermod -aG docker $USER
   newgrp docker

   # Verify installation
   docker --version
   docker-compose --version
   ```

3. **Configure Firewall**:
   ```bash
   # UFW example
   sudo ufw allow 80/tcp
   sudo ufw allow 443/tcp
   sudo ufw allow 22/tcp
   sudo ufw enable
   ```

### Step 2: Application Setup

1. **Clone Repository**:
   ```bash
   cd /opt
   sudo git clone https://github.com/dan-jeff/immich-album-downloader.git
   sudo chown -R $USER:$USER immich-album-downloader
   cd immich-album-downloader
   ```

2. **Generate Strong JWT Secret**:
   ```bash
   openssl rand -base64 32
   ```
   Save this securely (password manager recommended)

3. **Configure Production Environment**:
   ```bash
   cp .env.example .env
   nano .env
   ```

   Production `.env`:
   ```env
   # Required: Strong JWT secret (32+ characters)
   JWT_SECRET_KEY=<your-very-secure-random-key-here>

   # Environment
   ASPNETCORE_ENVIRONMENT=Production

   # JWT Configuration
   JWT_ISSUER=ImmichDownloader
   JWT_AUDIENCE=ImmichDownloader
   JWT_TOKEN_LIFETIME_HOURS=24

   # CORS (Update with your domain)
   ALLOWED_ORIGINS=https://yourdomain.com,https://www.yourdomain.com

   # Database (default is fine for SQLite)
   ConnectionStrings__DefaultConnection=Data Source=/app/data/immich_downloader.db

   # Optional: Custom port
   # EXPOSE_PORT=8082
   ```

4. **Secure Environment File**:
   ```bash
   chmod 600 .env
   ```

### Step 3: Docker Compose Configuration

1. **Review docker-compose.yml**:
   ```bash
   cat docker-compose.yml
   ```

2. **For Production, Consider**:
   - Change exposed port if needed
   - Add resource limits
   - Configure restart policies

   Example production `docker-compose.yml`:
   ```yaml
   version: '3.9'

   services:
     backend:
       container_name: immich-downloader-backend
       build:
         context: ./backend
         dockerfile: ImmichDownloader.Web/Dockerfile
       environment:
         - ASPNETCORE_ENVIRONMENT=Production
         - ASPNETCORE_URLS=http://+:5000
         - ConnectionStrings__DefaultConnection=Data Source=/app/data/immich_downloader.db
         - JWT_SECRET_KEY=${JWT_SECRET_KEY}
         - Jwt__Issuer=${JWT_ISSUER:-ImmichDownloader}
         - Jwt__Audience=${JWT_AUDIENCE:-ImmichDownloader}
         - Jwt__TokenLifetimeHours=${JWT_TOKEN_LIFETIME_HOURS:-24}
         - ALLOWED_ORIGINS=${ALLOWED_ORIGINS}
       volumes:
         - backend-data:/app/data
       expose:
         - "5000"
       restart: unless-stopped
       deploy:
         resources:
           limits:
             cpus: '2.0'
             memory: 2G
           reservations:
             cpus: '0.5'
             memory: 512M

     frontend:
       container_name: immich-downloader-frontend
       build:
         context: ./frontend
         dockerfile: Dockerfile
         args:
           - REACT_APP_API_URL=/api
       expose:
         - "80"
       restart: unless-stopped
       deploy:
         resources:
           limits:
             cpus: '0.5'
             memory: 256M

     nginx:
       container_name: immich-downloader-nginx
       image: nginx:alpine
       ports:
         - "${EXPOSE_PORT:-8082}:80"
       volumes:
         - ./nginx.conf:/etc/nginx/nginx.conf:ro
       depends_on:
         - backend
         - frontend
       restart: unless-stopped
       deploy:
         resources:
           limits:
             cpus: '0.5'
             memory: 128M

   volumes:
     backend-data:
       driver: local
   ```

### Step 4: Build and Deploy

1. **Build Images**:
   ```bash
   docker-compose build --no-cache
   ```

2. **Start Services**:
   ```bash
   docker-compose up -d
   ```

3. **Verify Deployment**:
   ```bash
   # Check container status
   docker-compose ps

   # Check logs
   docker-compose logs -f

   # Test health endpoint
   curl http://localhost:8082/api/health
   ```

4. **Monitor Startup**:
   ```bash
   # Watch backend logs
   docker-compose logs -f backend

   # Watch all logs
   docker-compose logs -f
   ```

### Step 5: Initial Configuration

1. **Access Application**:
   - Navigate to `http://your-server-ip:8082`

2. **Create Admin Account**:
   - Click "Register"
   - Create strong credentials
   - Save credentials securely

3. **Configure Immich Connection**:
   - Go to "Configuration" page
   - Enter Immich server URL
   - Enter Immich API key
   - Click "Test Connection"
   - Save configuration

4. **Verify Functionality**:
   - Browse to "Albums" page
   - Verify albums load from Immich
   - Test a small download

---

## Environment Configuration

### Complete Environment Variables

```env
# ============================================
# REQUIRED SETTINGS
# ============================================

# JWT Secret Key (REQUIRED - Generate with: openssl rand -base64 32)
JWT_SECRET_KEY=your-very-secure-random-key-minimum-32-characters

# ============================================
# ENVIRONMENT CONFIGURATION
# ============================================

# Environment: Development, Production, Staging
ASPNETCORE_ENVIRONMENT=Production

# ============================================
# JWT CONFIGURATION
# ============================================

# JWT Issuer (default: ImmichDownloader)
JWT_ISSUER=ImmichDownloader

# JWT Audience (default: ImmichDownloader)
JWT_AUDIENCE=ImmichDownloader

# JWT Token Lifetime in hours (default: 24)
JWT_TOKEN_LIFETIME_HOURS=24

# ============================================
# CORS CONFIGURATION
# ============================================

# Allowed Origins (comma-separated, REQUIRED for production)
ALLOWED_ORIGINS=https://yourdomain.com,https://www.yourdomain.com

# ============================================
# DATABASE CONFIGURATION
# ============================================

# SQLite Connection String (default is fine)
ConnectionStrings__DefaultConnection=Data Source=/app/data/immich_downloader.db

# ============================================
# APPLICATION CONFIGURATION
# ============================================

# Data Path (default: /app/data)
# DataPath=/app/data

# ============================================
# DOCKER CONFIGURATION
# ============================================

# Exposed Port (default: 8082)
EXPOSE_PORT=8082

# Backend Port (default: 5000, usually doesn't need changing)
# ASPNETCORE_URLS=http://+:5000
```

### Environment-Specific Settings

#### Development
```env
ASPNETCORE_ENVIRONMENT=Development
JWT_SECRET_KEY=dev-key-at-least-32-characters-long
ALLOWED_ORIGINS=http://localhost:3000,http://localhost:8082
```

#### Staging
```env
ASPNETCORE_ENVIRONMENT=Staging
JWT_SECRET_KEY=<secure-staging-key>
ALLOWED_ORIGINS=https://staging.yourdomain.com
```

#### Production
```env
ASPNETCORE_ENVIRONMENT=Production
JWT_SECRET_KEY=<very-secure-production-key>
ALLOWED_ORIGINS=https://yourdomain.com,https://www.yourdomain.com
JWT_TOKEN_LIFETIME_HOURS=12
```

---

## SSL/TLS Setup

### Option 1: Nginx with Let's Encrypt (Recommended)

1. **Install Certbot**:
   ```bash
   sudo apt install certbot python3-certbot-nginx -y
   ```

2. **Update Nginx Configuration**:

   Create `nginx-ssl.conf`:
   ```nginx
   server {
       listen 80;
       server_name yourdomain.com www.yourdomain.com;
       return 301 https://$server_name$request_uri;
   }

   server {
       listen 443 ssl http2;
       server_name yourdomain.com www.yourdomain.com;

       ssl_certificate /etc/letsencrypt/live/yourdomain.com/fullchain.pem;
       ssl_certificate_key /etc/letsencrypt/live/yourdomain.com/privkey.pem;
       ssl_protocols TLSv1.2 TLSv1.3;
       ssl_prefer_server_ciphers on;
       ssl_ciphers ECDHE-RSA-AES256-GCM-SHA512:DHE-RSA-AES256-GCM-SHA512;

       # Frontend
       location / {
           proxy_pass http://frontend:80;
           proxy_set_header Host $host;
           proxy_set_header X-Real-IP $remote_addr;
           proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
           proxy_set_header X-Forwarded-Proto $scheme;
       }

       # Backend API
       location /api {
           proxy_pass http://backend:5000;
           proxy_set_header Host $host;
           proxy_set_header X-Real-IP $remote_addr;
           proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
           proxy_set_header X-Forwarded-Proto $scheme;
       }

       # SignalR WebSocket
       location /progressHub {
           proxy_pass http://backend:5000;
           proxy_http_version 1.1;
           proxy_set_header Upgrade $http_upgrade;
           proxy_set_header Connection "upgrade";
           proxy_set_header Host $host;
           proxy_cache_bypass $http_upgrade;
           proxy_set_header X-Real-IP $remote_addr;
           proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
           proxy_set_header X-Forwarded-Proto $scheme;
       }
   }
   ```

3. **Obtain Certificate**:
   ```bash
   sudo certbot --nginx -d yourdomain.com -d www.yourdomain.com
   ```

4. **Update Docker Compose**:
   ```yaml
   nginx:
     ports:
       - "80:80"
       - "443:443"
     volumes:
       - ./nginx-ssl.conf:/etc/nginx/nginx.conf:ro
       - /etc/letsencrypt:/etc/letsencrypt:ro
   ```

5. **Renew Certificates Automatically**:
   ```bash
   # Test renewal
   sudo certbot renew --dry-run

   # Certificates auto-renew via systemd timer
   sudo systemctl status certbot.timer
   ```

### Option 2: Reverse Proxy (Caddy, Traefik)

See respective documentation for automatic HTTPS setup.

---

## Database Management

### Backup Database

```bash
# Stop application (recommended for consistent backup)
docker-compose stop backend

# Backup database file
docker run --rm \
  -v immich-album-downloader_backend-data:/data \
  -v $(pwd)/backups:/backup \
  alpine \
  tar czf /backup/db-backup-$(date +%Y%m%d-%H%M%S).tar.gz -C /data .

# Restart application
docker-compose start backend
```

### Restore Database

```bash
# Stop application
docker-compose stop backend

# Restore database
docker run --rm \
  -v immich-album-downloader_backend-data:/data \
  -v $(pwd)/backups:/backup \
  alpine \
  sh -c "rm -rf /data/* && tar xzf /backup/db-backup-YYYYMMDD-HHMMSS.tar.gz -C /data"

# Restart application
docker-compose start backend
```

### Automated Backups

Create backup script (`scripts/backup.sh`):
```bash
#!/bin/bash
set -e

BACKUP_DIR="/opt/backups/immich-downloader"
RETENTION_DAYS=30

mkdir -p "$BACKUP_DIR"

# Backup database
docker run --rm \
  -v immich-album-downloader_backend-data:/data \
  -v $BACKUP_DIR:/backup \
  alpine \
  tar czf /backup/db-backup-$(date +%Y%m%d-%H%M%S).tar.gz -C /data .

# Remove old backups
find "$BACKUP_DIR" -name "db-backup-*.tar.gz" -mtime +$RETENTION_DAYS -delete

echo "Backup completed: $(date)"
```

Add to crontab:
```bash
# Edit crontab
crontab -e

# Add daily backup at 2 AM
0 2 * * * /opt/immich-album-downloader/scripts/backup.sh >> /var/log/immich-downloader-backup.log 2>&1
```

---

## Monitoring and Logging

### Application Logs

```bash
# View all logs
docker-compose logs -f

# View backend logs
docker-compose logs -f backend

# View last 100 lines
docker-compose logs --tail=100 backend

# View logs since timestamp
docker-compose logs --since="2024-01-15T10:00:00" backend
```

### Log Rotation

Configure Docker log rotation in `/etc/docker/daemon.json`:
```json
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3"
  }
}
```

Restart Docker:
```bash
sudo systemctl restart docker
docker-compose up -d
```

### Health Monitoring

```bash
# Check health endpoint
curl http://localhost:8082/api/health

# Check with detailed response
curl -v http://localhost:8082/api/health
```

### Resource Monitoring

```bash
# Monitor container resources
docker stats

# Monitor specific container
docker stats immich-downloader-backend
```

---

## Backup and Restore

### Complete System Backup

```bash
#!/bin/bash
# Full backup script

BACKUP_ROOT="/opt/backups/immich-downloader-full"
DATE=$(date +%Y%m%d-%H%M%S)
BACKUP_DIR="$BACKUP_ROOT/$DATE"

mkdir -p "$BACKUP_DIR"

# Stop services
docker-compose stop

# Backup data volume
docker run --rm \
  -v immich-album-downloader_backend-data:/data \
  -v "$BACKUP_DIR":/backup \
  alpine tar czf /backup/data.tar.gz -C /data .

# Backup .env file
cp .env "$BACKUP_DIR/env"

# Backup docker-compose.yml
cp docker-compose.yml "$BACKUP_DIR/"

# Backup nginx config
cp nginx.conf "$BACKUP_DIR/"

# Restart services
docker-compose start

echo "Full backup completed: $BACKUP_DIR"
```

### Disaster Recovery

```bash
# 1. Install Docker and Docker Compose
# 2. Clone repository or extract backup

cd /opt/immich-album-downloader

# 3. Restore .env file
cp backups/YYYYMMDD-HHMMSS/env .env

# 4. Restore data volume
docker-compose down -v
docker volume create immich-album-downloader_backend-data
docker run --rm \
  -v immich-album-downloader_backend-data:/data \
  -v $(pwd)/backups/YYYYMMDD-HHMMSS:/backup \
  alpine tar xzf /backup/data.tar.gz -C /data

# 5. Start services
docker-compose up -d
```

---

## Performance Tuning

### Docker Resource Limits

Adjust `docker-compose.yml`:
```yaml
services:
  backend:
    deploy:
      resources:
        limits:
          cpus: '4.0'          # Increase for heavy loads
          memory: 4G           # Increase for large albums
        reservations:
          cpus: '1.0'
          memory: 1G
```

### Application Configuration

Adjust chunking and concurrency in backend code if needed:
- `CHUNK_SIZE`: Images per chunk (default: 50)
- `MAX_CONCURRENT_DOWNLOADS`: Parallel downloads (default: 5)

### Database Optimization

SQLite is optimized by default. For heavy usage, consider:
- Regular `VACUUM` operations
- WAL mode (already enabled)
- Index optimization (already implemented)

---

## Troubleshooting

### Common Issues

#### 1. JWT Secret Key Error

**Error**: "JWT secret key must be configured"

**Solution**:
```bash
# Generate new key
openssl rand -base64 32

# Update .env
JWT_SECRET_KEY=<new-key>

# Restart
docker-compose restart backend
```

#### 2. Port Already in Use

**Error**: "port is already allocated"

**Solution**:
```bash
# Check what's using the port
sudo netstat -tulpn | grep :8082

# Change port in .env
EXPOSE_PORT=8083

# Restart
docker-compose down && docker-compose up -d
```

#### 3. CORS Errors

**Error**: "CORS policy: No 'Access-Control-Allow-Origin'"

**Solution**:
```bash
# Update .env with correct origin
ALLOWED_ORIGINS=https://yourdomain.com

# Restart backend
docker-compose restart backend
```

#### 4. SignalR Connection Failed

**Error**: "WebSocket connection failed"

**Solution**:
- Check nginx configuration for `/progressHub`
- Verify WebSocket upgrade headers
- Check firewall rules
- Verify JWT token is valid

#### 5. Out of Disk Space

**Solution**:
```bash
# Check disk usage
df -h

# Clean old downloads
docker-compose exec backend rm -rf /app/data/downloads/*

# Clean Docker
docker system prune -a
```

### Debug Mode

Enable detailed logging:
```yaml
# docker-compose.yml
services:
  backend:
    environment:
      - Logging__LogLevel__Default=Debug
      - Logging__LogLevel__Microsoft.AspNetCore=Debug
```

---

## Security Hardening

### 1. Strong JWT Secret

```bash
# Generate cryptographically secure key
openssl rand -base64 64
```

### 2. Limit Network Exposure

```yaml
# docker-compose.yml
services:
  backend:
    expose:
      - "5000"  # Don't use ports: for internal services
```

### 3. Non-Root User

Already implemented in Dockerfiles.

### 4. Update Dependencies

```bash
# Rebuild with latest dependencies
docker-compose build --no-cache --pull
```

### 5. Firewall Configuration

```bash
# UFW example
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow 22/tcp    # SSH
sudo ufw allow 80/tcp    # HTTP
sudo ufw allow 443/tcp   # HTTPS
sudo ufw enable
```

### 6. Security Headers

Already implemented via `SecurityHeadersMiddleware`.

### 7. Regular Updates

```bash
# Update application
cd /opt/immich-album-downloader
git pull
docker-compose build --no-cache
docker-compose up -d
```

---

## Maintenance

### Regular Tasks

#### Daily
- Monitor logs for errors
- Check disk space
- Verify backups completed

#### Weekly
- Review security logs
- Clean old downloads
- Check for application updates

#### Monthly
- Update dependencies
- Review backup retention
- Test disaster recovery
- Security audit

### Update Procedure

```bash
# 1. Backup current state
./scripts/backup.sh

# 2. Pull latest code
git pull

# 3. Review changes
git log

# 4. Rebuild images
docker-compose build --no-cache

# 5. Stop services
docker-compose down

# 6. Start services
docker-compose up -d

# 7. Verify deployment
curl http://localhost:8082/api/health
docker-compose logs -f
```

### Rollback Procedure

```bash
# 1. Stop services
docker-compose down

# 2. Restore previous code
git checkout <previous-commit-hash>

# 3. Rebuild images
docker-compose build

# 4. Restore database backup
# (See Backup and Restore section)

# 5. Start services
docker-compose up -d
```

---

**Document Version**: 1.0
**Last Updated**: 2025-11-06
**Status**: Complete
