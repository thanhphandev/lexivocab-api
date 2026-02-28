# LexiVocab API — Kiến trúc & Tài liệu kỹ thuật toàn diện

> **Mục đích**: Tài liệu dành cho bảo trì và phát triển tính năng mới. Đọc tài liệu này là đủ để hiểu toàn bộ cấu trúc, flows, conventions và cách thêm feature.

---

## Mục lục

1. [Tổng quan Kiến trúc](#1-tổng-quan-kiến-trúc)
2. [Dependency & Project References](#2-dependency--project-references)
3. [Domain Layer – Lõi hệ thống](#3-domain-layer--lõi-hệ-thống)
4. [Application Layer – Business Logic](#4-application-layer--business-logic)
5. [Infrastructure Layer – Implementations](#5-infrastructure-layer--implementations)
6. [API Layer – Presentation](#6-api-layer--presentation)
7. [Luồng xử lý Request (End-to-End)](#7-luồng-xử-lý-request-end-to-end)
8. [Hệ thống SRS (Spaced Repetition)](#8-hệ-thống-srs-spaced-repetition)
9. [Hệ thống Authentication](#9-hệ-thống-authentication)
10. [Hệ thống Payment & Subscription](#10-hệ-thống-payment--subscription)
11. [Freemium & Feature Gating](#11-freemium--feature-gating)
12. [Database Schema & Indexing Strategy](#12-database-schema--indexing-strategy)
13. [Error Handling Strategy](#13-error-handling-strategy)
14. [Conventions & Patterns](#14-conventions--patterns)
15. [Hướng dẫn thêm Feature mới](#15-hướng-dẫn-thêm-feature-mới)

---

## 1. Tổng quan Kiến trúc

```
┌─────────────────────────────────────────────────────────────────┐
│                    LexiVocab.API (Presentation)                  │
│  Controllers • Middleware • Program.cs • DI Configuration        │
│                           ↓ depends on                           │
├─────────────────────────────────────────────────────────────────┤
│              LexiVocab.Application (Business Logic)              │
│  CQRS Handlers • DTOs • Behaviors • Service Interfaces • Result  │
│                           ↓ depends on                           │
├─────────────────────────────────────────────────────────────────┤
│                 LexiVocab.Domain (Core / Kernel)                 │
│  Entities • Enums • Repository Interfaces • BaseEntity           │
├─────────────────────────────────────────────────────────────────┤
│              LexiVocab.Infrastructure (Implementations)          │
│  EF Core • Repositories • JWT • BCrypt • PayPal • Google Auth    │
│                    ↑ depends on Application + Domain              │
└─────────────────────────────────────────────────────────────────┘
```

**Kiến trúc**: Clean Architecture + CQRS (Command Query Responsibility Segregation)

**Nguyên tắc cốt lõi**:
- **Dependency Rule**: Domain không phụ thuộc bất kỳ layer nào. Application phụ thuộc Domain. Infrastructure phụ thuộc Application + Domain. API phụ thuộc tất cả.
- **CQRS via MediatR**: Tách biệt Command (ghi) và Query (đọc). Mỗi use case = 1 handler riêng biệt.
- **Result\<T\> thay Exception**: Business logic KHÔNG throw exception. Dùng `Result<T>` wrapper để truyền success/failure.

---

## 2. Dependency & Project References

```
API ──→ Application ──→ Domain
 │           ↑
 └──→ Infrastructure ──┘
```

| Project | References | NuGet Packages chính |
|---------|-----------|---------------------|
| **Domain** | _(không phụ thuộc gì)_ | – |
| **Application** | Domain | MediatR, FluentValidation |
| **Infrastructure** | Application, Domain | EF Core, Npgsql, BCrypt, JWT Bearer, StackExchange.Redis, Serilog |
| **API** | Application, Infrastructure | Scalar (API docs) |

---

## 3. Domain Layer – Lõi hệ thống

### 3.1. BaseEntity

Tất cả entity kế thừa `BaseEntity`:

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();     // UUID — anti-enumeration, distributed-friendly
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
```

**Tại sao dùng UUID?** Ngăn chặn enumeration attack (không đoán được ID tiếp theo), hỗ trợ sharding/phân tán sau này.

### 3.2. Entities (7 bảng)

```
┌──────────────────┐     1:N     ┌──────────────────┐     N:1     ┌──────────────────┐
│       User       │────────────▶│  UserVocabulary   │◀───────────│ MasterVocabulary │
│                  │             │  (BẢNG LỚN NHẤT) │             │  (Từ điển chung) │
│ • Email          │             │ • WordText (denorm)│            │ • Word (unique)  │
│ • PasswordHash   │             │ • SM-2 fields     │            │ • PhoneticUK/US  │
│ • Role           │             │ • NextReviewDate  │            │ • AudioUrl       │
│ • AuthProvider   │             │ • IsArchived      │            │ • PopularityRank │
│ • RefreshToken   │             └────────┬─────────┘            └──────────────────┘
│ • PlanExpiration │                      │ 1:N
└────────┬─────────┘                      ▼
    │ 1:1   1:N                  ┌──────────────────┐
    │    │                       │    ReviewLog      │
    ▼    │                       │  (APPEND-ONLY)    │
┌────────┴───────┐               │ • QualityScore    │
│  UserSetting   │               │ • TimeSpentMs     │
│ • Highlight    │               │ • ReviewedAt      │
│ • ExcludedDoms │               │   (BRIN indexed)  │
│ • DailyGoal   │               └──────────────────┘
└────────────────┘
    
    1:N                          1:N
User ──────▶ Subscription ──────▶ PaymentTransaction
             • Plan                • ExternalOrderId (UNIQUE)
             • Status              • Amount, Currency
             • Provider            • RawPayload (audit)
```

#### Chi tiết từng Entity:

| Entity | Vai trò | Số dòng dự kiến | Ghi chú quan trọng |
|--------|---------|-----------------|-------------------|
| **User** | Tài khoản người dùng | ~10K | `PasswordHash` = null nếu OAuth. `PlanExpirationDate` = denormalized từ Subscription |
| **UserVocabulary** | Flashcard của user (trái tim hệ thống ❤️) | **~Hàng triệu** | Chứa SM-2 fields. `WordText` denormalized để tránh JOIN |
| **MasterVocabulary** | Từ điển chung (shared) | ~100K | `Word` có unique index. Tránh lưu trùng lặp phiên âm cho mỗi user |
| **ReviewLog** | Log ôn tập (append-only) | **~Hàng trăm triệu** | `UserId` denormalized. BRIN index trên `ReviewedAt` cho time-series queries |
| **Subscription** | Gói cước của user | ~10K | Chỉ 1 active tại bất kỳ thời điểm nào |
| **PaymentTransaction** | Giao dịch thanh toán | ~10K | `ExternalOrderId` = UNIQUE → idempotency key |
| **UserSetting** | Cài đặt Extension (1:1 User) | ~10K | `ExcludedDomains` lưu JSONB |

### 3.3. Enums (6)

| Enum | Values | Sử dụng |
|------|--------|---------|
| `UserRole` | User(0), Premium(1), Admin(2) | Role-based authorization |
| `QualityScore` | CompleteBlackout(0) → Perfect(5) | SM-2 input — user tự đánh giá mức nhớ |
| `SubscriptionPlan` | Free(0), Premium(1) | Loại gói |
| `SubscriptionStatus` | Active, Expired, Cancelled | Trạng thái gói |
| `PaymentProvider` | Mock, PayPal | Cổng thanh toán |
| `PaymentStatus` | Pending, Completed, Failed, Refunded | Trạng thái giao dịch |

### 3.4. Repository Interfaces (8)

```
IRepository<T>                          ← Generic CRUD (GetById, Add, Update, Remove, Count, Exists)
├── IUserRepository                     ← GetByEmail, GetByAuthProvider, EmailExists
├── IVocabularyRepository               ← GetPaginated, GetDueForReview, GetStats, GetByWord
├── IReviewLogRepository                ← GetHeatmap, GetStreak, GetPeriodStats
├── IMasterVocabularyRepository         ← LookupByWord, Search
├── ISubscriptionRepository             ← GetActiveByUserId
└── IPaymentTransactionRepository       ← GetPaginatedByUser, CountByUser

IUnitOfWork                             ← Tổ hợp tất cả repositories + SaveChangesAsync
```

**Quy tắc**: Handlers chỉ truy cập DB thông qua `IUnitOfWork`. KHÔNG inject `AppDbContext` trực tiếp trong Application layer.

---

## 4. Application Layer – Business Logic

### 4.1. Cấu trúc thư mục

```
Application/
├── Common/
│   ├── Behaviors/
│   │   ├── ValidationBehavior.cs       ← Auto-validate requests trước khi handler chạy
│   │   └── PerformanceBehavior.cs      ← Log slow requests > 500ms
│   ├── Interfaces/                     ← 7 service interfaces (xem bên dưới)
│   ├── Result.cs                       ← Result<T> wrapper
│   └── PagedResult.cs                  ← Pagination wrapper
├── DTOs/
│   ├── Auth/AuthDtos.cs
│   ├── Vocabulary/VocabularyDtos.cs
│   ├── Analytics/AnalyticsDtos.cs
│   ├── Review/ReviewDtos.cs
│   ├── Settings/SettingsDtos.cs
│   └── Payment/PaymentDtos.cs
├── Features/
│   ├── Auth/
│   │   ├── Commands/AuthCommands.cs    ← Register, Login, GoogleLogin, RefreshToken
│   │   └── Queries/AuthQueries.cs      ← GetCurrentUser, GetUserPermissions
│   ├── Vocabularies/
│   │   ├── Commands/VocabularyCommands.cs ← Create, Update, Archive, Delete, BatchImport
│   │   └── Queries/VocabularyQueries.cs   ← GetList, GetById, GetStats, Export
│   ├── Reviews/
│   │   ├── Commands/ReviewCommands.cs  ← SubmitReview (triggers SM-2 recalculation)
│   │   └── Queries/ReviewQueries.cs    ← GetReviewSession, GetReviewHistory
│   ├── Analytics/AnalyticsHandlers.cs  ← Dashboard, Heatmap, Streak
│   ├── Settings/SettingsHandlers.cs    ← Get/Update user settings
│   ├── MasterVocabularies/Queries/     ← Lookup, Search master dictionary
│   └── Payments/
│       ├── PaymentQueries.cs           ← GetBillingOverview, GetPaymentHistory
│       └── PaymentCommands.cs          ← CreatePaymentOrder, CapturePaymentOrder
├── Services/
│   └── SrsAlgorithmService.cs          ← SM-2 implementation (pure calculation)
└── DependencyInjection.cs              ← AddApplication() extension method
```

### 4.2. MediatR Pipeline (thứ tự chạy)

```
HTTP Request
    │
    ▼
Controller          ← Nhận request, mapping sang Command/Query, gửi MediatR
    │
    ▼
┌───────────────────────────────────────────────────┐
│              MediatR Pipeline                      │
│                                                    │
│  1️⃣  PerformanceBehavior                          │
│     └─ Bắt đầu Stopwatch                          │
│                                                    │
│  2️⃣  ValidationBehavior                           │
│     └─ Chạy TẤT CẢ FluentValidation validators    │
│     └─ Nếu lỗi → throw ValidationException        │
│        → bị bắt bởi GlobalExceptionMiddleware      │
│        → trả 400 Bad Request với chi tiết lỗi      │
│                                                    │
│  3️⃣  Handler (Command/Query)                      │
│     └─ ICurrentUserService → extract user từ JWT   │
│     └─ IFeatureGatingService → check quota         │
│     └─ IUnitOfWork → repository operations         │
│     └─ Return Result<T>                            │
│                                                    │
│  4️⃣  PerformanceBehavior (sau khi handler xong)   │
│     └─ Nếu > 500ms → log warning ⚠️               │
└───────────────────────────────────────────────────┘
    │
    ▼
Controller.ToActionResult(result)
    │
    ▼
HTTP Response { success: true/false, data/error }
```

### 4.3. Result\<T\> Pattern

```csharp
// ✅ Trả kết quả thành công
return Result<VocabularyDto>.Success(dto);          // 200 OK
return Result<VocabularyDto>.Created(dto);           // 201 Created

// ❌ Trả lỗi business logic (KHÔNG throw exception)
return Result<VocabularyDto>.Failure("Invalid input", 400);
return Result<VocabularyDto>.NotFound("Vocabulary not found");
return Result<VocabularyDto>.Unauthorized();
return Result<VocabularyDto>.Forbidden();
return Result<VocabularyDto>.Conflict("Word already exists");

// Controller mapping
private IActionResult ToActionResult<T>(Result<T> result)
{
    if (result.IsSuccess)
        return StatusCode(result.StatusCode, new { success = true, data = result.Data });
    return StatusCode(result.StatusCode, new { success = false, error = result.Error });
}
```

**Khi nào dùng Exception vs Result?**
- `Result<T>` → **Business logic failures** (user not found, quota exceeded, invalid data)
- `throw Exception` → **Unexpected system errors** (DB down, network timeout, null reference)
- Middleware `GlobalExceptionMiddleware` bắt tất cả exceptions chưa handle

### 4.4. Service Interfaces (7)

| Interface | Mô tả | Implementation |
|-----------|--------|---------------|
| `ICurrentUserService` | Extract UserId/Email từ JWT HttpContext | `CurrentUserService` (Infrastructure) |
| `IJwtTokenService` | Tạo/validate Access + Refresh tokens | `JwtTokenService` (Infrastructure) |
| `IPasswordHasher` | Hash/verify passwords (BCrypt) | `BcryptPasswordHasher` (Infrastructure) |
| `IGoogleAuthService` | Validate Google ID token | `GoogleAuthService` (Infrastructure) |
| `IFeatureGatingService` | Check quotas, permissions, premium status | `FeatureGatingService` (Infrastructure) |
| `IPaymentService` | Create/capture payment orders, verify webhooks | `PayPalService` (Infrastructure) |
| `ISrsAlgorithm` | SM-2 calculation (pure function) | `SrsAlgorithmService` (Application) |

---

## 5. Infrastructure Layer – Implementations

### 5.1. Cấu trúc thư mục

```
Infrastructure/
├── Authentication/
│   ├── CurrentUserService.cs           ← Extract user từ HttpContext.User claims
│   ├── JwtTokenService.cs             ← JWT generation + validation
│   ├── BcryptPasswordHasher.cs        ← BCrypt hashing
│   └── GoogleAuthService.cs           ← Google OAuth token validation
├── Persistence/
│   ├── AppDbContext.cs                 ← EF Core DbContext (7 DbSets)
│   └── Configurations/                ← 7 Fluent API configs (snake_case columns, indexes)
│       ├── UserConfiguration.cs
│       ├── UserVocabularyConfiguration.cs
│       ├── MasterVocabularyConfiguration.cs
│       ├── ReviewLogConfiguration.cs
│       ├── SubscriptionConfiguration.cs
│       ├── PaymentTransactionConfiguration.cs
│       └── UserSettingConfiguration.cs
├── Repositories/
│   ├── GenericRepository<T>.cs         ← Base CRUD implementation
│   ├── UserRepository.cs
│   ├── VocabularyRepository.cs
│   ├── ReviewLogRepository.cs
│   ├── MasterVocabularyRepository.cs
│   ├── SubscriptionRepository.cs
│   ├── PaymentTransactionRepository.cs
│   └── UnitOfWork.cs                  ← Coordinates all repos + SaveChangesAsync
├── Services/
│   ├── FeatureGatingService.cs        ← Quota enforcement
│   └── PayPalService.cs               ← PayPal REST API integration
├── Migrations/                         ← EF Core migration files
└── DependencyInjection.cs             ← AddInfrastructure() extension method
```

### 5.2. DI Registration Chain

```csharp
// Program.cs
builder.Services.AddApplication();          // MediatR + FluentValidation + SRS
builder.Services.AddInfrastructure(config);  // EF Core + Repos + Auth + Payment + Redis

// AddApplication() registers:
//   - MediatR (scan Application assembly) + Pipeline Behaviors
//   - FluentValidation (scan Application assembly)
//   - ISrsAlgorithm → SrsAlgorithmService (Singleton)

// AddInfrastructure() registers:
//   - AppDbContext → PostgreSQL (Npgsql, retry on failure)
//   - 6 Repositories (Scoped) + UnitOfWork
//   - Auth services: IJwtTokenService, IPasswordHasher, ICurrentUserService
//   - IFeatureGatingService, IPaymentService (PayPal HttpClient)
//   - IGoogleAuthService (Google HttpClient)
//   - JWT Bearer authentication
//   - Authorization policies ("RequirePremium" → Role Premium|Admin)
//   - Redis distributed cache (fallback: in-memory)
```

---

## 6. API Layer – Presentation

### 6.1. Controllers (7)

| Controller | Route | Auth | Endpoints | Mô tả |
|-----------|-------|------|-----------|-------|
| **AuthController** | `/api/auth` | Mixed | 6 | Register, Login, GoogleLogin, RefreshToken, GetMe, GetPermissions |
| **VocabulariesController** | `/api/vocabularies` | `[Authorize]` | 8 | CRUD + Batch Import + Stats + Export (Premium) |
| **ReviewsController** | `/api/reviews` | `[Authorize]` | 3 | GetSession, SubmitReview, GetHistory |
| **AnalyticsController** | `/api/analytics` | `[Authorize]` | 3 | Dashboard, Heatmap, Streak |
| **SettingsController** | `/api/settings` | `[Authorize]` | 2 | Get/Update extension settings |
| **MasterVocabController** | `/api/master-vocab` | `[Authorize]` | 2 | Lookup, Search dictionary |
| **PaymentsController** | `/api/payments` | `[Authorize]` | 5 | Billing, History, CreateOrder, CaptureOrder, Webhook |

**Convention mỗi controller đều tuân thủ**:
- Kế thừa `ControllerBase` 
- Attributes: `[ApiController]`, `[Route("api/[controller]")]`, `[Authorize]`, `[Produces("application/json")]`
- Inject **chỉ** `IMediator` (trừ PaymentsController cần thêm cho webhook)
- Method `ToActionResult<T>(Result<T>)` — mapping chuẩn result → HTTP response
- `[AllowAnonymous]` cho các endpoint public (register, login, webhook)

### 6.2. Middleware Pipeline (Program.cs)

```
Request
  │
  ▼
GlobalExceptionMiddleware      ← Bắt mọi exception, trả JSON error response
  │
  ▼
SerilogRequestLogging          ← Log mọi request (method, path, status, duration)
  │
  ▼
HTTPS Redirection
  │
  ▼
CORS ("LexiVocabPolicy")      ← Allow origins: localhost:3000, localhost:5173, chrome-extension://*
  │
  ▼
Rate Limiter                   ← 100 requests/phút per IP (FixedWindow)
  │
  ▼
Authentication (JWT Bearer)
  │
  ▼
Authorization                  ← Role checks, "RequirePremium" policy
  │
  ▼
Controller → MediatR Pipeline → Handler → Result<T>
  │
  ▼
Response
```

### 6.3. GlobalExceptionMiddleware

Mapping exception → HTTP status code:

| Exception Type | HTTP Status | Khi nào xảy ra |
|---------------|------------|-----------------|
| `ValidationException` (FluentValidation) | 400 Bad Request | Input không hợp lệ |
| `UnauthorizedAccessException` | 401 Unauthorized | Thiếu/sai auth |
| `KeyNotFoundException` | 404 Not Found | Resource không tồn tại |
| `OperationCanceledException` | 400 Bad Request | Client cancel request |
| Mọi exception khác | 500 Internal Error | Lỗi không dự đoán |

Response format:
```json
{
    "success": false,
    "error": "sanitized message (không lộ stack trace)",
    "statusCode": 400,
    "traceId": "unique-correlation-id",
    "timestamp": "2026-02-26T..."
}
```

---

## 7. Luồng xử lý Request (End-to-End)

### 7.1. Ví dụ: Tạo từ vựng mới (POST `/api/vocabularies`)

```
1. Client gửi POST /api/vocabularies
   Body: { "wordText": "ubiquitous", "customMeaning": "có mặt ở khắp nơi" }

2. Rate Limiter → Check IP quota (100/min)

3. JWT Bearer → Validate access_token
   → Extract: UserId=abc, Email=user@test.com, Role=User

4. VocabulariesController.Create()
   → Map sang CreateVocabularyCommand("ubiquitous", "có mặt ở khắp nơi", null, null)
   → _mediator.Send(command)

5. MediatR Pipeline:
   │
   ├─ PerformanceBehavior: Bắt đầu Stopwatch ⏱️
   │
   ├─ ValidationBehavior: Chạy CreateVocabularyValidator
   │  → Check: WordText required, max 100 chars, etc.
   │  → Nếu fail → throw ValidationException → 400
   │
   └─ CreateVocabularyHandler.Handle():
      │
      ├─ ICurrentUserService.UserId → lấy user ID từ JWT claim
      │
      ├─ IFeatureGatingService.CanCreateVocabularyAsync()
      │  → Query: Đếm số từ hiện tại
      │  → Free user: limit 500 từ. Premium: unlimited
      │  → Nếu chạm limit → return Result.Failure("Quota exceeded", 403)
      │
      ├─ IUnitOfWork.Vocabularies.GetByWordAsync(userId, "ubiquitous")
      │  → Kiểm tra duplicate
      │  → Nếu đã tồn tại → return Result.Conflict("Word already exists")
      │
      ├─ IUnitOfWork.MasterVocabularies.LookupByWordAsync("ubiquitous")
      │  → Tìm trong từ điển chung → Lấy phonetics, audio
      │
      ├─ new UserVocabulary { WordText, CustomMeaning, ... }
      │  → SM-2 defaults: RepetitionCount=0, EasinessFactor=2.5, NextReviewDate=now
      │
      ├─ IUnitOfWork.Vocabularies.AddAsync(entity)
      ├─ IUnitOfWork.SaveChangesAsync() → Persist to PostgreSQL
      │
      └─ return Result.Created(vocabularyDto)

6. ToActionResult(result) → HTTP 201 Created
   { "success": true, "data": { "id": "...", "wordText": "ubiquitous", ... } }
```

### 7.2. Ví dụ: Submit Review (POST `/api/reviews`)

```
1. Client gửi body: { userVocabularyId: "...", qualityScore: 4, timeSpentMs: 1200 }

2. SubmitReviewHandler:
   ├─ Tìm UserVocabulary theo ID
   ├─ Gọi ISrsAlgorithm.Calculate(currentRep, currentEF, currentInterval, quality=4)
   │
   │   SM-2 Calculation:
   │   ├─ EF' = 2.5 + (0.1 - (5-4) * (0.08 + (5-4) * 0.02)) = 2.5 + 0.0 = 2.5
   │   ├─ rep = 1 + 1 = 2 → interval = 6 days
   │   └─ NextReviewDate = now + 6 days
   │
   ├─ Update UserVocabulary: rep=2, EF=2.5, interval=6, NextReviewDate=+6d
   ├─ Tạo ReviewLog mới (append-only, không update/delete)
   └─ SaveChangesAsync
```

---

## 8. Hệ thống SRS (Spaced Repetition)

### Thuật toán SM-2 (SuperMemo-2)

```
Input:
  - QualityScore q (0-5): User tự đánh giá mức độ nhớ
  - Current: RepetitionCount, EasinessFactor (EF), IntervalDays

Formula:
  EF' = EF + (0.1 - (5 - q) * (0.08 + (5 - q) * 0.02))
  EF' = max(1.3, EF')

  if q < 3:            ← Trả lời sai → reset
    rep = 0
    interval = 1 day
  else:                 ← Trả lời đúng → advance
    rep += 1
    interval = rep == 1 ? 1 : rep == 2 ? 6 : ceil(prev_interval × EF')

  NextReviewDate = now + interval days
```

**Ý nghĩa các QualityScore:**

| Score | Tên | Ý nghĩa | Hệ quả SRS |
|-------|-----|---------|-------------|
| 0 | CompleteBlackout | Không nhớ gì | Reset hoàn toàn, ôn lại ngày mai |
| 1 | IncorrectButRecognized | Sai nhưng nhận ra đáp án | Reset, ôn lại ngày mai |
| 2 | IncorrectButEasyRecall | Sai nhưng thấy dễ nhớ lại | Reset, ôn lại ngày mai |
| 3 | CorrectWithDifficulty | Đúng nhưng khó nhớ | Interval tăng ít |
| 4 | CorrectWithHesitation | Đúng, hơi do dự | Interval tăng trung bình |
| 5 | Perfect | Nhớ ngay lập tức | Interval tăng mạnh |

**Implementation**: `SrsAlgorithmService.cs` — pure function, no side effects, dễ unit test.

---

## 9. Hệ thống Authentication

### 9.1. Luồng Email/Password

```
Register:
  Email + Password → BcryptHash(password) → Save User(Role=User)
  → Generate AccessToken (JWT, 15min) + RefreshToken (random, 7d)
  → Store BCryptHash(refreshToken) vào User.RefreshTokenHash
  → Return tokens

Login:
  Email + Password → Find User → BCrypt.Verify(password, hash)
  → Generate new token pair → Return
```

### 9.2. Luồng Google OAuth

```
1. Frontend: Google Sign-In SDK → nhận ID Token
2. POST /api/auth/google { idToken: "..." }
3. GoogleAuthService.ValidateTokenAsync(idToken)
   → Gọi Google API validate token → nhận email, name, sub
4. Tìm User theo AuthProvider="Google" + AuthProviderId=sub
   → Nếu tìm thấy → Login
   → Nếu không → Tìm theo email → Nếu có → Link Google account
   → Nếu không → Tạo User mới (PasswordHash=null)
5. Generate JWT tokens → Return
```

### 9.3. JWT Token Rotation

```
AccessToken:
  - Short-lived: 15 phút (configurable)
  - Payload: { sub: userId, email, role, exp }
  - Dùng cho mọi API call

RefreshToken:
  - Long-lived: 7 ngày (configurable)
  - Random string, KHÔNG phải JWT
  - BCrypt hash lưu trong User.RefreshTokenHash
  - Mỗi lần refresh → tạo refresh token MỚI → hash MỚI (rotation)
  
Tại sao rotation?
  → Nếu refresh token bị leak, attacker chỉ dùng được 1 lần
  → Lần refresh tiếp theo, token cũ đã invalid
```

---

## 10. Hệ thống Payment & Subscription

### 10.1. Luồng thanh toán PayPal

```
┌─────────┐      ┌──────────────┐      ┌──────────┐      ┌──────────┐
│ Frontend │      │  LexiVocab   │      │  PayPal  │      │  PayPal  │
│          │      │  API         │      │  API     │      │  Checkout│
└────┬─────┘      └──────┬───────┘      └────┬─────┘      └────┬─────┘
     │                   │                    │                  │
     │ 1. POST create-order                   │                  │
     │──────────────────▶│                    │                  │
     │                   │ 2. CreateOrder API  │                  │
     │                   │───────────────────▶│                  │
     │                   │◀── approvalUrl ────│                  │
     │◀── approvalUrl ──│                    │                  │
     │                   │                    │                  │
     │ 3. Redirect user ─────────────────────────────────────────▶
     │                   │                    │    User approves │
     │◀── redirected with orderID ───────────────────────────────│
     │                   │                    │                  │
     │ 4. POST capture-order                  │                  │
     │──────────────────▶│                    │                  │
     │                   │ 5. CaptureOrder    │                  │
     │                   │───────────────────▶│                  │
     │                   │◀── success ────────│                  │
     │                   │                    │                  │
     │                   │ 6. Update DB:      │                  │
     │                   │ • User.Role=Premium│                  │
     │                   │ • Create Subscription                 │
     │                   │ • Create PaymentTransaction           │
     │                   │                    │                  │
     │◀── { success } ──│                    │                  │
```

### 10.2. Idempotency

`PaymentTransaction.ExternalOrderId` có **UNIQUE index**. Nếu webhook PayPal gửi lại nhiều lần (retry do timeout), DB sẽ reject duplicate insert → không tạo double order.

### 10.3. Provider abstraction

```csharp
public interface IPaymentService
{
    PaymentProvider Provider { get; }
    Task<string> CreateOrderAsync(Guid userId, string planId, CancellationToken ct);
    Task<bool> CaptureOrderAsync(string orderId, Guid userId, CancellationToken ct);
    Task<bool> VerifyWebhookSignatureAsync(string body, IDictionary<string, string> headers);
}
```

Muốn thêm **Stripe**? Tạo `StripeService : IPaymentService`, đổi DI registration. Business logic không đổi.

---

## 11. Freemium & Feature Gating

```csharp
public interface IFeatureGatingService
{
    Task<bool> CanCreateVocabularyAsync(Guid userId, CancellationToken ct);  // Check quota
    Task<UserPermissionsDto> GetPermissionsAsync(Guid userId, CancellationToken ct);
    Task<bool> IsPremiumAsync(Guid userId, CancellationToken ct);           // Quick check
}
```

**Cách xác định Premium**: `User.Role == Premium || Admin` HOẶC có `Subscription` active chưa hết hạn.

**Giới hạn Free tier** (định nghĩa trong FeatureGatingService):

| Feature | Free | Premium |
|---------|------|---------|
| Tổng số từ vựng | 500 | Unlimited |
| Export (CSV/JSON) | ❌ | ✅ |
| Batch Import | ❌ | ✅ |
| Tất cả features khác | ✅ | ✅ |

---

## 12. Database Schema & Indexing Strategy

### 12.1. EF Core Configuration

- **Column naming**: `snake_case` (PostgreSQL convention)
- **Table naming**: Plural `snake_case` (e.g., `user_vocabularies`)
- **UUID primary keys**: `Guid.NewGuid()` — default generated by application
- **Audit fields**: `created_at` (default `NOW()`), `updated_at`
- **Enum storage**: Mapped tới `VARCHAR` qua `JsonStringEnumConverter`

### 12.2. Critical Indexes

```sql
-- UserVocabulary (bảng lớn nhất)
ix_user_vocabularies_user_id                    -- Fast per-user list
ix_user_vocabularies_next_review_date           -- Sub-ms review queue
ix_user_vocabularies_user_review_archive        -- COMPOSITE: user + review date + archived
                                                 -- Query phổ biến nhất: "Lấy flashcards cần ôn hôm nay"

-- ReviewLog (bảng lớn thứ 2, append-only)
BRIN index trên ReviewedAt                       -- Time-series queries cho heatmap
                                                 -- BRIN hiệu quả hơn B-Tree cho time-series data

-- MasterVocabulary
Unique index trên Word                           -- O(log n) lookup

-- PaymentTransaction
Unique index trên ExternalOrderId                -- Idempotency key
```

### 12.3. PostgreSQL-specific features

| Feature | Sử dụng |
|---------|---------|
| **JSONB column** | `UserSetting.ExcludedDomains` — mảng domain linh hoạt |
| **BRIN index** | `ReviewLog.ReviewedAt` — tối ưu cho append-only time-series |
| **Retry on failure** | Npgsql: 3 retries, max delay 5s |
| **Enum → VARCHAR** | `JsonStringEnumConverter` — human-readable trong DB |

---

## 13. Error Handling Strategy

```
Layer 1: Result<T>                  ← Business logic (400, 404, 403, 409)
         └─ Handler trả Result.Failure()
         └─ Controller gọi ToActionResult()

Layer 2: FluentValidation           ← Input validation (400)
         └─ ValidationBehavior throw ValidationException
         └─ GlobalExceptionMiddleware bắt → 400

Layer 3: GlobalExceptionMiddleware  ← System errors (500)
         └─ Bắt TẤT CẢ exceptions chưa handle
         └─ Log full stack trace (Serilog)
         └─ Trả sanitized message cho client
```

**Logging**: Serilog → Console + File (`logs/lexivocab-{date}.log`)
- Request logging (mọi HTTP request)
- Slow request warnings (> 500ms)
- Exception errors (full context)
- Payment transaction logs

---

## 14. Conventions & Patterns

### 14.1. Naming Conventions

| Item | Convention | Ví dụ |
|------|-----------|-------|
| Entity | PascalCase class | `UserVocabulary` |
| DB Column | snake_case | `next_review_date` |
| DB Table | snake_case plural | `user_vocabularies` |
| DTO | PascalCase record + suffix `Dto/Request` | `VocabularyDto`, `CreateOrderRequest` |
| Handler | PascalCase + suffix `Handler` | `CreateVocabularyHandler` |
| Command | PascalCase + suffix `Command` | `CreateVocabularyCommand` |
| Query | PascalCase + suffix `Query` | `GetBillingOverviewQuery` |
| Interface | `I` prefix | `IPaymentService` |
| Repository | `I{Entity}Repository` | `ISubscriptionRepository` |
| API Route | kebab-case | `/api/payments/create-order` |

### 14.2. File Organization Conventions

| Pattern | Quy tắc |
|---------|---------|
| **DTOs** | Gom chung theo feature: `Payment/PaymentDtos.cs` (chứa nhiều record) |
| **Handlers** | Gom Commands/Queries theo feature trong 1-2 files |
| **Controller** | 1 controller mỗi feature, inject chỉ `IMediator` |
| **Config** | 1 EF configuration file mỗi entity |
| **Repository** | 1 interface (Domain) + 1 implementation (Infrastructure) mỗi entity |

### 14.3. API Response Format

```json
// Success
{ "success": true, "data": { ... } }

// Error
{ "success": false, "error": "Human-readable message" }

// Paginated
{
    "success": true,
    "data": {
        "items": [ ... ],
        "totalCount": 150,
        "page": 1,
        "pageSize": 20,
        "totalPages": 8
    }
}
```

---

## 15. Hướng dẫn thêm Feature mới

### Checklist khi thêm 1 endpoint mới:

```
□ 1. Domain/Entities/       → Tạo Entity mới (kế thừa BaseEntity) nếu cần
□ 2. Domain/Enums/          → Tạo Enum mới nếu cần
□ 3. Domain/Interfaces/     → Tạo IRepository interface
□ 4. Application/DTOs/      → Tạo DTO records (request + response)
□ 5. Application/Features/  → Tạo Command/Query + Handler
                               (inject IUnitOfWork + ICurrentUserService)
                               (return Result<T>)
□ 6. Infrastructure/Persistence/Configurations/ → Tạo EF Configuration
□ 7. Infrastructure/Repositories/               → Implement repository
□ 8. Infrastructure/Repositories/UnitOfWork.cs  → Thêm property mới
□ 9. Infrastructure/DependencyInjection.cs      → Register repository
□ 10. Domain/Interfaces/IUnitOfWork.cs          → Thêm property
□ 11. API/Controllers/                          → Thêm Controller endpoint
                                                   (inject IMediator only)
                                                   (gọi ToActionResult)
□ 12. dotnet ef migrations add <Name>           → Tạo migration
□ 13. dotnet build                              → Verify 0 errors
```

### Ví dụ thực tế: Thêm "Achievements" feature

```
1. Domain/Entities/Achievement.cs
   → class Achievement : BaseEntity { Name, Description, UnlockedAt }

2. Domain/Interfaces/IAchievementRepository.cs
   → interface IAchievementRepository : IRepository<Achievement>
     { Task<List<Achievement>> GetByUserIdAsync(userId, ct) }

3. Application/DTOs/Achievement/AchievementDtos.cs
   → record AchievementDto(Id, Name, Description, UnlockedAt)

4. Application/Features/Achievements/AchievementQueries.cs
   → record GetAchievementsQuery : IRequest<Result<List<AchievementDto>>>
   → class GetAchievementsHandler (inject IUnitOfWork, ICurrentUserService)

5. Infrastructure/Repositories/AchievementRepository.cs
   → class AchievementRepository : GenericRepository<Achievement>, IAchievementRepository

6. Infrastructure/Persistence/Configurations/AchievementConfiguration.cs

7. Update: IUnitOfWork, UnitOfWork, DependencyInjection.cs

8. API/Controllers/AchievementsController.cs
   → [Authorize], inject IMediator, ToActionResult

9. dotnet ef migrations add AddAchievements
10. dotnet build ✅
```

---

## Phụ lục: Cấu hình quan trọng

### appsettings.json keys:

| Key | Mô tả |
|-----|-------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string |
| `ConnectionStrings:Redis` | Redis connection (optional) |
| `Jwt:Secret` | Signing key cho JWT |
| `Jwt:Issuer` | JWT issuer claim |
| `Jwt:Audience` | JWT audience claim |
| `Jwt:AccessTokenExpirationMinutes` | Access token lifetime |
| `Jwt:RefreshTokenExpirationDays` | Refresh token lifetime |
| `Cors:AllowedOrigins` | Danh sách origin được phép |
| `PayPal:*` | PayPal API credentials |
| `Google:ClientId` | Google OAuth client ID |
