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
  registry_credentials {
    identity = azurerm_container_app.api.identity[0].id
    server   = azurerm_container_registry.main.login_server
  }

  template {
    # ─── Auto-scaling: Scale based on concurrent HTTP requests ─────────
    min_replicas = 1
    max_replicas = var.max_replicas

    container {
      name   = "lexivocab-api"
      image  = "${azurerm_container_registry.main.login_server}/lexivocab-api:latest" # CI/CD sẽ update tag
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

      # Liveness probe: Tells Azure when to RESTART a broken container
      liveness_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/health"
        initial_delay           = 10
        interval_seconds        = 30
        timeout                 = 5
        failure_count_threshold = 3
      }

      # Readiness probe: Tells Azure when this replica is ready to receive traffic
      readiness_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/health"
        interval_seconds        = 10
        timeout                 = 3
        failure_count_threshold = 3
        success_count_threshold = 1
      }

      # Startup probe: Tolerant check during initial boot (migration + seeding can be slow)
      startup_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/health"
        interval_seconds        = 5
        timeout                 = 3
        failure_count_threshold = 30  # 30 × 5s = 150s tolerance for startup
      }
    }

    # ─── HTTP Scale Rule: Scale up when concurrent requests exceed threshold
    http_scale_rule {
      name                = "http-concurrency"
      concurrent_requests = tostring(var.scale_concurrent_requests)
    }
  }

  ingress {
    allow_insecure_connections = false
    external_enabled           = true
    target_port                = 8080
    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }
}

output "api_url" {
  value = azurerm_container_app.api.latest_revision_fqdn
}
