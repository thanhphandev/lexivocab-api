# 🚀 Production Deployment Guide — LexiVocab API on Azure

> **Phiên bản:** 2.0.1 | **Cập nhật:** 2026-04-21 | **Tác giả:** LexiVocab Team

Tài liệu này cung cấp hướng dẫn **từng bước, chi tiết** để triển khai toàn bộ hạ tầng Backend
cho LexiVocab API lên Microsoft Azure — từ khởi tạo hạ tầng (IaC) đến giám sát, bảo mật và vận hành.

---

## Mục lục

1. [Tổng quan Kiến trúc](#1-tổng-quan-kiến-trúc)
2. [Danh sách Tài nguyên Azure](#2-danh-sách-tài-nguyên-azure)
3. [Tiền điều kiện](#3-tiền-điều-kiện)
4. [Khởi tạo Terraform Backend (State)](#4-khởi-tạo-terraform-backend-state)
5. [Cấu hình Biến Terraform](#5-cấu-hình-biến-terraform)
6. [Triển khai Hạ tầng (Terraform)](#6-triển-khai-hạ-tầng-terraform)
7. [Cấu hình CI/CD Pipeline (GitHub Actions)](#7-cấu-hình-cicd-pipeline-github-actions)
8. [Biến Môi trường Container App](#8-biến-môi-trường-container-app)
9. [Hành vi theo Môi trường (Dev vs Production)](#9-hành-vi-theo-môi-trường-dev-vs-production)
10. [Cơ chế Auto-Migration & Seeding](#10-cơ-chế-auto-migration--seeding)
11. [Health Probes & Health Checks](#11-health-probes--health-checks)
12. [Auto-Scaling & Tối ưu Connection Pool](#12-auto-scaling--tối-ưu-connection-pool)
13. [Background Jobs (Hangfire)](#13-background-jobs-hangfire)
14. [Bảo mật Production](#14-bảo-mật-production)
15. [Giám sát & Logging](#15-giám-sát--logging)
16. [Xác minh sau Triển khai (Post-Deploy Verification)](#16-xác-minh-sau-triển-khai-post-deploy-verification)
17. [Rollback & DR (Disaster Recovery)](#17-rollback--dr-disaster-recovery)
18. [Chi phí Ước tính](#18-chi-phí-ước-tính)
19. [Teardown (Hủy tài nguyên)](#19-teardown-hủy-tài-nguyên)
20. [Tham chiếu nhanh lệnh CLI](#20-tham-chiếu-nhanh-lệnh-cli)

---

## 1. Tổng quan Kiến trúc

```
┌──────────────────────────────────────────────────────────────────┐
│                        INTERNET                                  │
│           Browser Extension / Web Dashboard / Mobile             │
└──────────────┬───────────────────────────────────────────────────┘
               │ HTTPS (TLS 1.2+)
               ▼
┌──────────────────────────────────────────────────────────────────┐
│              Azure Container Apps (Envoy Proxy)                  │
│         ┌─────────────┬─────────────┬─────────────┐             │
│         │  Replica 1  │  Replica 2  │  Replica N  │ ← Auto-Scale│
│         │  .NET 10    │  .NET 10    │  .NET 10    │   (1→5)     │
│         │  Kestrel    │  Kestrel    │  Kestrel    │             │
│         │  :8080      │  :8080      │  :8080      │             │
│         └──────┬──────┴──────┬──────┴──────┬──────┘             │
│                │             │             │                     │
│     ┌──────────▼─────────────▼─────────────▼──────────┐         │
│     │           Container App Environment              │         │
│     │     Log Analytics Workspace (OTEL Sink)          │         │
│     └──────────────────────────────────────────────────┘         │
└──────────────────────────────────────────────────────────────────┘
               │                              │
    ┌──────────▼──────────┐       ┌───────────▼───────────┐
    │   PostgreSQL 16     │       │   Azure Cache Redis   │
    │   Flexible Server   │       │   Standard C1         │
    │   B_Standard_B1ms   │       │   SSL-only, TLS 1.2   │
    │   VNet-Integrated   │       │   99.9% SLA           │
    │   35-day Backup     │       └───────────────────────┘
    │   Geo-Redundant     │
    └─────────────────────┘
               │
    ┌──────────▼──────────┐       ┌───────────────────────┐
    │   Private DNS Zone  │       │   Azure Key Vault     │
    │   VNet Link         │       │   Managed Identity    │
    └─────────────────────┘       │   Purge Protection    │
                                  └───────────────────────┘
               │
    ┌──────────▼──────────┐
    │   Azure Container   │
    │   Registry (ACR)    │
    │   Standard SKU      │
    │   Managed ID Pull   │
    └─────────────────────┘
```

---

## 2. Danh sách Tài nguyên Azure

Terraform tạo ra các tài nguyên sau (tổng cộng **9 tệp HCL**):

| Tệp Terraform      | Tài nguyên                                          | Naming Convention                     |
| ------------------- | --------------------------------------------------- | ------------------------------------- |
| `network.tf`        | Resource Group                                      | `rg-lexivocab-prod`                  |
| `network.tf`        | Virtual Network + Subnet (DB delegation)            | `vnet-lexivocab`, `snet-database`    |
| `network.tf`        | Private DNS Zone + VNet Link                        | `*.postgres.database.azure.com`      |
| `database.tf`       | PostgreSQL Flexible Server + Database               | `psql-lexivocab-prod`                |
| `redis.tf`          | Azure Cache for Redis                               | `redis-lexivocab-prod`               |
| `registry.tf`       | Azure Container Registry                            | `acrlexivocabprod****`               |
| `compute.tf`        | Log Analytics Workspace                             | `log-lexivocab-prod`                 |
| `compute.tf`        | Container App Environment + Container App           | `cae-lexivocab-prod`, `ca-lexivocab-api` |
| `security.tf`       | Key Vault + Access Policies + ACR Role Assignment   | `kv-lexivocab-prod-****`             |

---

## 3. Tiền điều kiện

### Phần mềm cần cài đặt

| Công cụ                                                                   | Phiên bản tối thiểu | Mục đích                        |
| ------------------------------------------------------------------------- | -------------------- | ------------------------------- |
| [Terraform CLI](https://developer.hashicorp.com/terraform/downloads)      | `>= 1.5.0`           | Provisioning hạ tầng (IaC)     |
| [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) | `>= 2.50`            | Xác thực + quản lý tài nguyên  |
| [Docker](https://docs.docker.com/get-docker/)                             | `>= 24.0`            | Build image cục bộ (tùy chọn)  |
| [Git](https://git-scm.com/)                                               | `>= 2.40`            | Version control + CI trigger   |

### Quyền Azure

- Tài khoản Azure với quyền **Contributor** hoặc **Owner** trên Subscription.
- Quyền tạo **Service Principal** (cho OIDC) hoặc đã có sẵn App Registration.

### Quyền GitHub

- Repository có cấu hình **Environment** là `production` (cho approval gate).
- Có quyền cấu hình **Secrets** trong repository settings.

---

## 4. Khởi tạo Terraform Backend (State)

> **Bắt buộc cho Production.** Terraform state phải được lưu trữ từ xa để đảm bảo
> state locking (ngăn 2 người apply đồng thời) và team collaboration.

### Bước 4.1: Tạo Storage Account cho Terraform State (Thủ công)

> [!IMPORTANT]  
> Terraform không thể tự tạo ra cái "kho" chứa chính nó một cách mượt mà. Bạn phải chạy lệnh CLI này **trước khi** chạy `terraform init`.

```powershell
# 1. Tạo Resource Group cho State
az group create --name rg-lexivocab-prod --location southeastasia

# 2. Tạo Storage Account (Tên phải UNIQUE toàn cầu)
$STORAGE_NAME = "lexivocabstate" + (Get-Random -Maximum 999)
az storage account create --name $STORAGE_NAME --resource-group rg-lexivocab-prod --sku Standard_LRS

# 3. Tạo Container 'tfstate'
az storage container create --name tfstate --account-name $STORAGE_NAME
```

### Bước 4.2: Khởi tạo Terraform với Remote Backend

```powershell
terraform init -reconfigure `
  -backend-config="resource_group_name=rg-lexivocab-prod" `
  -backend-config="storage_account_name=<TÊN_VỪA_TẠO_Ở_TRÊN>" `
  -backend-config="container_name=tfstate" `
  -backend-config="key=terraform.tfstate"
```

> **Lưu ý:** File `main.tf` đã cấu hình sẵn partial backend config. Các giá trị nhạy cảm
> được truyền qua `-backend-config` để không bị leak trong source code.

---

## 5. Cấu hình Biến Terraform

Tạo file `prod.tfvars` trong thư mục `infra/terraform/`:

```hcl
# ─── Thông tin Dự án ─────────────────────────────────────────────
project_name = "lexivocab"
environment  = "prod"
location     = "southeastasia"   # Singapore — gần user Việt Nam nhất

# ─── Database ────────────────────────────────────────────────────
db_username = "lexiadmin"
db_password = "Mật_Khẩu_Cực_Mạnh_Phải_Đổi_@2026!"  # ⚠️ Phải đổi!

# ─── Redis ───────────────────────────────────────────────────────
redis_sku      = "Standard"   # Standard = 99.9% SLA + replication
redis_family   = "C"
redis_capacity = 1            # 1GB cache

# ─── Auto-Scaling ────────────────────────────────────────────────
max_replicas                 = 5
scale_concurrent_requests    = 50    # Scale thêm replica khi >50 concurrent req
container_cpu                = 0.25  # 0.25 vCPU per replica
container_memory             = "0.5Gi"

# ─── Tuning cho Scaled Environment ───────────────────────────────
# Azure Burstable B1ms PostgreSQL: ~50 max_connections
# Formula: floor(50 / max_replicas) = floor(50 / 5) = 10
db_pool_size_per_replica     = 10
hangfire_workers_per_replica = 2     # Giữ thấp để tránh quá tải DB
```

> [!CAUTION]
> **KHÔNG BAO GIỜ** commit file `prod.tfvars` lên Git. Đảm bảo file này đã có trong `.gitignore`.
> Trong CI/CD, inject giá trị `db_password` từ GitHub Secrets hoặc Azure Key Vault.

---

## 6. Triển khai Hạ tầng (Terraform)

### Bước 6.1: Kiểm tra kế hoạch (Plan)

```bash
cd infra/terraform
terraform plan -var-file="prod.tfvars" -out="tfplan"
```

Review cẩn thận output — đặc biệt chú ý:
- ✅ Số tài nguyên sẽ tạo mới (`Plan: 12 to add`)
- ✅ Không có tài nguyên bị destroy ngoài ý muốn
- ✅ Connection string format đúng chuẩn Npgsql `Host=...;Port=...;Database=...;`

### Bước 6.2: Áp dụng (Apply)

```bash
terraform apply "tfplan"
```

⏳ **Thời gian:** ~10–15 phút (PostgreSQL và Redis tốn thời gian nhất).

> [!TIP]  
> **Xử lý lỗi "Already Exists" (Redis/Postgres):**  
> Nếu bạn lỡ tay ngắt lệnh (`Ctrl+C`) khi đang tạo Redis, hãy chạy lệnh Import sau để đồng bộ lại State:  
> `terraform import -var-file="prod.tfvars" azurerm_redis_cache.main /subscriptions/<SUB_ID>/resourceGroups/rg-lexivocab-prod/providers/Microsoft.Cache/redis/redis-lexivocab-prod`

### Bước 6.3: Lưu lại Output

```bash
terraform output
```

Output quan trọng:

| Key                              | Mô tả                                | Ví dụ                                          |
| -------------------------------- | ------------------------------------- | ----------------------------------------------- |
| `api_url` / `api_fqdn`          | FQDN của Container App               | `ca-lexivocab-api.niceforest-xxx.southeastasia.azurecontainerapps.io` |
| `acr_login_server`              | ACR server URL                        | `acrlexivocabprodxxxx.azurecr.io`              |
| `db_host`                        | PostgreSQL FQDN                       | `psql-lexivocab-prod.postgres.database.azure.com` |
| `redis_host`                     | Redis hostname                        | `redis-lexivocab-prod.redis.cache.windows.net` |
| `container_app_name`             | Tên Container App (cho CI/CD secrets) | `ca-lexivocab-api`                             |
| `resource_group_name`            | Tên Resource Group                    | `rg-lexivocab-prod`                            |

---

## 7. Cấu hình CI/CD Pipeline (GitHub Actions)

Pipeline CI/CD nằm tại `.github/workflows/cd.yml` và tự động trigger khi push vào branch `main`.

### 7.1 Tạo Azure Service Principal (OIDC — không cần client secret)

```bash
# Tạo App Registration
az ad app create --display-name "github-lexivocab-oidc"

# Lấy APP_ID
APP_ID=$(az ad app list --display-name "github-lexivocab-oidc" --query "[0].appId" -o tsv)

# Tạo Service Principal
az ad sp create --id $APP_ID

# Gán quyền Contributor lên Resource Group
SP_OBJECT_ID=$(az ad sp show --id $APP_ID --query "id" -o tsv)
az role assignment create \
  --assignee $SP_OBJECT_ID \
  --role "Contributor" \
  --scope "/subscriptions/<SUB_ID>/resourceGroups/rg-lexivocab-prod"

# Tạo Federated Credential cho GitHub OIDC
az ad app federated-credential create --id $APP_ID --parameters '{
  "name": "github-main-deploy",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<GITHUB_ORG>/<REPO_NAME>:environment:production",
  "audiences": ["api://AzureADTokenExchange"]
}'
```

### 7.2 Cấu hình GitHub Repository Secrets

Vào **Repository → Settings → Secrets and Variables → Actions**, tạo các secrets sau:

| Secret Name               | Giá trị                                      | Nguồn                        |
| ------------------------- | --------------------------------------------- | ----------------------------- |
| `AZURE_CLIENT_ID`         | Application (client) ID của App Registration  | Azure AD → App Registrations |
| `AZURE_TENANT_ID`         | Directory (tenant) ID                         | Azure AD → Overview          |
| `AZURE_SUBSCRIPTION_ID`   | Subscription ID                               | Azure Portal → Subscriptions |
| `ACR_NAME`                | Tên ACR (không phải URL)                      | `terraform output acr_login_server` → lấy phần trước `.azurecr.io` |
| `CONTAINER_APP_NAME`      | Tên Container App                             | `terraform output container_app_name` |
| `AZURE_RESOURCE_GROUP`    | Tên Resource Group                            | `terraform output resource_group_name` |

### 7.3 Cấu hình GitHub Environment

Vào **Repository → Settings → Environments → New environment**:

1. Tên: `production`
2. ✅ Bật **Required reviewers** (thêm tên bạn) — tạo approval gate trước mỗi deploy
3. ✅ Bật **Wait timer** nếu cần (vd: 5 phút chờ review)

### 7.4 Luồng Deployment

```
Push to main → GitHub Actions trigger
    ↓
Azure Login (OIDC — passwordless)
    ↓
Capture current image tag (cho rollback)
    ↓
az acr build → Build image trên ACR cloud (không cần Docker cục bộ)
    → Tag: lexivocab-api:<commit-sha> + lexivocab-api:latest
    ↓
Deploy new revision → Container App tạo revision mới
    ↓
Nếu fail? → Auto-rollback về previous image
```

---

## 8. Biến Môi trường Container App

Terraform tự động inject các biến sau vào Container App thông qua `compute.tf`:

| Biến                                   | Mô tả                                                    | Format / Ví dụ                                                       |
| --------------------------------------- | --------------------------------------------------------- | --------------------------------------------------------------------- |
| `ASPNETCORE_ENVIRONMENT`                | Môi trường runtime                                        | `Production`                                                          |
| `ConnectionStrings__DefaultConnection`  | Chuỗi kết nối PostgreSQL (Npgsql format)                  | `Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require;...` |
| `ConnectionStrings__Redis`              | Chuỗi kết nối Redis (StackExchange format)                | `hostname:6380,password=...,ssl=True,abortConnect=False,connectRetry=3` |
| `RUN_MIGRATIONS`                        | Cho phép auto-migration khi khởi động                     | `true`                                                                |
| `HANGFIRE_WORKER_COUNT`                 | Số Hangfire worker mỗi replica                            | `2`                                                                   |
| `DB_MAX_POOL_SIZE`                      | Max connections per replica (legacy override)              | `10`                                                                  |

### Biến cần cấu hình thủ công (sau terraform apply)

Các biến sau **không nằm trong Terraform** vì chứa secrets hoặc tuỳ theo domain. Cấu hình qua Azure Portal hoặc CLI:

```bash
az containerapp update \
  --name ca-lexivocab-api \
  --resource-group rg-lexivocab-prod \
  --set-env-vars \
    "Jwt__Secret=secretRef:jwt-secret" \
    "Jwt__Issuer=LexiVocab.API" \
    "Jwt__Audience=LexiVocab.Clients" \
    "Jwt__AccessTokenExpiryMinutes=30" \
    "Jwt__RefreshTokenExpiryDays=30" \
    "ENCRYPTION_KEY=secretRef:encryption-key" \
    "Resend__ApiKey=secretRef:resend-api-key" \
    "Google__ClientId=<YOUR_GOOGLE_CLIENT_ID>" \
    "Google__ClientSecret=secretRef:google-client-secret" \
    "PayPal__BaseUrl=https://api-m.paypal.com" \
    "PayPal__ClientId=<YOUR_PAYPAL_CLIENT_ID>" \
    "PayPal__ClientSecret=secretRef:paypal-client-secret" \
    "OTEL_EXPORTER_OTLP_ENDPOINT=<YOUR_OTEL_ENDPOINT>" \
    "Cors__AllowedOrigins__0=https://lexivocab.store" \
    "Cors__AllowedOrigins__1=chrome-extension://*" \
    "App__Url=https://lexivocab.store"
```

> [!IMPORTANT]
> Các giá trị dùng `secretRef:...` nên được lưu trong **Azure Container App Secrets**
> (hoặc tham chiếu từ Key Vault) để không hiển thị dưới dạng plaintext.

---

## 9. Hành vi theo Môi trường (Dev vs Production)

Ứng dụng tự động điều chỉnh hành vi dựa trên `ASPNETCORE_ENVIRONMENT`:

| Tính năng                         | Development                              | Production                              |
| --------------------------------- | ---------------------------------------- | --------------------------------------- |
| **OpenAPI / Scalar UI**           | ✅ Có (`/openapi`, `/scalar`)            | ❌ **Không** — route không được map     |
| **Serilog File Sink**             | ✅ `logs/lexivocab-.log` (rolling daily) | ❌ Chỉ Console + OTEL                  |
| **Serilog Minimum Level**         | `Information`                            | `Warning`                               |
| **Private Network Access**        | ✅ `Allow-Private-Network` header        | ❌ Không cần                            |
| **HSTS (Strict-Transport)**       | ❌ Không (tránh lock localhost)           | ✅ Bật để buộc HTTPS                    |
| **Kestrel Server Header**         | Bị ẩn (hardened)                         | Bị ẩn (hardened)                        |
| **Auto-Migration**                | ✅ Luôn chạy                             | ✅ Chỉ khi `RUN_MIGRATIONS=true`       |
| **Hangfire Dashboard Access**     | ✅ Mọi người                             | 🔒 Chỉ Admin (JWT + Role=Admin)        |
| **Security Headers**              | ✅ Full (CSP relaxed cho UI routes)      | ✅ Full (CSP strict cho API routes)     |
| **Response Compression**          | ✅ Brotli + Gzip                         | ✅ Brotli + Gzip                        |
| **Rate Limiting**                 | ✅ In-memory                             | ✅ In-memory (per-replica)              |

### Route chỉ có ở Development

```
GET  /openapi/v1.json      → OpenAPI schema (JSON)
GET  /scalar/v1             → Scalar API Reference UI (interactive docs)
```

### Route có ở cả hai môi trường

```
GET  /                      → API info (app name, version, status)
GET  /health                → Health check (DB + Redis status, JSON)
GET  /hangfire              → Hangfire Dashboard (Production: Admin only)
ALL  /api/v1/*              → API endpoints
```

> [!WARNING]
> Ở Production, các routes `/openapi` và `/scalar` **hoàn toàn không tồn tại** —
> trả về 404. Đây là thiết kế có chủ đích để tránh lộ schema API cho attacker.

---

## 10. Cơ chế Auto-Migration & Seeding

### Vấn đề

Khi Container App scale ra nhiều replicas đồng thời, tất cả replicas đều cố gắng chạy
migration → dẫn đến **race condition** và lỗi `already exists`.

### Giải pháp: PostgreSQL Advisory Lock

```
Replica 1: SELECT pg_try_advisory_lock(20260412) → true  → Chạy migration + seeding
Replica 2: SELECT pg_try_advisory_lock(20260412) → false → Bỏ qua, log "⏳ Another instance is running..."
Replica 3: SELECT pg_try_advisory_lock(20260412) → false → Bỏ qua
```

- **Lock ID:** `20260412` (arbitrary, cố định trong code)
- **Lock scope:** Session-level — tự giải phóng khi connection đóng
- **Explicit unlock:** Code gọi `pg_advisory_unlock()` trong `finally` block

### Startup Timeline

```
0s    → Container App khởi động replica
0-10s → Startup Probe bắt đầu check /health (interval: 5s)
10s   → Liveness Probe bắt đầu check (initial_delay: 10s)
~30s  → EF Core migration + data seeding hoàn tất (trung bình)
~150s → Startup Probe timeout (30 failures × 5s) — nếu vượt quá → restart container
```

---

## 11. Health Probes & Health Checks

### Azure Container App Probes (Terraform)

| Probe Type       | Path      | Port | Interval | Timeout | Fail Threshold | Mục đích                                                |
| ---------------- | --------- | ---- | -------- | ------- | -------------- | ------------------------------------------------------- |
| **Startup**      | `/health` | 8080 | 5s       | 3s      | 30 (=150s)     | Cho phép migration/seed chạy xong trước khi bị restart  |
| **Liveness**     | `/health` | 8080 | 30s      | 5s      | 3              | Restart container nếu app treo/không phản hồi           |
| **Readiness**    | `/health` | 8080 | 10s      | 3s      | 3 (success: 1) | Chỉ route traffic đến replica khi nó thực sự sẵn sàng   |

> [!IMPORTANT]  
> **Bootstrapping Note:**  
> Trong file `compute.tf` lần đầu, chúng ta sử dụng image `helloworld` và tạm thời **comment** các probe này. 
> Sau khi bạn push image thật lên ACR qua GitHub Actions, hãy mở lại (uncomment) các probe này để đảm bảo hệ thống giám sát đúng.

### Application Health Check (`/health`)

Endpoint `/health` trả về JSON chi tiết:

```json
{
  "status": "Healthy",
  "timestamp": "2026-04-20T08:00:00Z",
  "version": "1.0.0",
  "checks": [
    { "name": "npgsql", "status": "Healthy" },
    { "name": "redis", "status": "Healthy" }
  ]
}
```

Health checks được đăng ký trong `Program.cs`:
- **PostgreSQL:** `healthChecks.AddNpgSql(connectionString)` — kiểm tra kết nối DB
- **Redis:** `healthChecks.AddRedis(connectionString)` — kiểm tra kết nối cache

---

## 12. Auto-Scaling & Tối ưu Connection Pool

### Cách Auto-Scaling hoạt động

```
                    scale_concurrent_requests = 50
                              │
Request rate ═══════════════╗  │
                            ▼  ▼
Replicas:  [1] ──── 50 req ──→ [2] ──── 100 req ──→ [3] ─── ... ──→ [max_replicas]
```

- **Trigger:** HTTP concurrent requests > `scale_concurrent_requests` (mặc định: 50)
- **Min replicas:** 1 (luôn có ít nhất 1 instance chạy)
- **Max replicas:** 5 (giới hạn bởi DB connection pool)
- **Scale-to-zero:** Không — `min_replicas = 1` để tránh cold start

### Connection Pool Formula

```
              Azure Basic PG max_connections ≈ 50
    ──────────────────────────────────────────────────── = 10 connections/replica
                    max_replicas = 5

    Thực tế: 10 (app) + 2 (Hangfire workers) = ~12 connections/replica
    Tổng max: 12 × 5 = 60 → ⚠️ Sát giới hạn 50, cần monitor
```

> [!TIP]
> Nếu cần scale > 5 replicas, phải nâng SKU PostgreSQL lên **General Purpose**
> (`GP_Standard_D2s_v3` — 100 connections) hoặc dùng **PgBouncer** làm proxy.

---

## 13. Background Jobs (Hangfire)

Hangfire sử dụng **PostgreSQL** làm storage (không cần thêm tài nguyên).

### Recurring Jobs

| Job Name                     | Schedule              | Mô tả                                       |
| ---------------------------- | --------------------- | -------------------------------------------- |
| `SubscriptionExpirationJob`  | Daily @ 00:00 UTC     | Kiểm tra & hết hạn subscription quá hạn      |
| `ReviewReminderJob`          | Daily @ 01:00 UTC     | Gửi email nhắc nhở review từ vựng            |
| `PendingPaymentCleanupJob`   | Every minute          | Auto-cancel pending payments đã hết thời hạn |

### Worker Count Strategy

- **Per replica:** `HANGFIRE_WORKER_COUNT` (default: 2)
- **Toàn cluster:** 2 × 5 replicas = **10 concurrent workers max**
- **Server name:** `{MachineName}:{random-8-chars}` — đảm bảo unique giữa các replicas

> [!IMPORTANT]
> Trong môi trường scaled, mỗi replica đều chạy Hangfire server riêng.
> Hangfire sử dụng **distributed locking** qua PostgreSQL nên các job **không bị chạy trùng lặp**.

---

## 14. Bảo mật Production

### 14.1 Network Security

- ✅ PostgreSQL nằm trong **Private VNet** — không expose ra internet
- ✅ Private DNS Zone giải quyết hostname nội bộ
- ✅ Redis chỉ chấp nhận **SSL connections** (port 6380, TLS 1.2+)
- ✅ Container App ingress chỉ chấp nhận **HTTPS** (`allow_insecure_connections = false`)

### 14.2 Identity & Access

- ✅ **Managed Identity** (SystemAssigned) — không cần lưu trữ credential
- ✅ Container App → ACR: Pull qua **AcrPull** role assignment
- ✅ Container App → Key Vault: Truy cập qua **Access Policy** (Get + List)
- ✅ CI/CD → Azure: **OIDC (Federated Credential)** — không cần client secret

### 14.3 Application Security

| Tính năng                     | Chi tiết                                                       |
| ----------------------------- | -------------------------------------------------------------- |
| **Security Headers**          | X-Content-Type-Options, X-Frame-Options, CSP, HSTS, etc.      |
| **Kestrel Hardened**          | Server header bị ẩn, max body 10MB                             |
| **Rate Limiting**             | 5 tầng (Global 100/min, Auth 5/min, Sensitive 10/min, etc.)   |
| **JWT Validation**            | Strict (no clock skew, issuer + audience check)                |
| **User Deactivation Check**   | Redis-cached deactivation flag checked on every token validate |
| **CORS**                      | Configured per environment (wildcard for browser extensions)   |
| **Non-root Container**        | Docker user `appuser` — prevents privilege escalation          |
| **Dockerfile**                | Alpine-based minimal image (~80MB)                             |
| **Key Vault**                 | Soft delete 90 days + purge protection enabled                 |

### 14.4 Rate Limiting Policies

| Policy               | Limit       | Áp dụng cho                              |
| --------------------- | ----------- | ---------------------------------------- |
| **Global**            | 100 req/min | Tất cả endpoints                         |
| **AuthStrictLimit**   | 5 req/min   | Login, register, forgot-password         |
| **UserReadLimit**     | 60 req/min  | GET /me, /permissions                    |
| **RefreshLimit**      | 30 req/min  | Token refresh                            |
| **SensitiveWriteLimit** | 10 req/min | Đổi password, đổi email, xóa tài khoản  |

> [!NOTE]
> Rate limiting hiện dùng **in-memory** — mỗi replica có counter riêng.
> Effective limit khi 5 replicas = `limit × 5`. Nếu cần chính xác hơn,
> upgrade lên **Redis-backed distributed rate limiting**.

---

## 15. Giám sát & Logging

### 15.1 Logging Pipeline

```
Application (.NET)
    │
    ├─→ Console (stdout) ──→ Azure Container Apps Console Logs ──→ Log Analytics
    │
    └─→ OpenTelemetry (OTLP/HTTP) ──→ Seq / Grafana / Azure Monitor
```

### 15.2 Serilog Configuration (Production)

| Cấu hình                        | Giá trị                    | Lý do                                               |
| -------------------------------- | -------------------------- | ---------------------------------------------------- |
| Minimum Level                    | `Warning`                  | Giảm noise, chỉ log lỗi + cảnh báo                  |
| Microsoft.AspNetCore override    | `Warning`                  | Tránh flood log từ framework                         |
| Microsoft.EFCore override        | `Warning`                  | Chỉ log SQL errors, không log từng query             |
| File Sink                        | ❌ Disabled                | Container ephemeral — disk I/O tốn kém, storage bị xóa khi restart |
| Console Sink                     | ✅ Enabled                 | Azure Container Apps tự thu console logs              |
| OTEL Sink                        | ✅ Enabled                 | Structured logs → centralized observability           |

### 15.3 Truy vấn Log qua Azure Portal

Vào **Log Analytics Workspace** → **Logs**, chạy KQL:

#### Xem tất cả lỗi gần đây
```kql
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "ca-lexivocab-api"
| where Log_s contains "Exception" or Log_s contains "Error" or Log_s contains "❌"
| order by TimeGenerated desc
| take 50
```

#### Theo dõi startup/migration
```kql
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "ca-lexivocab-api"
| where Log_s contains "🚀" or Log_s contains "✅" or Log_s contains "⏳"
| order by TimeGenerated desc
| take 20
```

#### Xem replica scale events
```kql
ContainerAppSystemLogs_CL
| where ContainerAppName_s == "ca-lexivocab-api"
| where Reason_s == "Pulling" or Reason_s == "Started" or Reason_s == "ScalingUp"
| project TimeGenerated, Reason_s, Log_s
| order by TimeGenerated desc
```

#### Giám sát Health Check failures
```kql
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "ca-lexivocab-api"
| where Log_s contains "Unhealthy" or Log_s contains "Degraded"
| order by TimeGenerated desc
```

### 15.4 Giám sát Redis

```bash
# Xem metrics real-time
az redis show --name redis-lexivocab-prod --resource-group rg-lexivocab-prod \
  --query "{status:provisioningState, memory:redisConfiguration.maxmemoryPolicy, connections:accessKeys}"
```

### 15.5 Giám sát PostgreSQL

```bash
# Xem server status
az postgres flexible-server show \
  --name psql-lexivocab-prod \
  --resource-group rg-lexivocab-prod \
  --query "{state:state, version:version, sku:sku.name, storage:storage.storageSizeGb}"
```

---

## 16. Xác minh sau Triển khai (Post-Deploy Verification)

Chạy các bước sau **ngay sau mỗi lần deploy** để xác nhận mọi thứ hoạt động:

### Checklist nhanh

```bash
API_URL="https://$(az containerapp show \
  --name ca-lexivocab-api \
  --resource-group rg-lexivocab-prod \
  --query properties.configuration.ingress.fqdn -o tsv)"

echo "🔗 API URL: $API_URL"

# 1. Root endpoint
curl -s "$API_URL/" | jq .
# Expected: { "app": "LexiVocab API", "version": "1.0.0", "status": "healthy" }

# 2. Health check (DB + Redis)
curl -s "$API_URL/health" | jq .
# Expected: { "status": "Healthy", "checks": [...] }

# 3. Xác nhận OpenAPI KHÔNG có ở Production
curl -s -o /dev/null -w "%{http_code}" "$API_URL/openapi/v1.json"
# Expected: 404

# 4. Xác nhận Scalar UI KHÔNG có ở Production
curl -s -o /dev/null -w "%{http_code}" "$API_URL/scalar/v1"
# Expected: 404

# 5. Xác nhận security headers
curl -sI "$API_URL/" | grep -iE "(x-content-type|x-frame|content-security|strict-transport)"
# Expected: nosniff, DENY, CSP policy, strict-transport-security

# 6. Xác nhận Hangfire Dashboard yêu cầu auth
curl -s -o /dev/null -w "%{http_code}" "$API_URL/hangfire"
# Expected: 401 hoặc 403

# 7. Check replica count
az containerapp show \
  --name ca-lexivocab-api \
  --resource-group rg-lexivocab-prod \
  --query "properties.runningStatus.replicas" -o tsv
# Expected: >= 1
```

---

## 17. Rollback & DR (Disaster Recovery)

### 17.1 Rollback Tự động (CI/CD)

Pipeline GitHub Actions đã bao gồm **auto-rollback**: nếu step `Deploy to Azure Container Apps`
fail, step `Rollback on failure` sẽ tự động revert về Docker image trước đó.

### 17.2 Rollback Thủ công

```bash
# Xem lịch sử revision
az containerapp revision list \
  --name ca-lexivocab-api \
  --resource-group rg-lexivocab-prod \
  -o table

# Activate revision cũ (rollback)
az containerapp revision activate \
  --name ca-lexivocab-api \
  --resource-group rg-lexivocab-prod \
  --revision <REVISION_NAME>

# Chuyển 100% traffic sang revision cũ
az containerapp ingress traffic set \
  --name ca-lexivocab-api \
  --resource-group rg-lexivocab-prod \
  --revision-weight <REVISION_NAME>=100
```

### 17.3 Database Backup & Restore

- **Automatic Backup:** Azure PostgreSQL Flexible Server tự backup mỗi ngày
- **Retention:** 35 ngày (cấu hình trong `database.tf`)
- **Geo-Redundant:** ✅ Bật — backup được replicate sang region khác

```bash
# Point-in-time restore (khôi phục DB về thời điểm cụ thể)
az postgres flexible-server restore \
  --name psql-lexivocab-prod-restored \
  --resource-group rg-lexivocab-prod \
  --source-server psql-lexivocab-prod \
  --restore-time "2026-04-19T12:00:00Z"
```

---

## 18. Chi phí Ước tính

> Giá tham khảo cho region **Southeast Asia** (tháng 04/2026). Giá thực tế có thể thay đổi.

| Tài nguyên                         | SKU / Config          | Chi phí ước tính /tháng |
| ----------------------------------- | --------------------- | ----------------------- |
| Container Apps (1 replica idle)     | 0.25 vCPU, 0.5Gi     | ~$5–10                  |
| Container Apps (5 replicas active)  | 0.25 vCPU × 5        | ~$25–50                 |
| PostgreSQL Flexible Server          | B_Standard_B1ms, 32GB | ~$15–20                 |
| Azure Cache for Redis               | Standard C1, 1GB      | ~$25–30                 |
| Container Registry                  | Standard              | ~$5                     |
| Log Analytics                       | Pay-per-GB (ingested) | ~$2–5                   |
| Key Vault                           | Standard (operations) | ~$0.03/10K ops          |
| **Tổng ước tính (idle)**            |                       | **~$52–70/tháng**       |
| **Tổng ước tính (active scaling)**  |                       | **~$72–115/tháng**      |

---

## 19. Teardown (Hủy tài nguyên)

Khi không còn sử dụng hạ tầng (demo/test xong), xóa **toàn bộ** để dừng tính phí:

```bash
cd infra/terraform

# Review những gì sẽ bị xóa
terraform plan -destroy -var-file="prod.tfvars"

# Xóa toàn bộ tài nguyên
terraform destroy -var-file="prod.tfvars"
```

> [!CAUTION]
> - Key Vault có **purge protection** (90 ngày) — không thể xóa ngay.
> - PostgreSQL backup sẽ được **giữ lại** trong 35 ngày sau khi server bị xóa.
> - Đừng quên xóa cả **Terraform State Storage Account** nếu không cần:
>   ```bash
>   az group delete --name rg-terraform-state --yes
>   ```

---

## 20. Tham chiếu nhanh lệnh CLI

```bash
# ─── Xem Container App logs (real-time) ─────────────────────────
az containerapp logs show \
  --name ca-lexivocab-api \
  --resource-group rg-lexivocab-prod \
  --type console \
  --follow

# ─── Xem replicas đang chạy ─────────────────────────────────────
az containerapp replica list \
  --name ca-lexivocab-api \
  --resource-group rg-lexivocab-prod \
  -o table

# ─── Restart container app ──────────────────────────────────────
az containerapp revision restart \
  --name ca-lexivocab-api \
  --resource-group rg-lexivocab-prod \
  --revision $(az containerapp show --name ca-lexivocab-api \
    --resource-group rg-lexivocab-prod \
    --query properties.latestRevisionName -o tsv)

# ─── Scale thủ công (override auto-scale tạm thời) ──────────────
az containerapp update \
  --name ca-lexivocab-api \
  --resource-group rg-lexivocab-prod \
  --min-replicas 2 --max-replicas 10

# ─── Xem PostgreSQL connection count ────────────────────────────
az postgres flexible-server parameter show \
  --server-name psql-lexivocab-prod \
  --resource-group rg-lexivocab-prod \
  --name max_connections

# ─── Xem Redis info ─────────────────────────────────────────────
az redis show \
  --name redis-lexivocab-prod \
  --resource-group rg-lexivocab-prod \
  -o table

# ─── Thêm/sửa env var cho Container App ─────────────────────────
az containerapp update \
  --name ca-lexivocab-api \
  --resource-group rg-lexivocab-prod \
  --set-env-vars "KEY=value"

# ─── Xem Terraform state (debug) ────────────────────────────────
terraform state list
terraform state show azurerm_container_app.api
```

---

> **Tài liệu này được duy trì cùng source code.** Khi có thay đổi hạ tầng hoặc pipeline,
> vui lòng cập nhật tương ứng. Mọi thắc mắc liên hệ: LexiVocab DevOps Team.
