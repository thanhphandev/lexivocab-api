variable "project_name" {
  description = "Project name"
  type        = string
  default     = "lexivocab"
}

variable "environment" {
  description = "Environment (prod, dev, staging)"
  type        = string
  default     = "prod"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "southeastasia" # Singapore
}

variable "db_username" {
  description = "Username for PostgreSQL"
  type        = string
  default     = "lexivocab_admin"
}

variable "db_password" {
  description = "Password for PostgreSQL"
  type        = string
  sensitive   = true
}

variable "redis_sku" {
  description = "SKU for Azure Cache for Redis. Basic is the cheapest for dev/demo."
  type        = string
  default     = "Basic"
}

variable "redis_family" {
  description = "Family for Azure Cache for Redis"
  type        = string
  default     = "C"
}

variable "redis_capacity" {
  description = "Capacity for Azure Cache for Redis (0 = 250MB, cheapest)"
  type        = number
  default     = 0
}

# ─── Auto-Scaling Variables ────────────────────────────────────

variable "max_replicas" {
  description = "Maximum number of replicas for Container App (keep at 2 for demo to save costs)"
  type        = number
  default     = 2
}

variable "db_pool_size_per_replica" {
  description = "Maximum number of connection pool per replica. Formula: floor(pg_max_connections / max_replicas). Azure Burstable B1ms has ~50 connections."
  type        = number
  default     = 10
}

variable "hangfire_workers_per_replica" {
  description = "Number of Hangfire workers per replica. Keep low when scaling many instances to avoid overloading the DB."
  type        = number
  default     = 2
}

variable "scale_concurrent_requests" {
  description = "Threshold of HTTP concurrent requests before scaling up new replica"
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

# ─── App Secrets ───────────────────────────────────────────────

variable "jwt_secret" {
  description = "JWT Secret Key"
  type        = string
  sensitive   = true
}

variable "google_client_secret" {
  description = "Google Client Secret"
  type        = string
  sensitive   = true
}

variable "sepay_api_key" {
  description = "Sepay API Key"
  type        = string
  sensitive   = true
}

variable "paypal_client_secret" {
  description = "PayPal Client Secret"
  type        = string
  sensitive   = true
}

variable "resend_api_key" {
  description = "Resend API Key"
  type        = string
  sensitive   = true
}

variable "smtp_password" {
  description = "SMTP Password"
  type        = string
  sensitive   = true
}

variable "ai_custom_api_key" {
  description = "Custom AI API Key"
  type        = string
  sensitive   = true
}

variable "encryption_key" {
  description = "Encryption Key"
  type        = string
  sensitive   = true
}
