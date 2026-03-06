# 🔄 Luồng Xử lý Dữ liệu

> Chi tiết luồng request qua từng layer cho mọi tính năng chính.

---

## 1. Luồng chung: HTTP Request → Response

```
Client
  │ HTTP Request
  ▼
┌─────────────────────────────────┐
│ API Layer                        │
│  ① GlobalExceptionMiddleware    │ ← Catch exceptions → JSON error
│  ② SecurityHeadersMiddleware    │ ← Add security headers
│  ③ Response Compression         │ ← Brotli/Gzip
│  ④ Serilog Request Logging      │ ← Log request details
│  ⑤ CORS                         │ ← Validate origin
│  ⑥ Rate Limiter                 │ ← 100/min global, 5/min auth
│  ⑦ JWT Authentication           │ ← Validate token → ClaimsPrincipal
│  ⑧ Authorization                │ ← Check [Authorize], Roles
│  ⑨ Controller                   │ ← Route → mediator.Send()
└─────────────────────────────────┘
  │ MediatR Pipeline
  ▼
┌─────────────────────────────────┐
│ Application Layer                │
│  ① AuditLoggingBehavior        │ ← Log if IAuditedRequest
│  ② CachingBehavior             │ ← Cache if ICacheableQuery
│  ③ ValidationBehavior          │ ← FluentValidation
│  ④ PerformanceBehavior         │ ← Log slow handlers
│  ⑤ Handler                     │ ← Business logic
└─────────────────────────────────┘
  │ IUnitOfWork
  ▼
┌─────────────────────────────────┐
│ Infrastructure Layer             │
│  Repository → EF Core → SQL    │
│  Redis Cache                     │
│  External APIs (PayPal, Google) │
└─────────────────────────────────┘
  │
  ▼
PostgreSQL / Redis
```

---

## 2. Luồng Authentication

### 2.1 Register (`POST /api/v1/auth/register`)

```
AuthController.Register
  │ RegisterCommand(Email, Password, FullName, DeviceInfo, IpAddress)
  │
  ▼ MediatR Pipeline (AuditLogging → Validation → RegisterCommandHandler)
  │
  ├─ 1. EmailExistsAsync(email) → 409 Conflict nếu trùng
  │
  ├─ 2. Tạo User entity
  │     Email = email.ToLowerInvariant().Trim()
  │     PasswordHash = BCrypt.Hash(password)
  │     FullName = fullName.Trim()
  │     LastLogin = UtcNow
  │
  ├─ 3. AddAsync(user) + tạo UserSetting default
  │
  ├─ 4. SaveChangesAsync (1st save — tạo user)
  │
  ├─ 5. Gửi Welcome email (fire-and-forget, fail-safe)
  │     templateService.RenderTemplateAsync("Welcome", { FullName, AppUrl })
  │     emailQueue.EnqueueEmail(email, subject, html) → Hangfire job
  │
  ├─ 6. Generate JWT access token (1h expiry)
  │     Claims: userId, email, role
  │
  ├─ 7. Generate refresh token (random bytes → Base64)
  │     Hash refresh token → store in User.RefreshTokenHash
  │     Store metadata in Redis: rf_token:{token} → { userId, device, ip, created }
  │     TTL: 7 ngày
  │
  ├─ 8. SaveChangesAsync (2nd save — lưu RefreshTokenHash)
  │
  └─ 9. Return 201 + AuthResponse
        │ Controller: SetRefreshTokenCookie (HttpOnly, Secure, SameSite=None)
        │ Response: { userId, email, fullName, role, accessToken, refreshToken, expiresAt }
```

### 2.2 Login (`POST /api/v1/auth/login`)

```
  1. GetByEmailAsync(email)                  → 401 nếu không tìm thấy
  2. BCrypt.Verify(password, user.Hash)      → 401 nếu sai password
  3. Check user.IsActive                     → 403 nếu bị deactivate
  4. Generate token pair (giống Register)
  5. Update user.LastLogin = UtcNow
  6. Return 200 + AuthResponse
```

### 2.3 Google OAuth (`POST /api/v1/auth/google`)

```
  1. ValidateIdTokenAsync(idToken)           → Gọi Google API → 401 nếu invalid
  2. GetByAuthProviderAsync("Google", sub)   → Tìm user đã link Google
  
  Nếu có user linked:
    → Login bình thường (generate tokens)
    
  Nếu không có user linked:
    → GetByEmailAsync(googleUser.email)
    → Nếu có user cùng email → Link Google vào user hiện tại
    → Nếu không có → Tạo user mới (passwordHash = null, authProvider = "Google")
    
  3. Check user.IsActive                    → 403 nếu bị ban
  4. Generate tokens + return
```

### 2.4 Refresh Token (`POST /api/v1/auth/refresh`)

```
  1. Lấy refreshToken từ Cookie (HttpOnly)
  2. Redis GET rf_token:{token}              → 401 nếu không tồn tại
  3. Deserialize metadata → lấy userId
  4. GetByIdAsync(userId)                    → 401 nếu user không tồn tại
  5. BCrypt.Verify(refreshToken, user.RefreshTokenHash) → 401 nếu không khớp
  6. Redis DELETE rf_token:{oldToken}         ← TOKEN ROTATION (old token vô hiệu hóa)
  7. Generate new token pair
  8. Store new RefreshTokenHash + new Redis entry
  9. Return 200 + AuthResponse (new tokens)
```

### 2.5 Logout (`POST /api/v1/auth/logout`)

```
  1. Lấy refreshToken từ Cookie
  2. Redis DELETE rf_token:{token}            ← Invalidate token
  3. Delete refreshToken cookie
  4. Return 200 "Logged out successfully"
```

---

## 3. Luồng Vocabulary

### 3.1 Create (`POST /api/v1/vocabularies`)

```
  1. FeatureGating: CanCreateVocabularyAsync(userId)
     │ Free user + count >= 50 → 403 "ERR_QUOTA_EXCEEDED"
  
  2. Duplicate check: WordExistsForUserAsync(userId, wordText)
     │ Exists → 409 Conflict
  
  3. MasterVocabulary lookup: GetByWordAsync(wordText.toLower().trim())
     │ Có → lấy phonetics, audio URL → gán MasterVocabularyId
     │ Không có → MasterVocabularyId = null
  
  4. Tạo UserVocabulary entity
     │ SM-2 defaults: RepCount=0, EF=2.5, Interval=0, NextReview=Now
  
  5. SaveChangesAsync
  
  6. Cache invalidation: Redis SET vocab-v:{userId} = new UUID
  
  7. Return 201 + VocabularyDto
```

### 3.2 Batch Import (`POST /api/v1/vocabularies/batch`) — Premium Only

```
  1. FeatureGating: GetPermissionsAsync → CanBatchImport?
     │ Free → 403 "ERR_PREMIUM_REQUIRED"
  
  2. Batch duplicate check (1 query thay vì N queries):
     │ GetExistingWordsAsync(userId, allWordTexts)
     │ → HashSet<string> của words đã tồn tại
  
  3. Batch master vocab lookup (1 query):
     │ GetByWordsAsync(newWordTexts)
     │ → Dictionary<string, MasterVocabulary>
  
  4. Lặp qua words:
     │ Skip nếu word ∈ existingWords
     │ Skip nếu trùng trong cùng batch (intra-batch dedup)
     │ Tạo UserVocabulary entity với MasterVocabId từ dict
  
  5. AddRangeAsync(entities) — 1 batch INSERT
  6. SaveChangesAsync
  7. Cache invalidation
  8. Return 201 + count imported
```

### 3.3 Archive (`PATCH /api/v1/vocabularies/{id}/archive`)

```
  1. GetByIdAsync(id) + ownership check
  2. Toggle: vocab.IsArchived = !vocab.IsArchived
  3. SaveChangesAsync
  4. Cache invalidation
  5. Return 200
```

### 3.4 Delete (`DELETE /api/v1/vocabularies/{id}`)

```
  1. GetByIdAsync(id) + ownership check
  2. Delete tất cả ReviewLogs liên quan (cascade manual)
  3. Delete UserVocabulary
  4. SaveChangesAsync
  5. Cache invalidation
  6. Return 200
```

### 3.5 Export (`GET /api/v1/vocabularies/export?format=json|csv`) — Premium Only

```
  1. FeatureGating: CanExportData?
  2. GetByUserIdAsync(userId, page=1, pageSize=MAX)
  3. Map to export format (WordText, Meaning, Context, AddedOn, IsMastered)
  4. Format == "csv" → CSV string
     Format == "json" → JSON string (indented)
  5. Return File(bytes, contentType, fileName)
```

---

## 4. Luồng Review (Spaced Repetition)

### 4.1 Get Review Session (`GET /api/v1/reviews/session?limit=50`)

```
  1. GetDueForReviewAsync(userId, limit)
     │ Query: NextReviewDate <= Now AND !IsArchived
     │ Order: NextReviewDate ASC (oldest due first)
     │ Uses composite index (UserId, NextReviewDate, IsArchived) → sub-ms
  
  2. Map to ReviewCardDto:
     │ { id, wordText, meaning, context, phoneticUs, audioUrl, repCount, EF }
  
  3. GetStatsAsync → totalDue count
  
  4. Return ReviewSessionDto { cards[], totalDueCount }
```

### 4.2 Submit Review (`POST /api/v1/reviews`)

```
  1. GetByIdAsync(vocabId) + ownership check
  
  2. SM-2 Calculate:
     │ Input: currentRepCount, currentEF, currentInterval, qualityScore
     │ Output: newRepCount, newEF, newInterval, nextReviewDate
     │ (Chi tiết thuật toán → xem BUSINESS-LOGIC.md §1)
  
  3. Update UserVocabulary SRS state:
     │ RepetitionCount = newRepCount
     │ EasinessFactor = newEF
     │ IntervalDays = newInterval
     │ NextReviewDate = nextReviewDate
     │ LastReviewedAt = UtcNow
  
  4. Create ReviewLog (IMMUTABLE, append-only):
     │ { userVocabId, userId (denormalized), qualityScore, timeSpentMs, reviewedAt }
  
  5. SaveChangesAsync (atomic: cả vocab update + log insert)
  
  6. Cache invalidation: vocab-v:{userId}
  
  7. Return ReviewResultDto:
     │ { vocabId, newRepCount, newEF, newInterval, nextReviewDate }
```

---

## 5. Luồng Payment (PayPal)

### 5.1 Create Order (`POST /api/v1/payments/create-order`)

```
  1. GetAccessTokenAsync → PayPal OAuth2 token
  
  2. Build PayPal order payload:
     │ intent: "CAPTURE"
     │ amount: $9.99 (monthly) / $99.99 (yearly) / $199.99 (lifetime)
     │ return_url, cancel_url
  
  3. POST /v2/checkout/orders → PayPal API
  
  4. Parse response → extract approval URL
  
  5. Create DB records (BEFORE user pays):
     │ Subscription { status: Pending, plan: Premium }
     │ PaymentTransaction { status: Pending, externalOrderId: paypalOrderId }
  
  6. Return approvalUrl → frontend redirect user to PayPal
```

### 5.2 Capture Order (`POST /api/v1/payments/capture-order`)

```
  User đã approve trên PayPal → redirect back → frontend gọi capture:
  
  1. POST /v2/checkout/orders/{orderId}/capture → PayPal API
  
  2. Nếu status == "COMPLETED":
     │ ActivateSubscriptionByOrderIdAsync(orderId):
     │   a. Find PaymentTransaction by ExternalOrderId
     │   b. IDEMPOTENT CHECK: tx.Status == Completed → return (skip)
     │   c. tx.Status = Completed, tx.PaidAt = UtcNow
     │   d. Subscription.Status = Active
     │   e. Subscription.StartDate = UtcNow (recalculate from now)
     │   f. User.Role = Premium
     │   g. User.PlanExpirationDate = Subscription.EndDate
     │   h. SaveChangesAsync
     │   i. Gửi PaymentSuccess email
  
  3. Return 200 "Payment successful. Account upgraded."
```

### 5.3 Webhook (`POST /api/v1/payments/webhook`)

```
  PayPal gửi async notification (backup cho trường hợp user không redirect):
  
  1. Đọc raw body
  2. VerifyWebhookSignatureAsync:
     │ Production: POST /v1/notifications/verify-webhook-signature → PayPal API
     │ Development: trust by default (nếu không cấu hình WebhookId)
  
  3. ProcessWebhookEventAsync:
     │ PAYMENT.CAPTURE.COMPLETED → ActivateSubscriptionByOrderIdAsync (idempotent)
     │ PAYMENT.CAPTURE.REFUNDED → tx.Status=Refunded, user.Role=User, send email
     │ PAYMENT.CAPTURE.DENIED/DECLINED → tx.Status=Failed, sub.Status=Cancelled
```

---

## 6. Luồng Analytics

### 6.1 Dashboard (`GET /api/v1/analytics/dashboard`)

```
  1. Check cache (version-busted)
  2. GetStatsAsync → total, active, archived, dueToday
  3. GetPeriodStatsAsync(today) → reviewsToday, avgQuality
  4. GetPeriodStatsAsync(weekStart) → reviewsThisWeek
  5. GetCurrentStreakAsync → consecutive days reviewed
  6. GetHeatmapDataAsync(yearStart, today) → count study days
  7. Cache result (1h absolute)
  8. Return DashboardDto
```

### 6.2 Heatmap (`GET /api/v1/analytics/heatmap?year=2026`)

```
  1. Check cache
  2. GetHeatmapDataAsync(Jan 1, Dec 31) → [{date, count}]
  3. Cache result (4h)
  4. Return HeatmapDataDto { entries[], year }
```

---

## 7. Luồng Admin

### 7.1 User Management

```
GET    /admin/users                → Paginated user list (search by email/name)
GET    /admin/users/{id}           → User detail (vocab stats, review logs, subs)
PUT    /admin/users/{id}/role      → Change role (User/Premium/Admin)
PUT    /admin/users/{id}/status    → Activate/Deactivate (soft ban)
POST   /admin/users/{id}/subscriptions → Manual subscription (X days)
DELETE /admin/users/{id}/subscriptions → Force cancel subscription
```

### 7.2 Add Manual Subscription (Admin)

```
  1. Find user
  2. Deactivate existing active subscription (if any)
     │ currentSub.Status = Cancelled, EndDate = UtcNow
  3. Create new Subscription { Plan, Status=Active, EndDate=UtcNow+DurationDays }
  4. Update user.Role = Premium (unless already Admin)
  5. Update user.PlanExpirationDate = endDate
  6. SaveChangesAsync
```

### 7.3 System Metrics (`GET /admin/metrics`)

```
  Aggregated stats: total users, premium users, total vocabularies,
  reviews today/week, revenue stats, etc.
```

### 7.4 Audit Logs (`GET /admin/audit-logs`)

```
  Filters: userId, action, entityType, fromDate, toDate
  Pagination: page, pageSize (max 200)
  Returns: full audit trail for compliance
```
