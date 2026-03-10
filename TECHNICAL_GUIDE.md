# LexiVocab API: Technical Operations & Setup Guide

This document provides a comprehensive technical guide for developers and system administrators to deploy, configure, and maintain the LexiVocab API.

---

## 1. Architecture Overview

LexiVocab is built using **Clean Architecture** principles and **Domain-Driven Design (DDD)** concepts, ensuring high maintainability and testability.

- **LexiVocab.Domain**: Core entities, enums, and repository interfaces. No external dependencies.
- **LexiVocab.Application**: Business logic via MediatR (CQRS), DTOs, and Service interfaces.
- **LexiVocab.Infrastructure**: Implementation of data persistence (EF Core), External Services (PayPal, SePay, SMTP), and Background Processing (Hangfire).
- **LexiVocab.API**: RESTful endpoints, Middlewares, and DI container configuration.

### Key Patterns
- **CQRS**: Commands and Queries are handled separately to optimize performance.
- **Unit of Work**: Ensures transactional integrity across multiple repository operations.
- **Result Pattern**: Uniform error handling across all layers (standardized HTTP response codes).
- **Feature Gating**: Tier-based authorization driven by database definitions rather than hardcoded roles.

---

## 2. Environment Setup

### Prerequisites
- **.NET 10.0 SDK**
- **PostgreSQL 16+**
- **Redis 7+** (Optional, falls back to In-Memory)
- **SMTP Server** (Gmail App Password or Mailtrap for testing)

### Configuration (`appsettings.json`)
Ensure the following sections are correctly populated:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=lexivocab;Username=postgres;Password=your_pass",
    "Redis": "localhost:6379"
  },
  "Seapay": {
    "ApiKey": "YOUR_SEPAY_API_KEY",
    "BankAccount": "YOUR_BANK_NUM",
    "BankName": "MBBank"
  },
  "PayPal": {
    "BaseUrl": "https://api-m.sandbox.paypal.com",
    "ClientId": "...",
    "ClientSecret": "..."
  },
  "Smtp": {
    "Server": "smtp.gmail.com",
    "Port": 587,
    "Username": "..."
  }
}
```

---

## 3. Database Initialization & Seeding

The system uses Entity Framework Core for migrations. On initial startup in **Development**, migrations are applied automatically.

### Automated Seeding
The system seeds the following data via `IDataSeedContributor` patterns:
1.  **Feature Definitions**: `AI_ACCESS`, `MAX_WORDS`, `EXPORT_PDF`, etc.
2.  **Plan Definitions**:
    - **Free**: 0 VND, default tier.
    - **Premium**: 199,000 VND / Month.
    - **Business**: 999,000 VND / Year.
3.  **Plan-Feature Mappings**: Defines limits and boolean access for each tier.

### Manual Migration Commands
```bash
dotnet ef migrations add InitialCreate --project src/LexiVocab.Infrastructure --startup-project src/LexiVocab.API
dotnet ef database update --project src/LexiVocab.Infrastructure --startup-project src/LexiVocab.API
```

---

## 4. Payment Gateway Integration

### SePay (Vietnam Bank Transfer)
- **Flow**: User receives a VietQR. API waits for a webhook from SePay.
- **Webhook Target**: `POST /api/v1/payments/webhook/sepay`
- **Security Check**: The `SeapayService` validates the Bearer token in the `Authorization` header against the configured `Seapay:ApiKey`.

### PayPal (REST API)
- **Flow**: Direct Capture model.
- **Webhook Target**: `POST /api/v1/payments/webhook/paypal`
- **Verification**: The system calls the PayPal verification API to ensure the webhook payload is authentic.

---

## 5. Background Jobs & Maintenance

Leverages **Hangfire** for reliable background execution.

- **Dashboard**: Access via `/hangfire` (Admin only).
- **Subscription Expiration**: Runs daily at **00:00 UTC**. Checks for `endDate < Now` and reverts user to Free tier / sends warning email.
- **Review Reminders**: Runs daily at **01:00 UTC**. Notifies users of words due for review today.
- **Email Queue**: All transactional emails are queued to prevent API latency.

---

## 6. Security Hardening

1.  **JWT Authentication**: RSA/HMAC signed tokens with 15-minute expiry and Refresh Token rotation.
2.  **Rate Limiting**:
    - **Global**: 100 req/min per IP.
    - **Auth**: 5 req/min (Anti-Brute Force).
3.  **Request Constraints**: Max Body Size limited to 10MB to prevent DOS.
4.  **Auditing**: Every critical action (Create, Update, Delete) is logged in the `AuditLogs` table with IP, UserAgent, and TraceId.

---

## 7. Troubleshooting

- **Logs**: Persistent logs are found in the `/logs` directory (Serilog).
- **Health Checks**: Visit `/health` for real-time status of DB and Redis connections.
- **API Docs**: Interactive documentation at `/scalar/v1` (Development only).

---
*Created by LexiVocab Engineering Team - 2026*
