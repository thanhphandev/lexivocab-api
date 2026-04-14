resource "azurerm_postgresql_flexible_server" "main" {
  name                   = "psql-${var.project_name}-${var.environment}"
  resource_group_name    = azurerm_resource_group.main.name
  location               = azurerm_resource_group.main.location
  version                = "16"
  delegated_subnet_id    = azurerm_subnet.database.id
  private_dns_zone_id    = azurerm_private_dns_zone.database.id
  administrator_login    = var.db_username
  administrator_password = var.db_password
  zone                   = "1"

  storage_mb = 32768
  sku_name   = "B_Standard_B1ms" # Burstable SKU để tiết kiệm chi phí

  # Production backup: 35 days retention, geo-redundant for disaster recovery
  backup_retention_days        = 35
  geo_redundant_backup_enabled = true

  depends_on = [azurerm_private_dns_zone_virtual_network_link.database]
}

resource "azurerm_postgresql_flexible_server_database" "main" {
  name      = "lexivocab_prod"
  server_id = azurerm_postgresql_flexible_server.main.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

output "db_host" {
  value = azurerm_postgresql_flexible_server.main.fqdn
}
