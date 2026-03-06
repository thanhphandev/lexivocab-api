# ⚙️ Logic Nghiệp vụ & Thuật toán

> Chi tiết về SM-2 algorithm, caching strategy, audit trail, email system, feature gating, và background jobs.

---

## 1. Thuật toán SM-2 (Spaced Repetition)

### Tổng quan

SuperMemo-2 (SM-2) là thuật toán lõi quyết định **khi nào** user sẽ ôn lại một từ vựng.
Mỗi `UserVocabulary` lưu 4 trường SM-2:

| Trường | Default | Mô tả |
|--------|---------|-------|
| `RepetitionCount` | 0 | Số lần trả lời đúng liên tiếp |
| `EasinessFactor` | 2.5 | Hệ số dễ (min 1.3) — quyết định tốc độ tăng interval |
| `IntervalDays` | 0 | Khoảng cách ngày đến lần ôn tiếp theo |
| `NextReviewDate` | Now | Ngày ôn tập tiếp theo (INDEXED) |

### Công thức (file: `SrsAlgorithmService.cs`)

```
Input: currentRepCount, currentEF, currentInterval, qualityScore (0-5)

Bước 1: Tính EF mới
    EF' = EF + (0.1 - (5 - q) × (0.08 + (5 - q) × 0.02))
    EF' = max(1.3, EF')

Bước 2: Tính interval mới
    Nếu q < 3 (trả lời sai):
        → Reset: repCount = 0, interval = 1 ngày (re-learn)
    Nếu q >= 3 (trả lời đúng):
        → repCount = repCount + 1
        → interval = { 1 nếu rep=1, 6 nếu rep=2, previousInterval × EF' nếu rep>2 }

Bước 3: NextReviewDate = Now + interval ngày

Output: (newRepCount, newEF, newInterval, nextReviewDate)
```

### Quality Score Enum (`QualityScore.cs`)

| Giá trị | Tên | Ý nghĩa |
|---------|-----|---------|
| 0 | `Blackout` | Không nhớ gì |
| 1 | `Incorrect` | Sai nhưng nhận ra đáp án |
| 2 | `IncorrectButRemembered` | Sai nhưng nhớ mang máng |
| 3 | `CorrectWithDifficulty` | Đúng nhưng khó khăn |
| 4 | `CorrectWithHesitation` | Đúng, hơi chần chừ |
| 5 | `Perfect` | Đúng hoàn hảo, tự tin |

### Ví dụ tính toán

```
Từ "apple" — lần ôn đầu tiên, user đánh giá quality = 4:

Input: rep=0, EF=2.5, interval=0, q=4
EF' = 2.5 + (0.1 - (5-4) × (0.08 + (5-4) × 0.02)) = 2.5 + 0 = 2.5
q >= 3 → rep' = 1, interval = 1 (rep == 1)
NextReview = Now + 1 ngày

─────────────────────────────────────────

Lần 2, quality = 5:
Input: rep=1, EF=2.5, interval=1, q=5
EF' = 2.5 + (0.1 - 0 × ...) = 2.5 + 0.1 = 2.6
rep' = 2, interval = 6 (rep == 2)
NextReview = Now + 6 ngày

─────────────────────────────────────────

Lần 3, quality = 4:
Input: rep=2, EF=2.6, interval=6, q=4  
EF' = 2.6 + 0 = 2.6
rep' = 3, interval = ceil(6 × 2.6) = 16 ngày ← interval tăng dần!
NextReview = Now + 16 ngày

─────────────────────────────────────────

Lần 4, quality = 1 (quên mất):
Input: rep=3, EF=2.6, interval=16, q=1
EF' = 2.6 + (0.1 - 4 × (0.08 + 4 × 0.02)) = 2.6 - 0.54 = 2.06
q < 3 → RESET: rep' = 0, interval = 1 ← bắt đầu lại từ đầu!
```

### Tính chất quan trọng:
- **Pure function**: Không side effects, không truy cập DB, có thể unit test hoàn toàn
- **Stateless**: Service injected as Scoped, nhưng không giữ state
- **EF floor**: EF không bao giờ < 1.3 (tránh interval quá ngắn vĩnh viễn)

---

## 2. Caching Strategy

### Cache-Aside Pattern + Version Busting

Thay vì invalidate từng cache key riêng lẻ (phức tạp), dùng **version key** để invalidate toàn bộ:

```
Khi user thêm/sửa/xóa vocab:
    Redis SET "vocab-v:{userId}" = random UUID

Khi đọc vocab list:
    version = Redis GET "vocab-v:{userId}" ?? "0"
    cacheKey = "vocab-list:{userId}:v{version}:p{page}:s{size}:a{archived}:q{search}"
    
    → Nếu version thay đổi → tất cả cache key cũ sẽ tự hết hạn (không cần xóa)
```

### Nơi áp dụng caching

| Endpoint | Cache Key Pattern | TTL | Invalidation |
|----------|-------------------|-----|--------------|
| GET /vocabularies | `vocab-list:{userId}:v{ver}:p:s:a:q` | 1h sliding | Bất kỳ write nào → new version |
| GET /vocabularies/{id} | `vocab-item:{userId}:{id}:v{ver}` | 1h sliding | Bất kỳ write nào → new version |
| GET /vocabularies/stats | `vocab-stats:{userId}:v{ver}` | 2h sliding | Bất kỳ write nào → new version |
| GET /analytics/dashboard | `analytics-dashboard:{userId}:v{ver}` | 1h absolute | Bất kỳ write nào → new version |
| GET /analytics/heatmap | `analytics-heatmap:{userId}:{year}:v{ver}` | 4h absolute | Bất kỳ write nào → new version |
| GET /analytics/streak | `analytics-streak:{userId}:v{ver}` | 4h absolute | Bất kỳ write nào → new version |
| Refresh Token | `rf_token:{token}` | 7 ngày absolute | Logout / Refresh rotation |

### Redis fallback

```
Development (Redis không cấu hình):
    → Tự động dùng In-Memory DistributedCache
    → Không cần Docker Redis để dev

Production (Redis down):
    → CachingBehavior catch exception → log warning → skip cache → query DB trực tiếp
    → Application KHÔNG bao giờ crash vì Redis
```

---

## 3. MediatR Pipeline Behaviors

### Thứ tự thực thi (quan trọng)

```
Request ──→ ① AuditLogging ──→ ② Caching ──→ ③ Validation ──→ ④ Performance ──→ Handler
```

### ① AuditLoggingBehavior

```
Điều kiện kích hoạt: Request implements IAuditedRequest

Cơ chế:
1. Trước handler: Ghi nhận thời gian bắt đầu, serialize request payload
2. Gọi handler (next())
3. Sau handler: Ghi AuditLog vào DB (fire-and-forget via Task.Run)

An toàn:
- Serialize: MaxDepth=3, IgnoreCycles, truncate 4KB
- [JsonIgnore] trên sensitive fields (Password, IdToken)
- Fire-and-forget: audit failure không block main request
- Chỉ requests implementing IAuditedRequest mới bị log
```

### ② CachingBehavior

```
Điều kiện kích hoạt: Request implements ICacheableQuery

Cơ chế:
1. Compute cache key từ ICacheableQuery.CacheKey
2. Check Redis → HIT? Return cached data
3. MISS → call handler → serialize result → store in Redis
4. Return result

An toàn:
- Redis down → log warning → skip cache → normal DB query
- Chỉ cache khi result.IsSuccess == true
- Dùng DistributedCacheEntryOptions từ query
```

### ③ ValidationBehavior

```
Điều kiện kích hoạt: Có FluentValidation validator cho request type

Cơ chế:
1. Tìm tất cả IValidator<TRequest> từ DI container
2. ValidateAsync tất cả validators
3. Nếu có lỗi → throw ValidationException
4. GlobalExceptionMiddleware bắt → 400 Bad Request

Ưu điểm:
- Auto-discover validators (FluentValidation DI Extensions)
- Fail-fast: không gọi handler nếu invalid
```

### ④ PerformanceBehavior

```
Cơ chế:
1. Bắt đầu Stopwatch
2. Gọi handler
3. Nếu elapsed > 500ms → LogWarning

Mục đích: Phát hiện slow handlers trong production qua log monitoring
```

---

## 4. Feature Gating (Freemium Model)

### Cơ chế kiểm tra (file: `FeatureGatingService.cs`)

```
IsPremium(userId):
    user = GetByIdAsync(userId)
    return user.Role == Admin
        || user.Role == Premium
        || (user.PlanExpirationDate != null && user.PlanExpirationDate > UtcNow)

CanCreateVocabulary(userId):
    if IsPremium → true (unlimited)
    currentCount = CountByUserIdAsync(userId)
    maxLimit = config["FreePlan:MaxVocabularies"] (default: 50)
    return currentCount < maxLimit
```

### Permissions Matrix

| Feature | Free | Premium |
|---------|------|---------|
| Tạo vocab | ≤ 50 từ | ∞ (unlimited) |
| Batch Import | ❌ | ✅ |
| Export Data | ❌ | ✅ |
| Review Session | ✅ | ✅ |
| Analytics | ✅ | ✅ |
| Chrome Extension | ✅ | ✅ |

### UserPermissionsDto trả về cho frontend

```json
{
    "planName": "Free",
    "maxVocabularies": 50,
    "currentVocabularyCount": 23,
    "canCreateVocabulary": true,
    "canBatchImport": false,
    "canExportData": false,
    "planExpirationDate": null
}
```

---

## 5. Background Jobs

### SubscriptionExpirationJob (chạy mỗi 12 giờ)

```
Cơ chế:
1. Tìm subscriptions: Status == Active AND EndDate <= UtcNow
2. Cho mỗi subscription hết hạn:
   a. Set Status = Expired
   b. Set User.Role = User (revert from Premium)
   c. Set User.PlanExpirationDate = null
   d. Send SubscriptionExpired email
3. Tìm subscriptions sắp hết hạn (3 ngày):
   a. Send SubscriptionExpiring warning email

Xử lý lỗi:
- Mỗi user xử lý trong try-catch riêng
- 1 user lỗi không ảnh hưởng user khác
- Log error + continue
```

### ReviewReminderJob (chạy mỗi 24 giờ, delay 1h sau startup)

```
Cơ chế:
1. Tìm users: IsActive == true
2. Cho mỗi user:
   a. Kiểm tra có due cards? (NextReviewDate <= Now AND !IsArchived)
   b. Kiểm tra inactive > 3 ngày? (LastReviewedAt hoặc LastLogin)
   c. Nếu cả 2 → Send ReviewReminder email

Tối ưu:
- Chỉ gửi 1 email/ngày (job chạy 1 lần/ngày)
- Skip users không có due cards
```

---

## 6. Email System

### Architecture

```
Handler/Job
    │
    ▼
IEmailQueueService (Hangfire)
    │ BackgroundJob.Enqueue()      ← returns immediately (non-blocking)
    │
    ▼
IEmailService (SMTP)               ← actual delivery in background worker
```

### Email Templates (6 files HTML)

| Template | Trigger | Placeholders |
|----------|---------|--------------|
| `Welcome.html` | Registration (email + Google) | FullName, AppUrl |
| `PaymentSuccess.html` | Payment capture completed | FullName, PlanName, Amount, ExpiryDate, TransactionId |
| `SubscriptionExpired.html` | Subscription hết hạn | FullName, AppUrl |
| `SubscriptionExpiring.html` | 3 ngày trước hết hạn | FullName, DaysRemaining, ExpiryDate, AppUrl |
| `ReviewReminder.html` | Inactive 3+ ngày, có due cards | FullName, DueCount, AppUrl |
| `PaymentRefunded.html` | PayPal refund webhook | FullName, Amount |

### Template Rendering (`EmailTemplateService.cs`)

```
1. Load HTML file từ embedded resources
2. Replace {{FullName}} → actual value
3. Replace {{AppUrl}} → config["App:Url"]
4. Return rendered HTML string
```

---

## 7. Audit Trail System

### Cơ chế hoạt động

```
Command implements IAuditedRequest:
    property AuditAction → enum (Register, Login, VocabCreated, etc.)
    property EntityType → string ("User", "UserVocabulary")
    property EntityId → string? (entity ID if applicable)

Pipeline flow:
    AuditLoggingBehavior intercepts → 
    Serialize request payload (safe: MaxDepth=3, Truncate 4KB) →
    Execute handler →
    Fire-and-forget: Write AuditLog to DB

AuditLog entity data:
    UserId, UserEmail (denormalized)
    Action, EntityType, EntityId
    RequestPayload (JSON)
    IpAddress, UserAgent
    TraceId (Serilog correlation)
    DurationMs
    IsSuccess
    Timestamp
```

### Queried by Admin

Admin Dashboard: `GET /api/v1/admin/audit-logs?userId=&action=&entityType=&fromDate=&toDate=&page=&pageSize=`
- Filter by user, action type, entity type, date range
- Paginated (max 200 per page)
- Full traceability cho compliance

---

## 8. Payment & Subscription Logic

### Subscription Lifecycle

```
     ┌─────────┐   Create Order    ┌─────────┐   Capture/Webhook   ┌─────────┐
     │ (none)  │ ───────────────→ │ Pending │ ────────────────→  │ Active  │
     └─────────┘                   └─────────┘                    └─────────┘
                                        │                              │
                                  User cancels                    EndDate <= Now
                                   or payment                (SubscriptionExpirationJob)
                                    fails                          │
                                        │                          ▼
                                        ▼                    ┌─────────┐
                                  ┌───────────┐              │ Expired │
                                  │ Cancelled │              └─────────┘
                                  └───────────┘

Idempotency:
    - Cả CaptureOrderAsync VÀ Webhook đều gọi ActivateSubscriptionByOrderIdAsync
    - Nếu tx.Status == Completed → return (skip, đã xử lý)
    - Đảm bảo user chỉ bị upgrade 1 lần dù webhook đến muộn
```

### Pricing

| Plan | Price | Duration |
|------|-------|----------|
| Monthly | $9.99/mo | 1 tháng |
| Yearly | $99.99/yr | 1 năm |
| Lifetime | $199.99 | Vĩnh viễn (EndDate = null) |
