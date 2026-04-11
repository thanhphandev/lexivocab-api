# LexiVocab API — Production Deployment Guide

> **Stack:** .NET 10 · PostgreSQL 16 · Redis 7 · Docker · AWS ECS / Self-hosted VPS
> **Mục tiêu:** Tài liệu này hướng dẫn triển khai LexiVocab API lên môi trường production theo tiêu chuẩn chuyên nghiệp — đảm bảo bảo mật, khả năng phục hồi và khả năng quan sát.

---

## Mục lục

1. [Kiến trúc tổng quan](#1-kiến-trúc-tổng-quan)
2. [Yêu cầu môi trường](#2-yêu-cầu-môi-trường)
3. [Chuẩn bị secrets & biến môi trường](#3-chuẩn-bị-secrets--biến-môi-trường)
4. [Build Docker image](#4-build-docker-image)
5. [Phương án A — Self-hosted VPS (Docker Compose)](#5-phương-án-a--self-hosted-vps-docker-compose)
6. [Phương án B — AWS ECS + ECR (CI/CD)](#6-phương-án-b--aws-ecs--ecr-cicd)
7. [Reverse Proxy & TLS (Nginx)](#7-reverse-proxy--tls-nginx)
8. [Database Migration & Seeding](#8-database-migration--seeding)
9. [Backup & Recovery](#9-backup--recovery)
10. [Health Checks & Monitoring](#10-health-checks--monitoring)
11. [Hangfire Dashboard](#11-hangfire-dashboard)
12. [Thanh toán (Sepay / PayPal / Stripe)](#12-thanh-toán-sepay--paypal--stripe)
13. [CI/CD Pipeline (GitHub Actions)](#13-cicd-pipeline-github-actions)
14. [Security Hardening Checklist](#14-security-hardening-checklist)
15. [Runbook — Xử lý sự cố thường gặp](#15-runbook--xử-lý-sự-cố-thường-gặp)
16. [Rollback Strategy](#16-rollback-strategy)

---

## 1. Kiến trúc tổng quan

```
Internet
    │
    ▼
[Cloudflare CDN / WAF]  ←── DDoS protection, caching tĩnh
    │
    ▼
[Nginx Reverse Proxy]   ←── TLS termination, rate-limit headers, gzip
    │
    ▼
[LexiVocab API]         ←── .NET 10, port 8080 (nội bộ)
    │          │
    ▼          ▼
[PostgreSQL] [Redis]    ←── Persistent storage & cache
    │
    ▼
[Cloudflare R2]         ←── Database backups (S3-compatible)
    │
    ▼
[SEQ / OpenTelemetry]   ←── Centralized log aggregation
```

### Các service trong stack

| Service         | Image                   | Role                        | Port (nội bộ) |
|-----------------|-------------------------|-----------------------------|---------------|
| `api`           | Custom (Dockerfile)     | ASP.NET Core API            | 8080          |
| `postgres`      | postgres:16-alpine      | Database chính              | 5432          |
| `redis`         | redis:7-alpine          | Cache & session store       | 6379          |
| `seq`           | datalust/seq            | Log aggregator (UI)         | 80 / 5341     |
| `pgadmin`       | dpage/pgadmin4          | DB management UI            | 80            |

> **Production note:** pgAdmin và SEQ **không nên** expose ra internet. Truy cập qua SSH tunnel hoặc private network.

---

## 2. Yêu cầu môi trường

### Máy chủ tối thiểu (VPS)

| Tài nguyên | Minimum    | Recommended  |
|------------|------------|--------------|
| CPU        | 2 vCPU     | 4 vCPU       |
| RAM        | 2 GB       | 4 GB         |
| Disk       | 20 GB SSD  | 50 GB SSD    |
| OS         | Ubuntu 22.04 LTS | Ubuntu 22.04 LTS |

### Phần mềm cần thiết trên server

```bash
# Docker & Docker Compose
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER

# AWS CLI (cho backup lên R2)
sudo apt install awscli -y

# Certbot (Let's Encrypt)
sudo apt install certbot python3-certbot-nginx -y
```

---

## 3. Chuẩn bị secrets & biến môi trường

### 3.1 Tạo file `.env.production`

> **Không commit file này lên git.** Thêm vào `.gitignore`.

Điền đầy đủ các giá trị sau (đã được cấu hình trong `.env.production` mẫu):

```dotenv
# ─── ASP.NET Core ─────────────────────────────────────────
ASPNETCORE_ENVIRONMENT=Production

# ─── Database (PostgreSQL) ────────────────────────────────
POSTGRES_HOST=postgres                    # Đặt là 'postgres' nếu dùng container, hoặc IP/Domain database ngoài
POSTGRES_DB=lexivocab_prod
POSTGRES_USER=lexivocab_app               # Không dùng 'postgres' superuser
POSTGRES_PASSWORD=<sinh_password_manh>
POSTGRES_PORT=5432
# ─── Redis ────────────────────────────────────────────────
REDIS_PASSWORD=<sinh_password_manh>

# ─── JWT ──────────────────────────────────────────────────
JWT_SECRET=<sinh_key_64_ky_tu>

# ─── App URL & CORS ───────────────────────────────────────
APP_URL=https://lexivocab.store
CORS_ORIGIN_0=https://lexivocab.store
CORS_ORIGIN_1=https://www.lexivocab.store
```

### 3.2 Sinh secret key an toàn

```bash
# JWT Secret (256-bit)
openssl rand -hex 32

# PostgreSQL password
openssl rand -hex 32

# Redis password
openssl rand -hex 24
```

### 3.3 Phân quyền file `.env.production`

```bash
chmod 600 .env.production
chown root:root .env.production
```

---

### 3.3 Linh hoạt Host & Cloud-Native (Railway, AWS, etc.)

API LexiVocab được thiết kế để triển khai linh hoạt trên mọi môi trường:

1.  **Docker Compose:** Sử dụng các biến `POSTGRES_HOST=postgres` và `REDIS_HOST=redis` để API kết nối trong mạng nội bộ Docker.
2.  **Railway / Render:** API tự động ưu tiên các biến môi trường nền tảng như `DATABASE_URL` và `REDIS_URL`. Nếu các biến này có giá trị (dạng URI: `postgres://...`), API sẽ tự xử lý và chuẩn hóa.
3.  **Managed DB (RDS):** Chỉ cần thay `POSTGRES_HOST` bằng địa chỉ instance DB của bạn.

---

## 4. Build Docker image

### Build locally và kiểm tra

```bash
# Build image production
docker build -t lexivocab-api:latest .

# Kiểm tra image size (nên ~80-120MB)
docker images lexivocab-api

# Chạy thử locally
docker run --rm -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="Host=host.docker.internal;..." \
  lexivocab-api:latest
```

### Multi-arch build (nếu server ARM)

```bash
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t lexivocab-api:latest \
  --push .
```

---

## 5. Phương án A — Self-hosted VPS (Docker Compose)

### 5.1 Tạo `docker-compose.prod.yml`

Tạo file mới chuyên dùng cho production, **tách biệt hoàn toàn** với file development:

```yaml
# docker-compose.prod.yml
services:

  postgres:
    image: postgres:16-alpine
    container_name: lexivocab-db
    restart: always
    environment:
      POSTGRES_DB: ${POSTGRES_DB}
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER} -d ${POSTGRES_DB}"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
    expose:
      - "5432"
    networks:
      - lexivocab-network

  redis:
    image: redis:7-alpine
    container_name: lexivocab-redis
    restart: always
    environment:
      - REDIS_PASSWORD=${REDIS_PASSWORD}
    command: sh -c 'if [ -n "$REDIS_PASSWORD" ]; then redis-server --requirepass "$REDIS_PASSWORD"; else redis-server; fi'
    volumes:
      - redisdata:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 5
    expose:
      - "6379"
    networks:
      - lexivocab-network

  api:
    image: ${API_IMAGE:-lexivocab-api:latest}
    container_name: lexivocab-api
    restart: always
    ports:
      - "${API_PORT:-5000}:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - OTEL_EXPORTER_OTLP_ENDPOINT=http://seq:5341/ingest/otlp/v1/logs
      
      - ConnectionStrings__DefaultConnection=Host=${POSTGRES_HOST:-postgres};Port=${POSTGRES_PORT:-5432};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};Maximum Pool Size=100;Pooling=true
      - ConnectionStrings__Redis=${REDIS_HOST:-redis}:6379,password=${REDIS_PASSWORD},ssl=false,abortConnect=false
      
      - Jwt__Secret=${JWT_SECRET}
      - Google__ClientId=${GOOGLE_CLIENT_ID}
      - Google__ClientSecret=${GOOGLE_CLIENT_SECRET}
      - Smtp__Server=${SMTP_HOST}
      - Smtp__Port=${SMTP_PORT:-587}
      - Smtp__Username=${SMTP_USER}
      - Smtp__Password=${SMTP_PASSWORD}
      - Sepay__ApiKey=${SEPAY_API_KEY}
      - PayPal__ClientId=${PAYPAL_CLIENT_ID}
      - PayPal__ClientSecret=${PAYPAL_CLIENT_SECRET}
      - App__Url=${APP_URL}
      - ENCRYPTION_KEY=${ENCRYPTION_KEY}
      - ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
      seq:
        condition: service_started
    networks:
      - lexivocab-network
    deploy:
      resources:
        limits:
          memory: 1G
          cpus: '2.0'

  seq:
    image: datalust/seq:latest
    container_name: lexivocab-seq
    restart: always
    environment:
      - ACCEPT_EULA=Y
      - SEQ_FIRSTRUN_ADMINPASSWORD=${SEQ_ADMIN_PASSWORD}
    ports:
      - "5341:5341"
      - "8899:80"
    networks:
      - lexivocab-network

volumes:
  pgdata:
  redisdata:

networks:
  lexivocab-network:
    driver: bridge
```

### 5.2 Deploy lần đầu

```bash
# 1. Clone repository (trên server)
git clone https://github.com/your-org/LexiVocabAPI.git /opt/lexivocab
cd /opt/lexivocab

# 2. Copy file môi trường
cp /secure/location/.env.production .env

# 3. Pull image (hoặc build tại server)
docker compose -f docker-compose.prod.yml pull
# Hoặc build: docker compose -f docker-compose.prod.yml build

# 4. Khởi động stack (nền)
docker compose -f docker-compose.prod.yml --env-file .env up -d

# 5. Kiểm tra trạng thái
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs api --follow
```

### 5.3 Update image khi có phiên bản mới

```bash
# 1. Pull image mới
docker pull lexivocab-api:latest

# 2. Rolling restart (zero-downtime với single node)
docker compose -f docker-compose.prod.yml up -d --no-deps api

# 3. Kiểm tra health
curl -s http://localhost:8080/health | jq .
```

---


## 7. Phương án C — Azure Container Apps

Kiến trúc này sử dụng Azure Container Apps (serverless containers), PostgreSQL Flexible Server, và Azure Cache for Redis. Phương án này tối ưu chi phí (scale-to-zero) và dễ triển khai nhất.

### 7.1 Chuẩn bị môi trường Azure (Dùng Azure CLI)

```bash
# 1. Đăng nhập Azure
az login

# 2. Tạo Resource Group
az group create --name rg-lexivocab --location southeastasia

# 3. Tạo Azure Container Registry (ACR)
az acr create --resource-group rg-lexivocab --name lexivocabacr --sku Basic --admin-enabled true

# 4. Tạo PostgreSQL Flexible Server (Burstable B1ms - đủ cho thesis demo)
az postgres flexible-server create \
  --resource-group rg-lexivocab \
  --name lexivocab-pg \
  --location southeastasia \
  --admin-user lexivocab_admin \
  --admin-password "<STRONG_PASSWORD>" \
  --sku-name Standard_B1ms \
  --tier Burstable \
  --storage-size 32 \
  --version 16 \
  --public-access 0.0.0.0 \
  --yes

# 5. Tạo Database
az postgres flexible-server db create \
  --resource-group rg-lexivocab \
  --server-name lexivocab-pg \
  --database-name lexivocab_prod

# 6. Tạo Azure Cache for Redis
az redis create \
  --resource-group rg-lexivocab \
  --name lexivocab-redis \
  --location southeastasia \
  --sku Basic \
  --vm-size c0 \
  --redis-version 6

# 7. Tạo Container Apps Environment
az containerapp env create \
  --name lexivocab-env \
  --resource-group rg-lexivocab \
  --location southeastasia
```

### 7.2 Build và Push Docker Image lên ACR

Thay vì build ở local, bạn có thể build trực tiếp trên cloud environment của ACR:

```bash
# Chạy ở thư mục chứa Dockerfile của project
az acr build \
  --registry lexivocabacr \
  --image lexivocab-api:latest \
  --image lexivocab-api:v1.0.0 \
  --file Dockerfile \
  .
```

### 7.3 Deploy Azure Container Apps lần đầu tiên

> Chú ý: Ở lần đầu deploy, ta bật `RUN_MIGRATIONS=true` để tự động tạo schema + seed database. Nhớ đổi thành `<...>` bằng các secrets thật từ file `.env.production`.

```bash
# Lấy Password của PostgreSQL (Thiết lập ở bước 4)
PG_HOST="lexivocab-pg.postgres.database.azure.com"
PG_PASSWORD="<STRONG_PASSWORD>"

# Lấy Primary Key của Redis bằng lệnh:
# az redis list-keys --name lexivocab-redis --resource-group rg-lexivocab --query primaryKey -o tsv
REDIS_HOST="lexivocab-redis.redis.cache.windows.net"
REDIS_KEY="<PRIMARY_KEY>"

# Deploy App
az containerapp create \
  --name lexivocab-api \
  --resource-group rg-lexivocab \
  --environment lexivocab-env \
  --image lexivocabacr.azurecr.io/lexivocab-api:latest \
  --registry-server lexivocabacr.azurecr.io \
  --target-port 8080 \
  --ingress external \
  --min-replicas 0 \
  --max-replicas 3 \
  --cpu 1 \
  --memory 2Gi \
  --env-vars \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    RUN_MIGRATIONS=true \
    "ConnectionStrings__DefaultConnection=Host=$PG_HOST;Port=5432;Database=lexivocab_prod;Username=lexivocab_admin;Password=$PG_PASSWORD;SslMode=Require;Trust Server Certificate=true;Maximum Pool Size=100;Pooling=true" \
    "ConnectionStrings__Redis=$REDIS_HOST:6380,password=$REDIS_KEY,ssl=True,abortConnect=False" \
    "Jwt__Secret=<JWT_SECRET_TU_ENV_PRODUCTION>" \
    "Jwt__Issuer=LexiVocab.API" \
    "Jwt__Audience=LexiVocab.Clients" \
    "Jwt__AccessTokenExpiryMinutes=120" \
    "Jwt__RefreshTokenExpiryDays=30" \
    "Jwt__RefreshTokenGracePeriodSeconds=60" \
    "Jwt__ClockSkewSeconds=0" \
    "Google__ClientId=<GOOGLE_CLIENT_ID>" \
    "Google__ClientSecret=<GOOGLE_CLIENT_SECRET>" \
    "App__Url=https://lexivocab.store" \
    "ENCRYPTION_KEY=<ENCRYPTION_KEY>" \
    "ASPNETCORE_FORWARDEDHEADERS_ENABLED=true"
```

### 7.4 Tắt Migration để tránh làm chậm startup time các lần sau

```bash
az containerapp update \
  --name lexivocab-api \
  --resource-group rg-lexivocab \
  --set-env-vars "RUN_MIGRATIONS=false"
```

### 7.5 CI/CD với GitHub Actions (Azure Container Apps)

Pipeline OIDC đã có sẵn tại `.github/workflows/cd.yml`.
Bạn chỉ cần lấy credentials cho Azure (Federated credentials) và tạo các secrets trong repo:

- `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
- `ACR_NAME`, `CONTAINER_APP_NAME`, `AZURE_RESOURCE_GROUP`

Mỗi khi code được merge vào `main`, action sẽ tự động trigger, build docker image và deploy revision mới (scale-to-zero friendly).

---

## 7. Reverse Proxy & TLS (Nginx)

### 7.1 Cài đặt Nginx

```bash
sudo apt update && sudo apt install nginx -y
```

### 7.2 Cấu hình Nginx cho LexiVocab API

```nginx
# /etc/nginx/sites-available/lexivocab-api
upstream lexivocab_api {
    server 127.0.0.1:5000;    # Docker expose port 5000 → container 8080
    keepalive 32;
}

# Redirect HTTP → HTTPS
server {
    listen 80;
    server_name api.yourdomain.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name api.yourdomain.com;

    # ─── TLS ──────────────────────────────────────────────────
    ssl_certificate     /etc/letsencrypt/live/api.yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/api.yourdomain.com/privkey.pem;
    ssl_protocols       TLSv1.2 TLSv1.3;
    ssl_ciphers         ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384;
    ssl_prefer_server_ciphers off;
    ssl_session_cache   shared:SSL:10m;
    ssl_session_timeout 1d;

    # HSTS (2 năm, bao gồm subdomains)
    add_header Strict-Transport-Security "max-age=63072000; includeSubDomains; preload" always;

    # ─── Security Headers ─────────────────────────────────────
    add_header X-Content-Type-Options    "nosniff"          always;
    add_header X-Frame-Options           "DENY"             always;
    add_header Referrer-Policy           "strict-origin"    always;
    add_header Permissions-Policy        "geolocation=(), microphone=()" always;

    # ─── Proxy Settings ───────────────────────────────────────
    location / {
        proxy_pass          http://lexivocab_api;
        proxy_http_version  1.1;
        proxy_set_header    Upgrade         $http_upgrade;
        proxy_set_header    Connection      "upgrade";
        proxy_set_header    Host            $host;
        proxy_set_header    X-Real-IP       $remote_addr;
        proxy_set_header    X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header    X-Forwarded-Proto $scheme;

        proxy_connect_timeout 30s;
        proxy_send_timeout    60s;
        proxy_read_timeout    60s;

        # Timeouts cho long-polling (Hangfire, SSE)
        proxy_buffering off;
    }

    # ─── Health check (không log) ─────────────────────────────
    location = /health {
        proxy_pass http://lexivocab_api;
        access_log off;
    }

    # ─── Rate Limiting (bổ sung tầng Nginx) ──────────────────
    # Đã có rate limit trong .NET, Nginx chỉ làm tầng phụ
    limit_req_zone $binary_remote_addr zone=api:10m rate=100r/m;
    limit_req zone=api burst=20 nodelay;

    # ─── Gzip ─────────────────────────────────────────────────
    gzip on;
    gzip_types application/json text/plain;
    gzip_min_length 1024;

    # ─── Upload size ──────────────────────────────────────────
    client_max_body_size 10M;    # Khớp với Kestrel config

    # ─── Logging ──────────────────────────────────────────────
    access_log /var/log/nginx/lexivocab.access.log;
    error_log  /var/log/nginx/lexivocab.error.log warn;
}
```

### 7.3 Enable và test config

```bash
sudo nginx -t
sudo ln -s /etc/nginx/sites-available/lexivocab-api /etc/nginx/sites-enabled/
sudo systemctl reload nginx
```

### 7.4 Cấp SSL bằng Let's Encrypt

```bash
sudo certbot --nginx -d api.yourdomain.com --email admin@yourdomain.com --agree-tos
# Auto-renew đã được cấu hình bởi certbot
sudo systemctl status certbot.timer
```

---

## 8. Database Migration & Seeding

### 8.1 Production migration (thủ công & kiểm soát)

> **Quan trọng:** Trong production, `ASPNETCORE_ENVIRONMENT=Production` → auto-migrate bị tắt (chỉ chạy trong Development). Migration phải chạy **thủ công** hoặc qua CI/CD trước khi deploy.

```bash
# Cách 1: Chạy migration từ container đang chạy
docker exec -it lexivocab-api dotnet ef database update \
  --connection "Host=postgres;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"

# Cách 2: Chạy migration script riêng (khuyến nghị cho production)
docker run --rm \
  --network lexivocab-internal \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}" \
  lexivocab-api:latest \
  dotnet ef database update

# Cách 3: Dùng PowerShell script có sẵn
./migrate.ps1
```

### 8.2 Xem trạng thái migration

```bash
docker exec -it lexivocab-api dotnet ef migrations list
```

### 8.3 Tạo migration mới (trên máy dev)

```bash
dotnet ef migrations add <MigrationName> \
  --project src/LexiVocab.Infrastructure \
  --startup-project src/LexiVocab.API
```

### 8.4 Script migration trong CI/CD

Thêm bước sau vào `cd.yml` **trước** bước `ecs update-service`:

```yaml
- name: Run database migrations
  run: |
    docker run --rm \
      -e ConnectionStrings__DefaultConnection="${{ secrets.DB_CONNECTION_STRING }}" \
      ${{ steps.login-ecr.outputs.registry }}/lexivocab-prod-api:${{ github.sha }} \
      dotnet ef database update --no-build
```

---

## 9. Backup & Recovery

### 9.1 Cấu hình Cloudflare R2

```bash
# Cấu hình aws CLI trỏ tới R2
aws configure --profile r2
# Access Key ID: <từ Cloudflare R2 API Token>
# Secret Access Key: <từ Cloudflare R2 API Token>
# Default region name: auto
# Default output format: json

# Lưu endpoint vào config
aws configure set endpoint_url https://<ACCOUNT_ID>.r2.cloudflarestorage.com --profile r2
```

### 9.2 Cập nhật `scripts/backup_to_r2.sh` cho production

```bash
# Sửa các giá trị này trong file scripts/backup_to_r2.sh:
DB_NAME="lexivocab_prod"                                          # DB production
R2_ENDPOINT="https://<YOUR_CLOUDFLARE_ACCOUNT_ID>.r2.cloudflarestorage.com"
```

### 9.3 Lên lịch backup tự động (cron)

```bash
# Chạy backup lúc 2:00 AM mỗi ngày
crontab -e

# Thêm dòng:
0 2 * * * /opt/lexivocab/scripts/backup_to_r2.sh >> /var/log/lexivocab-backup.log 2>&1
```

### 9.4 Restore từ backup

```bash
# Download backup từ R2
aws s3 cp s3://lexivocab-backups/db-backups/2025/03/lexivocab_20250329_020000.sql.gz . \
  --endpoint-url https://<ACCOUNT_ID>.r2.cloudflarestorage.com

# Restore vào container
gunzip -c lexivocab_20250329_020000.sql.gz | \
  docker exec -i lexivocab-db pg_restore \
    -U postgres -d lexivocab_prod \
    --clean --if-exists

# Kiểm tra
docker exec -it lexivocab-db psql -U postgres -d lexivocab_prod -c "\dt"
```

### 9.5 Kiểm tra backup định kỳ (restore drill)

```bash
# Chạy monthly: restore vào DB tạm thời và kiểm tra
docker exec -it lexivocab-db psql -U postgres -c "CREATE DATABASE lexivocab_restore_test;"
# ... restore và verify ...
docker exec -it lexivocab-db psql -U postgres -c "DROP DATABASE lexivocab_restore_test;"
```

---

## 10. Health Checks & Monitoring

### 10.1 Health check endpoint

```bash
# Kiểm tra nhanh
curl -s https://api.yourdomain.com/health | jq .

# Response mẫu khi healthy:
{
  "status": "Healthy",
  "timestamp": "2025-03-29T02:00:00Z",
  "version": "1.0.0",
  "checks": [
    { "name": "npgsql", "status": "Healthy" },
    { "name": "redis",  "status": "Healthy" }
  ]
}
```

### 10.2 Uptime monitoring (khuyến nghị)

Dùng một trong các dịch vụ sau để ping `/health` mỗi 1-5 phút:

- **Better Uptime** — free tier, cảnh báo email/SMS
- **UptimeRobot** — free tier, 5 phút interval
- **Freshping** — 1 phút interval

Cấu hình cảnh báo khi `status != "Healthy"`.

### 10.3 SEQ — Centralized Logging

Truy cập SEQ UI (qua SSH tunnel):

```bash
# Tạo SSH tunnel từ máy local
ssh -L 8081:localhost:8081 user@your-server

# Truy cập: http://localhost:8081
```

Các query hữu ích trong SEQ:

```sql
-- Lỗi trong 1 giờ qua
@Level = 'Error' AND @Timestamp > Now() - Duration('1:00:00')

-- Request chậm (> 500ms)
RequestDuration > 500

-- Lỗi thanh toán
SourceContext like '%Payment%' AND @Level = 'Error'

-- Authentication failures
SourceContext like '%Auth%' AND @Level = 'Warning'
```

### 10.4 Container metrics

```bash
# Theo dõi real-time resource usage
docker stats lexivocab-api lexivocab-db lexivocab-redis

# Kiểm tra logs
docker compose -f docker-compose.prod.yml logs api --since 1h --follow
docker compose -f docker-compose.prod.yml logs postgres --since 1h
```

---

## 11. Hangfire Dashboard

### 11.1 Truy cập Hangfire

URL: `https://api.yourdomain.com/hangfire`

- **Production**: Yêu cầu đăng nhập với tài khoản có role `Admin`
- **Development**: Cho phép tất cả truy cập

### 11.2 Scheduled Jobs

| Job                           | Lịch           | Mô tả                                       |
|-------------------------------|----------------|---------------------------------------------|
| `SubscriptionExpirationJob`   | Daily 00:00 UTC| Xử lý subscription hết hạn                  |
| `ReviewReminderJob`           | Daily 01:00 UTC| Gửi nhắc nhở ôn tập từ vựng                 |
| `PendingPaymentCleanupJob`    | Mỗi phút       | Hủy giao dịch pending quá hạn               |

### 11.3 Kiểm tra job thất bại

```bash
# Xem job failed trong Hangfire UI
# Hoặc query trực tiếp DB
docker exec -it lexivocab-db psql -U postgres -d lexivocab_prod \
  -c "SELECT * FROM \"HangFire\".\"Job\" WHERE \"StateName\" = 'Failed' ORDER BY \"CreatedAt\" DESC LIMIT 10;"
```

---

## 12. Thanh toán (Sepay / PayPal / Stripe)

### 12.1 SePay Webhook

SePay gọi webhook khi có giao dịch thành công. Production cần URL thật (không phải localhost):

```
POST https://api.yourdomain.com/api/v1/payments/sepay/webhook
```

Cấu hình trong tài khoản SePay:
1. Đăng nhập `my.sepay.vn`
2. Vào **Cài đặt → Webhook**
3. Nhập URL: `https://api.yourdomain.com/api/v1/payments/sepay/webhook`
4. Lưu API Key vào `SEPAY_API_KEY`

### 12.2 Stripe Webhook

```bash
# Lấy webhook secret sau khi tạo endpoint trên Stripe Dashboard
stripe listen --forward-to https://api.yourdomain.com/api/v1/payments/stripe/webhook

# Hoặc CLI để verify
stripe webhooks resend <event_id>
```

### 12.3 PayPal IPN/Webhook

- Production: Đổi `PAYPAL_MODE=live`
- Sandbox testing: `PAYPAL_MODE=sandbox`

### 12.4 Kiểm tra thanh toán

```bash
# Xem giao dịch gần nhất
docker exec -it lexivocab-db psql -U postgres -d lexivocab_prod \
  -c "SELECT id, amount, status, created_at FROM transactions ORDER BY created_at DESC LIMIT 20;"
```

---

## 13. CI/CD Pipeline (GitHub Actions)

### 13.1 Luồng CI (ci.yml)

```
Pull Request / Push to main|develop
    │
    ├─ Spin up PostgreSQL + Redis services
    ├─ dotnet restore
    ├─ dotnet build --configuration Release
    ├─ dotnet test (integration tests với real DB)
    └─ dotnet list package --vulnerable (SCA scan)
```

**Bảo vệ branch `main`:**
1. Vào `Settings → Branches → Add rule`
2. Branch name: `main`
3. Bật: *Require status checks to pass*, chọn job `build-and-test`
4. Bật: *Require pull request reviews before merging*

### 13.2 Luồng CD (cd.yml)

```
Push to main (sau khi CI pass)
    │
    ├─ AWS OIDC authentication
    ├─ Login to ECR
    ├─ docker build -t <ecr>:<sha> .
    ├─ docker push
    └─ aws ecs update-service --force-new-deployment
```

### 13.3 Thêm Database Migration vào CD

```yaml
# Thêm vào cd.yml sau bước push image:
- name: Run Database Migrations
  run: |
    aws ecs run-task \
      --cluster ${{ env.ECS_CLUSTER }} \
      --task-definition lexivocab-migration \
      --overrides '{"containerOverrides":[{"name":"api","command":["dotnet","ef","database","update","--no-build"]}]}' \
      --wait-for-task-stop
```

### 13.4 Environment protection rules

Trong GitHub:
1. `Settings → Environments → New environment: production`
2. Thêm **Required reviewers** (ít nhất 1 người)
3. Thêm **Wait timer: 5 phút** (thời gian để cancel nếu cần)

---

## 14. Security Hardening Checklist

### Application

- [ ] `ASPNETCORE_ENVIRONMENT=Production` (tắt Swagger, auto-migrate, debug info)
- [ ] JWT Secret ≥ 64 ký tự, sinh ngẫu nhiên
- [ ] CORS chỉ cho phép domain cụ thể (không dùng `SetIsOriginAllowed(_ => true)` trong prod)
- [ ] Rate limiting đã bật: Global (100/min), Auth (5/min), Sensitive (10/min)
- [ ] `SecurityHeadersMiddleware` đang chạy
- [ ] HSTS header bật (`app.UseHsts()` trong Production)
- [ ] Kestrel: `AddServerHeader = false`, `MaxRequestBodySize = 10MB`
- [ ] Hangfire Dashboard yêu cầu Admin role

### Infrastructure

- [ ] PostgreSQL không expose port ra ngoài
- [ ] Redis không expose port ra ngoài, có password
- [ ] pgAdmin/SEQ chỉ truy cập qua SSH tunnel
- [ ] File `.env.production` có permission `600`
- [ ] Docker không chạy với `root` user (thêm `user: "1000:1000"` vào service)
- [ ] Nginx TLS: chỉ TLS 1.2/1.3, cipher mạnh
- [ ] Firewall: chỉ mở port 80, 443 và 22 (SSH)

### Secrets

- [ ] Không có secret nào trong source code
- [ ] Không có secret nào trong Docker image layers
- [ ] GitHub Secrets đã cấu hình đầy đủ
- [ ] Production credentials tách biệt hoàn toàn với development
- [ ] Ngrok token **không** dùng trong production (chỉ dev/webhook testing)
- [ ] `.env` file trong `.gitignore`

### Monitoring

- [ ] Health check `/health` đang phản hồi
- [ ] Uptime monitor đã cấu hình
- [ ] Backup tự động đã lên lịch và đã test restore
- [ ] SEQ/log aggregation đang nhận logs

---

## 15. Runbook — Xử lý sự cố thường gặp

### 15.1 API không khởi động được

```bash
# Xem logs chi tiết
docker logs lexivocab-api --tail 100

# Kiểm tra biến môi trường
docker inspect lexivocab-api | jq '.[0].Config.Env'

# Kiểm tra kết nối DB
docker exec lexivocab-api ping postgres

# Khởi động lại
docker compose -f docker-compose.prod.yml restart api
```

### 15.2 Database connection failed

```bash
# Kiểm tra PostgreSQL health
docker exec lexivocab-db pg_isready -U postgres

# Test kết nối từ API container
docker exec lexivocab-api sh -c "apt-get install -y postgresql-client && psql -h postgres -U postgres -d lexivocab_prod -c 'SELECT 1'"

# Xem PostgreSQL logs
docker logs lexivocab-db --tail 50
```

### 15.3 Redis connection failed

```bash
# Ping Redis
docker exec lexivocab-redis redis-cli -a $REDIS_PASSWORD ping

# Kiểm tra memory
docker exec lexivocab-redis redis-cli -a $REDIS_PASSWORD info memory

# Xóa cache (nếu cần)
docker exec lexivocab-redis redis-cli -a $REDIS_PASSWORD FLUSHALL
```

### 15.4 Memory leak / CPU spike

```bash
# Xem resource usage
docker stats

# Xem top processes trong container
docker exec lexivocab-api top

# Tạo memory dump để phân tích
docker exec lexivocab-api sh -c "dotnet-dump collect -p 1 -o /app/dump.dmp"
docker cp lexivocab-api:/app/dump.dmp ./dump.dmp
```

### 15.5 Migration thất bại

```bash
# Xem lịch sử migration
docker exec lexivocab-api dotnet ef migrations list

# Rollback về migration trước
docker exec lexivocab-api dotnet ef database update <PreviousMigrationName>

# Kiểm tra __EFMigrationsHistory
docker exec lexivocab-db psql -U postgres -d lexivocab_prod \
  -c 'SELECT * FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 5;'
```

### 15.6 Webhook payment không nhận được

```bash
# Kiểm tra Nginx logs
sudo tail -f /var/log/nginx/lexivocab.access.log | grep webhook

# Kiểm tra API logs cho payment
docker logs lexivocab-api 2>&1 | grep -i "payment\|webhook\|sepay" | tail -50

# Test webhook thủ công
curl -X POST https://api.yourdomain.com/api/v1/payments/sepay/webhook \
  -H "Content-Type: application/json" \
  -d '{"test": true}'
```

---

## 16. Rollback Strategy

### 16.1 Rollback Docker image (VPS)

```bash
# Xem danh sách images có sẵn
docker images lexivocab-api

# Tag image cũ và restart
docker tag lexivocab-api:<previous-sha> lexivocab-api:latest
docker compose -f docker-compose.prod.yml up -d --no-deps api
```

### 16.2 Rollback ECS task

```bash
# Lấy danh sách task definitions cũ
aws ecs list-task-definitions \
  --family-prefix lexivocab-prod-api \
  --sort DESC \
  --query 'taskDefinitionArns[0:5]'

# Update service về task definition cũ
aws ecs update-service \
  --cluster lexivocab-prod-cluster \
  --service lexivocab-prod-cluster-api \
  --task-definition lexivocab-prod-api:<previous-revision>
```

### 16.3 Rollback Database migration

```bash
# Tạo backup TRƯỚC KHI rollback
./scripts/backup_to_r2.sh

# Rollback về migration trước
docker exec lexivocab-api dotnet ef database update <TargetMigrationName>
```

### 16.4 Quy trình rollback chuẩn (5 phút)

```
1. Phát hiện sự cố → Thông báo team
2. Quyết định rollback (< 2 phút sau phát hiện)
3. Backup DB ngay lập tức
4. Rollback image → version trước
5. Verify /health endpoint
6. Thông báo hệ thống ổn định
7. Post-mortem trong 24h
```

---

## Phụ lục — Lệnh thường dùng

```bash
# ─── Quản lý stack ────────────────────────────────────────────────
docker compose -f docker-compose.prod.yml up -d          # Khởi động
docker compose -f docker-compose.prod.yml down            # Dừng (giữ volumes)
docker compose -f docker-compose.prod.yml down -v         # Dừng + xóa volumes (NGUY HIỂM)
docker compose -f docker-compose.prod.yml ps              # Trạng thái
docker compose -f docker-compose.prod.yml logs api -f     # Theo dõi logs

# ─── Database ─────────────────────────────────────────────────────
docker exec -it lexivocab-db psql -U postgres -d lexivocab_prod   # PSQL shell
./scripts/backup_to_r2.sh                                          # Manual backup

# ─── Reload config không restart ─────────────────────────────────
docker compose -f docker-compose.prod.yml up -d --no-deps api      # Update chỉ API

# ─── Kiểm tra sức khỏe hệ thống ──────────────────────────────────
curl -s https://api.yourdomain.com/health | jq .
docker stats --no-stream
df -h && free -h                                                   # Disk & RAM server
```

---

> **Tác giả:** LexiVocab Engineering Team
> **Cập nhật lần cuối:** 2025-03-29
> **Phiên bản tài liệu:** 1.0.0
