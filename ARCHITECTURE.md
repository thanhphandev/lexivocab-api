# 🏗️ LexiVocab API — Architecture Overview

> **Mục đích**: Tài liệu gốc mô tả kiến trúc tổng quan. Các phần chi tiết được tách ra các file riêng trong thư mục `docs/`.

## 📚 Mục lục Tài liệu

| File | Nội dung |
|------|----------|
| **ARCHITECTURE.md** (file này) | Kiến trúc tổng quan, cấu trúc project, dependency rule, design patterns |
| [docs/BUSINESS-LOGIC.md](docs/BUSINESS-LOGIC.md) | Logic nghiệp vụ, thuật toán SM-2, cơ chế caching, audit, email, feature gating |
| [docs/DATA-FLOW.md](docs/DATA-FLOW.md) | Luồng xử lý request chi tiết qua từng layer, diagrams cho mọi tính năng |
| [docs/API-REFERENCE.md](docs/API-REFERENCE.md) | Toàn bộ endpoints, request/response format, HTTP status codes |
| [docs/TESTING.md](docs/TESTING.md) | Chiến lược testing, cách viết test mới, chạy test, cấu trúc test projects |
| [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) | Docker, CI/CD, deploy lên cloud, environment variables, monitoring |
| [docs/DEVELOPMENT-GUIDE.md](docs/DEVELOPMENT-GUIDE.md) | Hướng dẫn thêm tính năng mới, conventions, checklist phát triển |
| [docs/TERRAFORM.md](docs/TERRAFORM.md) | Infrastructure-as-Code: AWS modules, CI/CD tích hợp, cost estimates |

---

## 1. Kiến trúc: Clean Architecture + CQRS

```
┌──────────────────────────────────────────────────────────────┐
│                    🌐 API Layer                              │
│         Controllers · Middlewares · Program.cs               │
│              (Presentation & Configuration)                  │
├──────────────────────────────────────────────────────────────┤
│                  ⚙️ Application Layer                        │
│     Commands · Queries · Behaviors · DTOs · Validators       │
│              (Business Logic & Orchestration)                │
├──────────────────────────────────────────────────────────────┤
│                    💎 Domain Layer                            │
│          Entities · Enums · Repository Interfaces            │
│          (Core Business Rules — ZERO dependencies)           │
├──────────────────────────────────────────────────────────────┤
│                  🏗️ Infrastructure Layer                     │
│   EF Core · Repositories · PayPal · Email · Redis · JWT     │
│              (External Concerns & Implementations)           │
└──────────────────────────────────────────────────────────────┘
```

### Dependency Rule (BẮT BUỘC tuân thủ)

```
Domain ← Application ← Infrastructure
                     ← API
```

- **Domain**: Không import bất kỳ layer nào khác. Chứa entities, enums, repository interfaces.
- **Application**: Chỉ import Domain. Chứa handlers (CQRS), DTOs, behaviors, validators, service interfaces.
- **Infrastructure**: Import Application + Domain. Implement tất cả interfaces (repos, services, auth).
- **API**: Import Application + Infrastructure. Chỉ chứa controllers, middlewares, startup config.

---

## 2. Cấu trúc Project

```
LexiVocabAPI/
├── src/
│   ├── LexiVocab.Domain/                    # 💎 Core (ZERO dependencies)
│   │   ├── Common/
│   │   │   └── BaseEntity.cs                # Id (UUID), CreatedAt, UpdatedAt
│   │   ├── Entities/
│   │   │   ├── User.cs                      # Email, PasswordHash, Role, OAuth, RefreshToken
│   │   │   ├── UserVocabulary.cs            # ❤️ Core entity — SM-2 fields, denormalized WordText
│   │   │   ├── MasterVocabulary.cs          # Shared dictionary: phonetics, audio, PartOfSpeech
│   │   │   ├── ReviewLog.cs                 # Immutable review history (append-only)
│   │   │   ├── UserSetting.cs               # User preferences (JSONB ExcludedDomains)
│   │   │   ├── Subscription.cs              # Plan tracking (Free/Premium), start/end dates
│   │   │   ├── PaymentTransaction.cs        # Payment records (PayPal), idempotent via ExternalOrderId
│   │   │   └── AuditLog.cs                  # System-wide audit trail (KHÔNG kế thừa BaseEntity)
│   │   ├── Enums/
│   │   │   ├── UserRole.cs                  # User, Premium, Admin
│   │   │   ├── SubscriptionPlan.cs          # Free, Premium
│   │   │   ├── SubscriptionStatus.cs        # Pending, Active, Expired, Cancelled
│   │   │   ├── PaymentProvider.cs           # PayPal, Mock
│   │   │   ├── PaymentStatus.cs             # Pending, Completed, Failed, Refunded
│   │   │   ├── QualityScore.cs              # 0-5 (SM-2 quality rating)
│   │   │   └── AuditAction.cs              # Register, Login, VocabCreated, etc.
│   │   └── Interfaces/
│   │       ├── IRepository.cs               # Generic CRUD
│   │       ├── IUnitOfWork.cs               # Aggregates all repos + SaveChangesAsync
│   │       ├── IUserRepository.cs           # EmailExistsAsync, GetByAuthProviderAsync
│   │       ├── IVocabularyRepository.cs     # GetDueForReview, WordExistsForUser, GetStats
│   │       ├── IMasterVocabularyRepository.cs # GetByWord, Search, GetByWords (batch)
│   │       ├── IReviewLogRepository.cs      # Heatmap, Streak, PeriodStats
│   │       ├── ISubscriptionRepository.cs   # GetActiveByUserId
│   │       ├── IPaymentTransactionRepository.cs
│   │       └── IAuditLogRepository.cs       # GetPaged with filters
│   │
│   ├── LexiVocab.Application/              # ⚙️ Business Logic
│   │   ├── Common/
│   │   │   ├── Result.cs                    # Result<T> pattern (thay exception)
│   │   │   ├── PagedResult.cs               # Generic pagination wrapper
│   │   │   ├── Interfaces/                  # Service interfaces (13 interfaces)
│   │   │   │   ├── IJwtTokenService.cs
│   │   │   │   ├── IPasswordHasher.cs
│   │   │   │   ├── ICurrentUserService.cs
│   │   │   │   ├── IPaymentService.cs
│   │   │   │   ├── IGoogleAuthService.cs
│   │   │   │   ├── IEmailService.cs
│   │   │   │   ├── IEmailQueueService.cs
│   │   │   │   ├── IEmailTemplateService.cs
│   │   │   │   ├── IAuditLogService.cs
│   │   │   │   ├── IFeatureGatingService.cs
│   │   │   │   ├── ISrsAlgorithm.cs
│   │   │   │   ├── ICacheableQuery.cs       # Marker interface for auto-caching
│   │   │   │   └── IAuditedRequest.cs       # Marker interface for auto-audit
│   │   │   └── Behaviors/                   # MediatR Pipeline Behaviors
│   │   │       ├── AuditLoggingBehavior.cs  # ① Ghi audit log (fire-and-forget)
│   │   │       ├── CachingBehavior.cs       # ② Cache-aside cho queries
│   │   │       ├── ValidationBehavior.cs    # ③ FluentValidation auto-check
│   │   │       └── PerformanceBehavior.cs   # ④ Log warning nếu > 500ms
│   │   ├── DTOs/                            # Data Transfer Objects
│   │   │   ├── Auth/                        # AuthResponse, RegisterRequest, LoginRequest
│   │   │   ├── Vocabulary/                  # VocabularyDto, VocabularyStatsDto
│   │   │   ├── Review/                      # ReviewResultDto, ReviewSessionDto
│   │   │   ├── Analytics/                   # DashboardDto, HeatmapDto, StreakDto
│   │   │   ├── Payment/                     # BillingOverviewDto, PaymentHistoryDto
│   │   │   └── Admin/                       # UserOverviewDto, SystemStatsDto
│   │   ├── Features/                        # CQRS Handlers (Command & Query separated)
│   │   │   ├── Auth/Commands/               # Register, Login, GoogleLogin, Refresh, Logout
│   │   │   ├── Auth/Queries/                # GetCurrentUser, GetUserPermissions
│   │   │   ├── Vocabularies/Commands/       # Create, Update, Archive, Delete, BatchImport
│   │   │   ├── Vocabularies/Queries/        # GetList, GetById, GetStats, Export
│   │   │   ├── Reviews/Commands/            # SubmitReview (SM-2 calculation)
│   │   │   ├── Reviews/Queries/             # GetReviewSession, GetReviewHistory
│   │   │   ├── Analytics/                   # Dashboard, Heatmap, Streak
│   │   │   ├── Payments/                    # CreateOrder, CaptureOrder, Billing, History
│   │   │   └── Admin/                       # UserManagement, Subscriptions, SystemStats
│   │   ├── Services/
│   │   │   └── SrsAlgorithmService.cs       # SM-2 pure function (stateless)
│   │   ├── Validators/                      # FluentValidation rules per command
│   │   └── DependencyInjection.cs           # MediatR + Behaviors + Validators
│   │
│   ├── LexiVocab.Infrastructure/            # 🏗️ Implementations
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs              # 8 DbSets, auto UpdatedAt
│   │   │   └── Configurations/             # Fluent API entity configs (indexes, JSONB, BRIN)
│   │   ├── Repositories/
│   │   │   ├── GenericRepository.cs         # Base CRUD implementation
│   │   │   ├── UnitOfWork.cs               # Aggregates all repos
│   │   │   ├── VocabularyRepository.cs     # Composite indexes, batch lookups
│   │   │   ├── ReviewLogRepository.cs      # Heatmap, streak, BRIN index queries
│   │   │   └── ...                         # Other repos
│   │   ├── Services/
│   │   │   ├── PayPalService.cs            # PayPal REST API v2 (raw HttpClient)
│   │   │   ├── FeatureGatingService.cs     # Freemium quota checks
│   │   │   ├── SmtpEmailService.cs         # SMTP email delivery
│   │   │   ├── HangfireEmailQueueService.cs # Fire-and-forget email queue
│   │   │   ├── EmailTemplateService.cs     # HTML template rendering
│   │   │   ├── SubscriptionExpirationJob.cs # BackgroundService (12h cycle)
│   │   │   ├── ReviewReminderJob.cs        # BackgroundService (24h cycle)
│   │   │   └── AuditLogService.cs          # Audit log writer
│   │   ├── Auth/
│   │   │   ├── JwtTokenService.cs          # JWT generation + validation
│   │   │   ├── BcryptPasswordHasher.cs     # BCrypt password hashing
│   │   │   ├── CurrentUserService.cs       # Extract userId from HttpContext claims
│   │   │   └── GoogleAuthService.cs        # Google OAuth ID token validation
│   │   ├── EmailTemplates/                 # 6 HTML email templates
│   │   └── DependencyInjection.cs          # All infrastructure registrations
│   │
│   └── LexiVocab.API/                      # 🌐 Presentation
│       ├── Controllers/                     # 8 API Controllers
│       │   ├── AuthController.cs           # /api/v1/auth/*
│       │   ├── VocabulariesController.cs   # /api/v1/vocabularies/*
│       │   ├── ReviewsController.cs        # /api/v1/reviews/*
│       │   ├── AnalyticsController.cs      # /api/v1/analytics/*
│       │   ├── PaymentsController.cs       # /api/v1/payments/*
│       │   ├── SettingsController.cs       # /api/v1/settings/*
│       │   ├── AdminController.cs          # /api/v1/admin/* (Admin-only)
│       │   └── MasterVocabController.cs    # /api/v1/master-vocab/*
│       ├── Middlewares/
│       │   ├── GlobalExceptionMiddleware.cs # Catch-all → structured JSON errors
│       │   └── SecurityHeadersMiddleware.cs # CSP, X-Frame-Options, etc.
│       └── Program.cs                       # Bootstrap, DI, Middleware pipeline
│
├── tests/
│   ├── LexiVocab.Domain.UnitTests/          # Pure domain logic tests
│   ├── LexiVocab.Application.UnitTests/     # Handler tests (Moq)
│   ├── LexiVocab.Infrastructure.IntegrationTests/  # Real DB tests
│   └── LexiVocab.API.IntegrationTests/      # WebApplicationFactory tests
│
├── Dockerfile                               # Multi-stage Alpine build
├── docker-compose.yml                       # PostgreSQL + Redis + SEQ + pgAdmin
└── LexiVocabAPI.slnx                        # Solution file
```

---

## 3. Design Patterns sử dụng

| Pattern | Vị trí | Mục đích |
|---------|--------|----------|
| **Clean Architecture** | Toàn bộ project | Tách biệt concerns, dependency rule |
| **CQRS** | Application/Features | Commands (ghi) vs Queries (đọc) tách biệt |
| **MediatR Pipeline** | Application/Behaviors | Cross-cutting concerns (audit, cache, validation, perf) |
| **Result<T> Pattern** | Application/Common | Thay exception cho business failures |
| **Unit of Work** | Infrastructure/Repositories | Transaction management, aggregate repos |
| **Repository Pattern** | Domain/Interfaces → Infrastructure/Repos | Trừu tượng hóa data access |
| **Cache-Aside** | Application + Redis | Cache đọc → miss → DB → ghi cache |
| **Version-Busted Cache** | Vocabulary/Analytics queries | `vocab-v:{userId}` version key invalidate toàn bộ |
| **Fire-and-Forget** | AuditLogging, EmailQueue | Non-blocking background operations |
| **Idempotent Operations** | PayPal capture + webhook | `if (tx.Status == Completed) return;` |
| **Options Pattern** | Infrastructure DI | `IConfiguration` injection cho settings |
| **Factory Method** | Result<T>.Success/Failure/NotFound | Tạo result object qua static methods |

---

## 4. Technology Stack

| Thành phần | Công nghệ | Phiên bản |
|-----------|-----------|-----------|
| **Runtime** | .NET | 10.0 (Preview) |
| **Database** | PostgreSQL | 16 Alpine |
| **ORM** | Entity Framework Core | 10.0 |
| **Caching** | Redis | 7 Alpine |
| **Background Jobs** | Hangfire + BackgroundService | 1.8.23 |
| **Authentication** | JWT Bearer + Google OAuth | Custom implementation |
| **Password Hashing** | BCrypt.Net | - |
| **Logging** | Serilog → Console + File + OpenTelemetry | 9.0.0 |
| **Log Aggregator** | SEQ | Latest |
| **Validation** | FluentValidation | 12.0.0 |
| **Mediator** | MediatR | 12.5.0 |
| **API Docs** | Scalar (OpenAPI) | 2.12.46 |
| **Payment** | PayPal REST API v2 | Raw HttpClient |
| **Email** | SMTP via System.Net.Mail | Built-in |
| **Containerization** | Docker + Docker Compose | Multi-stage Alpine |
| **Resilience** | Microsoft.Extensions.Http.Resilience | Standard handler |

---

## 5. Database Schema (Entity Relationship)

```
┌─────────────────┐     1:N     ┌──────────────────┐     N:1     ┌─────────────────────┐
│      User        │────────────│  UserVocabulary   │────────────│  MasterVocabulary    │
│─────────────────│            │──────────────────│            │─────────────────────│
│ Id (UUID PK)     │            │ Id (UUID PK)      │            │ Id (UUID PK)         │
│ Email (unique)   │            │ UserId (FK)       │            │ Word (unique)        │
│ PasswordHash     │            │ MasterVocabId (FK)│            │ PhoneticUk/Us        │
│ FullName         │            │ WordText          │            │ AudioUrl             │
│ Role (enum)      │            │ CustomMeaning     │            │ PartOfSpeech         │
│ AuthProvider     │            │ ContextSentence   │            │ PopularityRank       │
│ RefreshTokenHash │            │ SourceUrl         │            └─────────────────────┘
│ PlanExpirationDt │            │ RepetitionCount   │
│ IsActive         │            │ EasinessFactor    │     1:N     ┌─────────────────┐
│ LastLogin        │            │ IntervalDays      │────────────│   ReviewLog      │
└─────────────────┘            │ NextReviewDate    │            │─────────────────│
        │                       │ LastReviewedAt    │            │ Id (UUID PK)     │
        │ 1:1                   │ IsArchived        │            │ UserVocabId (FK) │
        ▼                       └──────────────────┘            │ UserId (FK)      │
┌─────────────────┐                                              │ QualityScore     │
│   UserSetting    │                                              │ TimeSpentMs      │
│─────────────────│                                              │ ReviewedAt       │
│ Id (UUID PK)     │                                              └─────────────────┘
│ UserId (FK)      │
│ DailyGoal        │     1:N     ┌──────────────────┐     1:N     ┌───────────────────────┐
│ ExcludedDomains  │     │       │   Subscription    │────────────│  PaymentTransaction   │
│ (JSONB)          │     │       │──────────────────│            │───────────────────────│
└─────────────────┘     │       │ Id (UUID PK)      │            │ Id (UUID PK)           │
                         │       │ UserId (FK)       │            │ SubscriptionId (FK)    │
        User ────────────┘       │ Plan (enum)       │            │ UserId (FK)            │
                                 │ Status (enum)     │            │ ExternalOrderId (uniq) │
                                 │ StartDate         │            │ Amount / Currency      │
                                 │ EndDate (nullable) │            │ Status (enum)          │
                                 │ Provider (enum)    │            │ PaidAt                 │
                                 └──────────────────┘            │ RawPayload             │
                                                                  └───────────────────────┘

┌───────────────────────────────────────────────┐
│                 AuditLog                       │
│ (KHÔNG kế thừa BaseEntity — append-only)       │
│───────────────────────────────────────────────│
│ Id (UUID PK)  │ UserId       │ UserEmail      │
│ Action (enum) │ EntityType   │ EntityId       │
│ OldValues     │ NewValues    │ RequestPayload │
│ IpAddress     │ UserAgent    │ TraceId        │
│ DurationMs    │ IsSuccess    │ Timestamp      │
└───────────────────────────────────────────────┘
```

### Chiến lược Index quan trọng:

| Table | Index | Loại | Mục đích |
|-------|-------|------|----------|
| `UserVocabulary` | `(UserId, NextReviewDate, IsArchived)` | B-tree Composite | Query "due for review" sub-ms |
| `UserVocabulary` | `(UserId, WordText)` | Unique (CI) | Prevent duplicate words per user |
| `ReviewLog` | `ReviewedAt` | BRIN | Efficient time-series analytics |
| `User` | `Email` | Unique | Login lookup |
| `PaymentTransaction` | `ExternalOrderId` | Unique | Idempotent payment processing |

---

## 6. Middleware Pipeline (thứ tự thực thi)

```
HTTP Request
    │
    ▼
① GlobalExceptionMiddleware     ← Catch-all → structured JSON error
    │
    ▼
② SecurityHeadersMiddleware     ← CSP, X-Frame-Options, HSTS, etc.
    │
    ▼
③ HTTPS Redirection (Prod)      ← Force HTTPS
    │
    ▼
④ Response Compression          ← Brotli > Gzip
    │
    ▼
⑤ Serilog Request Logging       ← Log every request
    │
    ▼
⑥ CORS                          ← Allow frontend origins
    │
    ▼
⑦ Rate Limiter                  ← Global: 100/min, Auth: 5/min
    │
    ▼
⑧ Authentication (JWT Bearer)   ← Validate token, set ClaimsPrincipal
    │
    ▼
⑨ Authorization                 ← Check [Authorize], Roles, Policies
    │
    ▼
⑩ Routing → Controllers         ← MediatR dispatch
    │
    ▼
HTTP Response (processed in reverse through ①–⑨)
```

---

## Tiếp theo

Đọc các file chi tiết trong `docs/` để hiểu sâu từng phần:
- [Logic nghiệp vụ & thuật toán →](docs/BUSINESS-LOGIC.md)
- [Luồng xử lý dữ liệu →](docs/DATA-FLOW.md)
- [API Reference →](docs/API-REFERENCE.md)
- [Testing →](docs/TESTING.md)
- [Deployment →](docs/DEPLOYMENT.md)
- [Hướng dẫn phát triển →](docs/DEVELOPMENT-GUIDE.md)
- [Terraform IaC →](docs/TERRAFORM.md)
