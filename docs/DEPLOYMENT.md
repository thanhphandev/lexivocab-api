# 🚀 Deployment Guide

> Docker, CI/CD, cloud deployment, environment variables, monitoring.

---

## 1. Local Development

### Prerequisites

- .NET 10 SDK
- Docker Desktop (for PostgreSQL, Redis, SEQ)
- IDE: Rider / VS Code / Visual Studio

### Quick Start

```bash
# 1. Start infrastructure
docker compose up -d postgres redis seq

# 2. Run API
cd src/LexiVocab.API
dotnet run

# API: https://localhost:5001
# Scalar Docs: https://localhost:5001/scalar
# Hangfire: https://localhost:5001/hangfire
# SEQ Logs: http://localhost:5341
# pgAdmin: http://localhost:8080 (profile: tools)
```

### Docker Compose Services

| Service | Image | Port | Mô tả |
|---------|-------|------|-------|
| `postgres` | postgres:16-alpine | 5432 | Database chính |
| `redis` | redis:7-alpine | 6379 | Cache + Rate limiting |
| `seq` | datalust/seq | 5341 / 5342 | Log aggregation (OpenTelemetry) |
| `pgadmin` | dpage/pgadmin4 | 8080 | DB GUI (optional, `--profile tools`) |
| `api` | Custom Dockerfile | 8080 | Application |

### Database Migrations

```bash
# Tạo migration mới
dotnet ef migrations add <MigrationName> \
    --project src/LexiVocab.Infrastructure \
    --startup-project src/LexiVocab.API

# Áp dụng migrations
dotnet ef database update \
    --project src/LexiVocab.Infrastructure \
    --startup-project src/LexiVocab.API

# Development: migrations tự động apply khi startup (Program.cs)
# Production: PHẢI chạy migrations trước khi deploy
```

---

## 2. Docker Build

### Dockerfile (Multi-stage Alpine)

```dockerfile
# Stage 1: Build (~1.5GB, discarded)
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
COPY *.slnx .
COPY src/*/*.csproj src/*/    # Layer caching: restore chỉ chạy khi csproj thay đổi
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /out --no-restore

# Stage 2: Runtime (~80MB final image)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
RUN apk add --no-cache icu-libs krb5-libs    # ICU cho globalization + Kerberos cho PostgreSQL
COPY --from=build /out .
EXPOSE 8080
ENTRYPOINT ["dotnet", "LexiVocab.API.dll"]
```

### Build & Run

```bash
# Build image
docker build -t lexivocab-api:latest .

# Run container
docker run -p 8080:8080 \
    -e ConnectionStrings__DefaultConnection="Host=..." \
    -e Jwt__Secret="..." \
    lexivocab-api:latest

# Hoặc dùng docker compose
docker compose up -d
```

---

## 3. Environment Variables (Production)

### Bắt buộc

| Variable | Mô tả | Ví dụ |
|----------|-------|-------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | `Host=db;Port=5432;Database=lexivocab;Username=app;Password=xxx` |
| `ConnectionStrings__Redis` | Redis connection string | `redis:6379,password=xxx` |
| `Jwt__Secret` | JWT signing key (≥32 chars) | `YourSuperSecretKeyAtLeast32Characters!` |
| `Jwt__Issuer` | JWT issuer | `LexiVocab.API` |
| `Jwt__Audience` | JWT audience | `LexiVocab.Clients` |

### Payment (PayPal)

| Variable | Mô tả |
|----------|-------|
| `PayPal__BaseUrl` | `https://api-m.paypal.com` (production) hoặc `https://api-m.sandbox.paypal.com` |
| `PayPal__ClientId` | PayPal app client ID |
| `PayPal__ClientSecret` | PayPal app secret |
| `PayPal__WebhookId` | PayPal webhook ID (bắt buộc production) |
| `PayPal__ReturnUrl` | Frontend success URL |
| `PayPal__CancelUrl` | Frontend cancel URL |

### Email

| Variable | Mô tả |
|----------|-------|
| `Smtp__Server` | SMTP server | 
| `Smtp__Port` | SMTP port (587 for TLS) |
| `Smtp__Username` | SMTP username |
| `Smtp__Password` | SMTP password / app password |
| `Smtp__SenderName` | Display name |
| `Smtp__SenderEmail` | From email |

### Tùy chọn

| Variable | Default | Mô tả |
|----------|---------|-------|
| `App__Url` | `https://lexivocab.store` | Frontend URL cho email templates |
| `Google__ClientId` | - | Google OAuth client ID |
| `Cors__AllowedOrigins__0` | `http://localhost:3000` | CORS origins |
| `FreePlan__MaxVocabularies` | 50 | Free tier vocab limit |

---

## 4. Cloud Deployment

> 📌 **Terraform IaC**: Xem [TERRAFORM.md](TERRAFORM.md) cho hướng dẫn chi tiết provisioning infra bằng Terraform (AWS modules, CI/CD, cost estimates).

### Option A: Azure App Service

```bash
# 1. Tạo Azure resources
az group create --name lexivocab-rg --location southeastasia
az appservice plan create --name lexivocab-plan --resource-group lexivocab-rg --sku B1 --is-linux
az webapp create --name lexivocab-api --resource-group lexivocab-rg --plan lexivocab-plan --runtime "DOTNET|10.0"

# 2. Tạo PostgreSQL
az postgres flexible-server create --name lexivocab-db --resource-group lexivocab-rg --sku-name Standard_B1ms

# 3. Tạo Redis
az redis create --name lexivocab-cache --resource-group lexivocab-rg --sku Basic --vm-size c0

# 4. Cấu hình environment
az webapp config appsettings set --name lexivocab-api --resource-group lexivocab-rg --settings \
    ConnectionStrings__DefaultConnection="Host=...;..." \
    Jwt__Secret="..." \
    ...

# 5. Deploy
az webapp deploy --name lexivocab-api --src-path ./publish.zip --type zip
```

### Option B: AWS ECS (Fargate)

```bash
# 1. Push Docker image to ECR
aws ecr create-repository --repository-name lexivocab-api
docker tag lexivocab-api:latest <account>.dkr.ecr.<region>.amazonaws.com/lexivocab-api:latest
docker push <account>.dkr.ecr.<region>.amazonaws.com/lexivocab-api:latest

# 2. Create ECS Cluster + Service + Task Definition
# (Dùng aws-cdk hoặc Terraform)

# 3. Managed services:
# - RDS PostgreSQL (Multi-AZ)
# - ElastiCache Redis
# - CloudWatch Logs
# - ALB + HTTPS certificate
```

### Option C: DigitalOcean App Platform

```yaml
# .do/app.yaml
spec:
  name: lexivocab-api
  services:
    - name: api
      dockerfile_path: Dockerfile
      http_port: 8080
      instance_count: 1
      instance_size_slug: basic-xs
      envs:
        - key: ConnectionStrings__DefaultConnection
          value: ${db.DATABASE_URL}
  databases:
    - engine: PG
      name: db
      version: "16"
```

### Option D: Docker Compose (VPS)

```bash
# Trên VPS (Ubuntu 22.04)
# 1. Install Docker
# 2. Clone repo
# 3. Copy docker-compose.yml + .env
# 4. docker compose up -d
# 5. Setup Nginx reverse proxy + Let's Encrypt SSL

# Nginx config
server {
    listen 443 ssl;
    server_name api.lexivocab.store;
    
    ssl_certificate /etc/letsencrypt/live/api.lexivocab.store/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/api.lexivocab.store/privkey.pem;
    
    location / {
        proxy_pass http://localhost:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

---

## 5. CI/CD Pipeline

### GitHub Actions (Ví dụ)

```yaml
# .github/workflows/deploy.yml
name: Build and Deploy

on:
  push:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build

  deploy:
    needs: test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: dotnet publish src/LexiVocab.API -c Release -o ./publish
      # Deploy to Azure / AWS / DigitalOcean
```

---

## 6. Monitoring & Observability

### Logging (Serilog)

```
Output sinks:
├── Console     ← Development
├── File        ← Rolling file logs
└── OpenTelemetry → SEQ   ← Centralized query

Log levels:
- Information: Request logs, business events
- Warning: Slow handlers (>500ms), cache failures, webhook issues
- Error: Unhandled exceptions, payment failures, email failures
```

### Health Checks

```
GET /healthz
├── PostgreSQL  ← EF Core health check
├── Redis       ← Redis health check
└── Returns 200 Healthy / 503 Unhealthy
```

### Metrics gợi ý thêm

```
Application Insights / Prometheus / Grafana:
- Request rate & latency (P50, P95, P99)
- Error rate by endpoint
- Database query time
- Redis cache hit/miss ratio
- Hangfire job success/failure
- Active subscriptions count
```

---

## 7. Checklist Production

```
☐ Tất cả secrets dùng environment variables (KHÔNG hardcode)
☐ Jwt:Secret ≥ 32 characters, unique per environment
☐ PayPal:WebhookId configured (bắt buộc production)
☐ CORS chỉ allow frontend domain
☐ HTTPS enforced (UseHsts, UseHttpsRedirection)
☐ Database migrations applied TRƯỚC khi deploy
☐ Redis configured cho distributed cache
☐ Hangfire dashboard restricted to Admin role ✅ (đã fix)
☐ Rate limiting enabled
☐ Security headers middleware active
☐ Kestrel server header disabled
☐ Max request body size = 10MB
☐ Health check endpoint /healthz accessible
☐ Log level = Information (không Debug)
☐ Backup PostgreSQL daily
☐ Monitor disk space (logs, Hangfire storage)
```
