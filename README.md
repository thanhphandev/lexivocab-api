# LexiVocab API - High-Performance Language Learning Backend

LexiVocab API is a robust, scalable backend service built with **.NET 10**, following the principles of **Clean Architecture**. It serves as the core engine for the LexiVocab ecosystem, handling AI-driven translations, Spaced Repetition (SRS) logic, and secure user management.

## 🏗️ Architecture & Design

- **Clean Architecture**: Separation of concerns across Domain, Application, Infrastructure, and API layers.
- **DDD Patterns**: Implementation of Aggregate Roots, Entities, and Value Objects.
- **CQRS**: Command Query Responsibility Segregation for optimized data flow.
- **Advanced Migration Engine**: Uses PostgreSQL **Advisory Locks** to ensure safe, concurrent database migrations across multiple cloud replicas.

## 🚀 Key Features

- **AI Translation Engine**: Integrated with multiple AI providers for streaming translations.
- **SRS Engine**: Implementation of the SM-2 algorithm for optimized learning.
- **Real-time Monitoring**: Full observability with **OpenTelemetry** and health checks.
- **Distributed Caching**: High-speed performance using **Redis**.
- **Background Jobs**: Reliable asynchronous processing with **Hangfire**.
- **Security**: Hardened JWT-based authentication and Role-Based Access Control (RBAC).

## 🛠️ Tech Stack

- **Framework**: [.NET 10](https://dotnet.microsoft.com/)
- **Database**: [PostgreSQL](https://www.postgresql.org/) with [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- **Cache**: [Redis](https://redis.io/)
- **Background Tasks**: [Hangfire](https://www.hangfire.io/)
- **Testing**: [xUnit](https://xunit.net/), [FluentAssertions](https://fluentassertions.com/), and [Testcontainers](https://testcontainers.com/) for PostgreSQL integration tests.
- **API Documentation**: [Scalar](https://scalar.com/) & [OpenApi](https://swagger.io/)

## 📦 Installation & Setup

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Required for running Redis and Integration Tests)

### 1. Clone the repository
```bash
git clone https://github.com/thanhphandev/lexivocab-api.git
cd LexiVocabAPI
```

### 2. Configure Environment
Copy `.env.example` to `.env` and update your connection strings:
```bash
cp .env.example .env
```

### 3. Start Infrastructure (Optional - Local Docker)
If you don't have local Postgres/Redis, use the provided docker-compose:
```bash
docker-compose up -d
```

### 4. Run the API
```bash
dotnet run --project src/LexiVocab.API
```
The API will be available at `http://localhost:5000`. Documentation (Scalar) can be found at `/scalar/v1`.

## 🧪 Running Tests

The project includes a comprehensive test suite. Integration tests require Docker to be running as they use **Testcontainers** for a real PostgreSQL instance.

```bash
# Run all tests
dotnet test
```

---
Developed by [Phan Văn Thành](https://github.com/thanhphandev)
