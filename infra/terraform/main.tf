terraform {
  required_version = ">= 1.5.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.0"
    }
  }

  # Partial backend config — sensitive values passed via:
  #   terraform init -backend-config="resource_group_name=..." \
  #                  -backend-config="storage_account_name=..." \
  #                  -backend-config="container_name=tfstate" \
  #                  -backend-config="key=terraform.tfstate"
  backend "azurerm" {
    container_name = "tfstate"
    key            = "terraform.tfstate"
  }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy = false
    }
  }
}
