variable "project_name" {
  description = "Tên dự án"
  type        = string
  default     = "lexivocab"
}

variable "environment" {
  description = "Môi trường triển khai (prod, dev, staging)"
  type        = string
  default     = "prod"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "southeastasia" # Singapore
}

variable "db_username" {
  description = "Username cho PostgreSQL"
  type        = string
  default     = "lexivocab_admin"
}

variable "db_password" {
  description = "Password cho PostgreSQL"
  type        = string
  sensitive   = true
}

variable "redis_sku" {
  description = "SKU cho Azure Cache for Redis. Standard có SLA 99.9% và replication."
  type        = string
  default     = "Standard"
}

variable "redis_family" {
  description = "Family cho Azure Cache for Redis"
  type        = string
  default     = "C"
}

variable "redis_capacity" {
  description = "Capacity cho Azure Cache for Redis"
  type        = number
  default     = 1
}

# ─── Auto-Scaling Variables ────────────────────────────────────

variable "max_replicas" {
  description = "Số replica tối đa cho Container App (auto-scaling)"
  type        = number
  default     = 5
}

variable "db_pool_size_per_replica" {
  description = "Số connection pool tối đa cho mỗi replica. Formula: floor(pg_max_connections / max_replicas). Azure Burstable B1ms có ~50 connections."
  type        = number
  default     = 10
}

variable "hangfire_workers_per_replica" {
  description = "Số worker Hangfire mỗi replica. Giữ thấp khi scale nhiều instance để tránh quá tải DB."
  type        = number
  default     = 2
}

variable "scale_concurrent_requests" {
  description = "Ngưỡng HTTP concurrent requests trước khi scale thêm replica mới"
  type        = number
  default     = 50
}

variable "container_cpu" {
  description = "CPU allocated per unit (0.25, 0.5, 1.0, etc.)"
  type        = number
  default     = 0.25
}

variable "container_memory" {
  description = "Memory allocated per unit (0.5Gi, 1.0Gi, etc.)"
  type        = string
  default     = "0.5Gi"
}
