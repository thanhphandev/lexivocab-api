#!/bin/bash

# ==============================================================================
# LexiVocab API - PostgreSQL Backup to Cloudflare R2
# ==============================================================================
# Script này:
# 1. Chạy `pg_dump` tạo bản sao lưu trực tiếp vào volume /backups trong container
# 2. Sử dụng gzip để nén file
# 3. Upload file lên Cloudflare R2 qua AWS CLI (aws s3 cp)
# 4. Xóa các bản backup cũ trên Local (lưu 7 ngày gần nhất)
# ==============================================================================

set -e

# --- Cấu hình DB / Docker ---
DB_CONTAINER="lexivocab-db"
DB_USER="postgres"
DB_NAME="lexivocab_dev" # Thay bằng DB Prod của bạn
BACKUP_DIR="$(pwd)/backups"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP_FILENAME="lexivocab_${TIMESTAMP}.sql.gz"
LOCAL_BACKUP_PATH="${BACKUP_DIR}/${BACKUP_FILENAME}"

# --- Cấu hình Cloudflare R2 ---
# Đảm bảo bạn đã cấu hình aws cli bằng lệnh: `aws configure`
# Default Region name: auto
R2_BUCKET="lexivocab-backups" # Tên bucket trên R2
R2_ENDPOINT="https://<YOUR_CLOUDFLARE_ACCOUNT_ID>.r2.cloudflarestorage.com"
R2_S3_PATH="db-backups/$(date +"%Y/%m")"

# --- Bắt đầu Backup ---
echo "========================================="
echo "[*] Bắt đầu quá trình Backup: $(date)"
echo "========================================="

# 1. Đảm bảo thư mục local tồn tại
mkdir -p "$BACKUP_DIR"

# 2. Dump và nén dữ liệu từ container
echo "[*] Đang dump và nén dữ liệu từ container $DB_CONTAINER..."
# Nhờ mount ./backups:/backups trong docker-compose.yml, ghi file trong container sẽ hiển thị ở ngoài máy host luôn
docker exec -i "$DB_CONTAINER" sh -c "pg_dump -U $DB_USER -d $DB_NAME -F custom | gzip > /backups/${BACKUP_FILENAME}"

FILE_SIZE=$(du -h "$LOCAL_BACKUP_PATH" | cut -f1)
echo "[+] Dump thành công! Kích thước: $FILE_SIZE. File tại: $LOCAL_BACKUP_PATH"

# 3. Upload lên Cloudflare R2
echo "[*] Đang upload file lên Cloudflare R2 ($R2_BUCKET)..."

if [[ "$R2_ENDPOINT" == *"YOUR_CLOUDFLARE_ACCOUNT_ID"* ]]; then
    echo "[!] CẢNH BÁO: R2_ENDPOINT chưa được cấu hình đúng Account ID."
    echo "[!] Bỏ qua bước upload R2. Vui lòng cập nhật script."
else
    # Upload aws s3 cp
    aws s3 cp "$LOCAL_BACKUP_PATH" "s3://${R2_BUCKET}/${R2_S3_PATH}/${BACKUP_FILENAME}" \
      --endpoint-url "$R2_ENDPOINT"
    
    echo "[+] Upload thành công: s3://${R2_BUCKET}/${R2_S3_PATH}/${BACKUP_FILENAME}"
fi

# 4. Dọn dẹp bản backup ở Local
echo "[*] Dọn dẹp các bản backup trên Local (Giữ lại 7 ngày)..."
find "$BACKUP_DIR" -type f -name "*.sql.gz" -mtime +7 -exec rm {} \;
echo "[+] Hoàn tất dọn dẹp."

echo "========================================="
echo "[+] Backup hoàn tất lúc: $(date)"
echo "========================================="
