resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${var.project_name}-${var.environment}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

resource "azurerm_container_app_environment" "main" {
  name                       = "cae-${var.project_name}-${var.environment}"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
}

resource "azurerm_container_app" "api" {
  name                         = "ca-${var.project_name}-api"
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"

  # Managed Identity — required for Key Vault access and ACR pull
  identity {
    type = "SystemAssigned"
  }

  # ACR pull credentials — allows Container App to pull images from private registry
  registry {
    identity = "system"
    server   = azurerm_container_registry.main.login_server
  }

  # ─── Secrets Configuration ──────────────────────────────────────────
  secret {
    name  = "jwt-secret"
    value = var.jwt_secret
  }
  secret {
    name  = "google-client-secret"
    value = var.google_client_secret
  }
  secret {
    name  = "sepay-api-key"
    value = var.sepay_api_key
  }
  secret {
    name  = "paypal-client-secret"
    value = var.paypal_client_secret
  }
  secret {
    name  = "resend-api-key"
    value = var.resend_api_key
  }
  secret {
    name  = "smtp-password"
    value = var.smtp_password
  }
  secret {
    name  = "ai-custom-api-key"
    value = var.ai_custom_api_key
  }
  secret {
    name  = "encryption-key"
    value = var.encryption_key
  }

  template {
    container {
      name   = "lexivocab-api"
      # image  = "${azurerm_container_registry.main.login_server}/lexivocab-api:latest" # CI/CD sẽ update tag
      image  = "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest" 
      cpu    = var.container_cpu
      memory = var.container_memory

      # ─── Core App Settings ──────────────────────────────────────────
      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      # ─── Database Connection (Pool size tuned for auto-scaling) ─────
      # Formula: floor(max_connections / max_replicas) — Azure Basic PG: 50 connections
      env {
        name  = "ConnectionStrings__DefaultConnection"
        value = "Host=${azurerm_postgresql_flexible_server.main.fqdn};Port=5432;Database=${azurerm_postgresql_flexible_server_database.main.name};Username=${var.db_username};Password=${var.db_password};SSL Mode=Require;Trust Server Certificate=true;Maximum Pool Size=${var.db_pool_size_per_replica};Pooling=true;Connection Idle Lifetime=300"
      }

      # ─── Redis Connection (with resiliency for cloud) ──────────────
      env {
        name  = "ConnectionStrings__Redis"
        value = "${azurerm_redis_cache.main.hostname}:${azurerm_redis_cache.main.ssl_port},password=${azurerm_redis_cache.main.primary_access_key},ssl=True,abortConnect=False,connectRetry=3"
      }

      # ─── Migration Flag ─────────────────────────────────────────────
      env {
        name  = "RUN_MIGRATIONS"
        value = "true"
      }

      # ─── Hangfire Workers (conservative for scaled environment) ─────
      env {
        name  = "HANGFIRE_WORKER_COUNT"
        value = tostring(var.hangfire_workers_per_replica)
      }

      # ─── DB Pool Size Override (matches Terraform calculation) ──────
      env {
        name  = "DB_MAX_POOL_SIZE"
        value = tostring(var.db_pool_size_per_replica)
      }

      # ─── JWT ────────────────────────────────────────────────────────
      env {
        name        = "Jwt__Secret"
        secret_name = "jwt-secret"
      }
      env {
        name  = "Jwt__AccessTokenExpiryMinutes"
        value = "120"
      }
      env {
        name  = "Jwt__RefreshTokenExpiryDays"
        value = "30"
      }
      env {
        name  = "Jwt__RefreshTokenGracePeriodSeconds"
        value = "60"
      }
      env {
        name  = "Jwt__ClockSkewSeconds"
        value = "0"
      }



      # ─── App URL ────────────────────────────────────────────────────
      env {
        name  = "App__Url"
        value = "https://lexivocab.store"
      }

      # ─── Google OAuth ───────────────────────────────────────────────
      env {
        name  = "Google__ClientId"
        value = "265833989160-8trk2n7vgl2h06tsufnb5d7tisd1cv2u.apps.googleusercontent.com"
      }
      env {
        name        = "Google__ClientSecret"
        secret_name = "google-client-secret"
      }

      # ─── Sepay Payment ──────────────────────────────────────────────
      env {
        name        = "Sepay__ApiKey"
        secret_name = "sepay-api-key"
      }
      env {
        name  = "Sepay__BankAccount"
        value = "96247PVTHANH"
      }
      env {
        name  = "Sepay__BankName"
        value = "BIDV"
      }
      env {
        name  = "Sepay__ApiBaseUrl"
        value = "https://my.sepay.vn/api"
      }
      env {
        name  = "Sepay__QrTemplate"
        value = "https://qr.sepay.vn/img?acc={0}&bank={1}&amount={2}&des={3}"
      }

      # ─── PayPal ─────────────────────────────────────────────────────
      env {
        name  = "PayPal__ClientId"
        value = "AUIxa-VT97aCRWMbCwd3nF0ySaHqvWiXaY1r-IpdHxbHdwvU5Wd2YsNMdU0lpGuhdKlyHEWZwSioQqsD"
      }
      env {
        name        = "PayPal__ClientSecret"
        secret_name = "paypal-client-secret"
      }
      env {
        name  = "PayPal__WebhookId"
        value = "REPLACE_WITH_PAYPAL_LIVE_WEBHOOK_ID"
      }
      env {
        name  = "PayPal__ReturnUrl"
        value = "https://lexivocab.store/checkout/success"
      }
      env {
        name  = "PayPal__CancelUrl"
        value = "https://lexivocab.store/pricing"
      }

      # ─── Email (Resend) ─────────────────────────────────────────────
      env {
        name        = "Resend__ApiKey"
        secret_name = "resend-api-key"
      }
      env {
        name  = "Resend__SenderName"
        value = "LexiVocab Team"
      }
      env {
        name  = "Resend__SenderEmail"
        value = "no-reply@lexivocab.store"
      }

      # ─── Email (SMTP) ───────────────────────────────────────────────
      env {
        name  = "Smtp__Server"
        value = "smtp.gmail.com"
      }
      env {
        name  = "Smtp__Port"
        value = "587"
      }
      env {
        name  = "Smtp__Username"
        value = "thanhphanvan1610@gmail.com"
      }
      env {
        name        = "Smtp__Password"
        secret_name = "smtp-password"
      }
      env {
        name  = "Smtp__SenderName"
        value = "LexiVocab Team"
      }
      env {
        name  = "Smtp__SenderEmail"
        value = "no-reply@lexivocab.store"
      }

      # ─── AI Providers ───────────────────────────────────────────────
      env {
        name  = "AIProviders__DefaultProvider"
        value = "custom"
      }
      env {
        name        = "AIProviders__custom__ApiKey"
        secret_name = "ai-custom-api-key"
      }
      env {
        name  = "AIProviders__custom__BaseUrl"
        value = "https://ai.lexivocab.store:20128/v1"
      }
      env {
        name  = "AIProviders__custom__DefaultModel"
        value = "cx/gpt-5.4"
      }
      env {
        name  = "AIProviders__custom__MaxTokens"
        value = "4096"
      }

      # ─── Encryption ─────────────────────────────────────────────────
      env {
        name        = "ENCRYPTION_KEY"
        secret_name = "encryption-key"
      }

      # Liveness probe: Tells Azure when to RESTART a broken container
      # liveness_probe {
      #   transport               = "HTTP"
      #   port                    = 8080
      #   path                    = "/health"
      #   initial_delay           = 10
      #   interval_seconds        = 30
      #   timeout                 = 5
      #   failure_count_threshold = 3
      # }

      # Readiness probe: Tells Azure when this replica is ready to receive traffic
      # readiness_probe {
      #   transport               = "HTTP"
      #   port                    = 8080
      #   path                    = "/health"
      #   interval_seconds        = 10
      #   timeout                 = 3
      #   failure_count_threshold = 3
      #   success_count_threshold = 1
      # }

      # startup_probe {
      #   transport               = "HTTP"
      #   port                    = 8080
      #   path                    = "/health"
      #   interval_seconds        = 15
      #   timeout                 = 3
      #   failure_count_threshold = 10 # 10 × 15s = 150s tolerance for startup
      # }
    }

    # ─── Auto-scaling: Scale based on concurrent HTTP requests ─────────
    min_replicas = 1
    max_replicas = var.max_replicas

    # ─── HTTP Scale Rule: Scale up when concurrent requests exceed threshold
    http_scale_rule {
      name                = "http-concurrency"
      concurrent_requests = tostring(var.scale_concurrent_requests)
    }
  }

  ingress {
    allow_insecure_connections = false
    external_enabled           = true
    target_port                = 80 # Cổng 80 cho image 'helloworld' mồi. Sau này CI/CD tự update lên 8080.
    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  # lifecycle {
  #   ignore_changes = [
  #     template[0].container[0].image,
  #     ingress[0].target_port # Target port might change between helloworld (80) and real app (8080)
  #   ]
  # }
}

output "api_url" {
  value = azurerm_container_app.api.latest_revision_fqdn
}
