# Hướng dẫn triển khai Production Backend (LexiVocab API) trên Azure

Tài liệu này cung cấp các bước chuẩn hóa (Production-Ready) để triển khai cơ sở hạ tầng (Infrastructure as Code - IaC) cho dự án vựng **LexiVocab API** lên Microsoft Azure sử dụng Terraform.

---

## 1. Kiến trúc Hệ thống (Infrastructure Architecture)

Hệ thống được thiết kế để tự động mở rộng, có khả năng phục hồi (resilient) và bảo mật:

- **Azure Container Apps (ACA):** Chạy ứng dụng .NET Web API (`ca-lexivocab-api`). Tự động mở rộng số lượng Replicas dựa trên số lượng request concurrent.
- **Azure Database for PostgreSQL (Flexible Server):** Lưu trữ dữ liệu chính.
- **Azure Cache for Redis:** Quản lý caching, Rate Limiting phân tán và phiên người dùng.
- **Azure Log Analytics Workspace:** Thu thập Log tập trung qua OpenTelemetry & Serilog.

---

## 2. Tiền điều kiện (Prerequisites)

Trước khi thực hiện, đảm bảo công cụ máy của bạn đã cài đặt các thành phần sau:
- [Terraform CLI](https://developer.hashicorp.com/terraform/downloads) (Phiên bản >= 1.5.0)
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) (az)
- [Docker](https://docs.docker.com/get-docker/) (nếu build image cục bộ)
- Tài khoản Microsoft Azure có quyền **Contributor** trên Subscription chỉ định.

---

## 3. Cấu hình Terraform

Tham chiếu tới thư mục `infra/terraform`. Chúng ta xác định một số biến môi trường quan trọng:

Tạo một tệp tin `terraform.tfvars` (hoặc `prod.tfvars`) trong thư mục `infra/terraform` và không theo dõi bởi Git (`.gitignore`):

```hcl
project_name                 = "lexivocab"
environment                  = "prod"
location                     = "eastasia" # Chọn region gần user nhất
db_username                  = "lexiadmin"
db_password                  = "Mật_Khẩu_Siêu_Bảo_Mật_1!#45" # Nên inject từ Azure Key Vault hoặc CI/CD pipeline
db_pool_size_per_replica     = 10
scale_concurrent_requests    = 100
max_replicas                 = 5
hangfire_workers_per_replica = 2
```

> [!WARNING]
> Tuyệt đối **không** commit tệp tin `terraform.tfvars` lên hệ thống Source Control để tránh lộ thông tin Secret.

---

## 4. Các bước Triển khai Terraform 

Quy trình thực thi chuẩn (Runbook) cho Terraform:

### Bước 1: Login vào hệ thống Azure
```bash
az login
az account set --subscription <YOUR_SUBSCRIPTION_ID>
```

### Bước 2: Khởi tạo Backend Terraform
```bash
cd infra/terraform
terraform init
```

*(Khuyến cáo cho Production: Thay vì lưu trữ Terraform State cục bộ (`terraform.tfstate`), bạn nên cấu hình lưu State tại Azure Storage Account để quản lý đồng thời (Concurrency state locking)).*

### Bước 3: Kiểm tra kế hoạch thay đổi (Plan)
```bash
terraform plan -var-file="prod.tfvars" -out="tfplan"
```
Xem kỹ output để xác nhận Terraform sẽ tạo ra đúng Container Apps, PostgreSQL Server và Redis. 

### Bước 4: Áp dụng triển khai (Apply)
```bash
terraform apply "tfplan"
```
Quá trình này sẽ mất tầm 10-15 phút do PostgreSQL Flexible Server tốn thời gian khởi tạo.
Sau khi xong, Output sẽ trả về FQDN của API (`api_url`).

---

## 5. Build và Deploy Container App (CI/CD Pipeline)

Terraform ở trên chỉ định Placeholder Image `mcr.microsoft.com/azuredocs/containerapps-helloworld:latest`. Hệ thống CI/CD (GitHub Actions / Azure DevOps) sẽ thực thi Build và Update phiên bản mới lên Container App.

Luồng tổng quan:
1. Trigger khi push vào branch `main`.
2. Build Docker Image và Push lên Azure Container Registry (ACR) hoặc Docker Hub.
3. Chạy câu lệnh AZ CLI để Update Container App Revision Mode:

```bash
az containerapp update \
  --name ca-lexivocab-api \
  --resource-group rg-lexivocab-prod \
  --image <YOUR_REGISTRY>/lexivocab-api:$(Build.BuildId)
```

---

## 6. Tính năng Auto-Migrate & Data Seeder

Trong quá trình khởi động `LexiVocab.API` có cung cấp cơ chế tự động Migration & Seeding:

1. App sử dụng chế độ `Liveness Probe`, `Readiness Probe` & `Startup Probe` được cấu hình tại Terraform để cho phép App đủ dung sai thời gian chạy Seed data (khoảng 150s startup tolerance).
2. Chúng ta sử dụng **Advisory Lock (`pg_try_advisory_lock`) của Postgres**. Do Container App có thể scale ra nhiều Replicas lên cùng lúc, hàm khóa này bảo đảm chỉ một Replica duy nhất thực hiện Migrate DB, tránh tình trạng xung đột Migration.

---

## 7. Theo dõi và Giám sát Sự cố (Monitoring & Troubleshooting)

Do ứng dụng .NET đã được cấu hình ghi Log qua **OpenTelemetry** lên **Serilog**, toàn bộ Log Application sẽ được bắn thẳng vào ***Azure Log Analytics Workspace***. 

Để truy vấn lỗi (Error Analytics), vào tài nguyên **Log Analytics Workspace** tương ứng trên Azure Portal và chạy KQL:
```kql
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "ca-lexivocab-api"
| where Log_s contains "Exception" or Log_s contains "Error"
| order by TimeGenerated desc
```

> [!TIP]
> Thay vì cấu hình File Sink cho Log (như là Local debugging), Production logging sử dụng Sink console với định dạng OpenTelemetry để Azure Log Analytics phân tách Log hiệu quả nhất. Mức log cấp `Information` được hạn chế, ưu tiên `Warning` trở lên.

---

## 8. Hướng dẫn Dọn dẹp/Hủy tài nguyên (Teardown)

Khi không còn sử dụng hạ tầng này (Ví dụ: Demo / Test xong), bạn cần xóa để dừng tính phí:
```bash
cd infra/terraform
terraform destroy -var-file="prod.tfvars"
```
