# LexiVocab API: Deployment Guide (Production)

This guide outlines the steps to deploy the LexiVocab API to a production environment using Docker and standard cloud infrastructure.

---

## 1. Containerized Deployment (Recommended)

The project includes a multi-stage `Dockerfile` optimized for minimal runtime footprint (~100MB).

### Build the Image
```bash
docker build -t lexivocab-api:latest .
```

### Run with Docker Compose
Create a `docker-compose.yml` to orchestrate the API, Database, and Redis.

```yaml
services:
  api:
    image: lexivocab-api:latest
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=db;Database=lexivocab;Username=postgres;Password=prod_pass
      - ConnectionStrings__Redis=redis:6379
      - Jwt__Secret=YOUR_VERY_LONG_SECURE_SECRET_HERE
      - Smtp__Password=${SMTP_PASSWORD}
    depends_on:
      - db
      - redis

  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_PASSWORD: prod_pass
      POSTGRES_DB: lexivocab

  redis:
    image: redis:7-alpine
```

---

## 2. Environment Variables & Secret Management

Do NOT store production secrets in `appsettings.json`. Use environment variables mapping to the JSON structure (use `__` as a separator).

| Setting | Variable Example | Importance |
| :--- | :--- | :--- |
| DB Connection | `ConnectionStrings__DefaultConnection` | **Critical** |
| JWT Secret | `Jwt__Secret` | **Critical** (Min 32 chars) |
| SMTP Password | `Smtp__Password` | **Critical** |
| SePay API Key | `Seapay__ApiKey` | Required for VN Bank Trans |

---

## 3. Database Migrations in Production

In production, `ASPNETCORE_ENVIRONMENT=Production` disables automatic migrations for safety. You should run migrations manually before deploying a new version.

**Option A: Using EF Core Tools (Local to Prod)**
```bash
dotnet ef database update --connection "YOUR_PROD_CONN_STRING"
```

**Option B: Migration Bundle (Best for CI/CD)**
```bash
dotnet ef migrations bundle --output efbundle
./efbundle --connection "YOUR_PROD_CONN_STRING"
```

---

## 4. Reverse Proxy Setup (Nginx)

The API is configured to trust `X-Forwarded-For` headers. Below is a sample Nginx config to handle SSL termination and proxying.

```nginx
server {
    listen 80;
    server_name api.lexivocab.store;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl;
    server_name api.lexivocab.store;

    ssl_certificate /etc/letsencrypt/live/api.lexivocab.store/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/api.lexivocab.store/privkey.pem;

    location / {
        proxy_pass         http://localhost:8080;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```

---

## 5. Post-Deployment Checklist

1.  **Check Health**: Visit `https://api.lexivocab.store/health`. All components should be "Healthy".
2.  **Verify Webhooks**: Ensure SePay/PayPal webhooks point to your production URL.
3.  **Monitor Logs**: Check the `/app/logs` directory inside the container or stream via `docker logs`.
4.  **Admin Access**: Ensure the first Admin user is created via SQL or manually assigned in the `Users` table.

---
*Created by LexiVocab DevOps - 2026*
