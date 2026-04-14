output "resource_group_name" {
  value = azurerm_resource_group.main.name
}

output "log_analytics_workspace_name" {
  value = azurerm_log_analytics_workspace.main.name
}

output "container_app_environment_name" {
  value = azurerm_container_app_environment.main.name
}

output "container_app_name" {
  value = azurerm_container_app.api.name
}

output "api_fqdn" {
  value = azurerm_container_app.api.latest_revision_fqdn
}
