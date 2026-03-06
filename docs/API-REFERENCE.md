# 📡 API Reference

> Tất cả endpoints, authentication, request/response formats.

---

## Base URL

```
Development: https://localhost:5001/api/v1
Production:  https://api.lexivocab.store/api/v1
```

## Authentication

Hầu hết endpoints yêu cầu JWT Bearer token:
```
Authorization: Bearer <access_token>
```

Refresh token được lưu trong HttpOnly cookie `refreshToken`.

## Response Format

```json
// Success
{ "success": true, "data": { ... } }

// Error
{ "success": false, "error": "Error message", "statusCode": 400, "traceId": "...", "timestamp": "..." }

// Paginated
{ "success": true, "data": { "items": [...], "totalCount": 100, "page": 1, "pageSize": 20 } }
```

---

## 1. Auth (`/auth`)

| Method | Endpoint | Auth | Rate Limit | Mô tả |
|--------|----------|------|------------|-------|
| POST | `/auth/register` | ❌ | 5/min | Đăng ký tài khoản |
| POST | `/auth/login` | ❌ | 5/min | Đăng nhập |
| POST | `/auth/google` | ❌ | 5/min | Đăng nhập Google OAuth |
| POST | `/auth/refresh` | ❌ | 5/min | Refresh access token |
| POST | `/auth/logout` | ❌ | 5/min | Đăng xuất |
| GET | `/auth/me` | ✅ | Global | Thông tin user hiện tại |
| GET | `/auth/permissions` | ✅ | Global | Feature permissions & quotas |

### POST `/auth/register`

```json
// Request
{ "email": "user@example.com", "password": "MyP@ssw0rd!", "fullName": "John Doe" }

// Response 201
{
    "success": true,
    "data": {
        "userId": "uuid",
        "email": "user@example.com",
        "fullName": "John Doe",
        "role": "User",
        "accessToken": "jwt...",
        "refreshToken": "random-base64...",
        "expiresAt": "2026-03-06T11:35:00Z"
    }
}
// + Set-Cookie: refreshToken=...; HttpOnly; Secure; SameSite=None; Expires=7days
```

### POST `/auth/login`

```json
// Request
{ "email": "user@example.com", "password": "MyP@ssw0rd!" }

// Response 200 — same shape as register
// Response 401 — { "success": false, "error": "Invalid credentials" }
// Response 403 — { "success": false, "error": "Account deactivated" }
```

### POST `/auth/google`

```json
// Request
{ "idToken": "google-id-token-from-frontend" }

// Response 200 — same shape as register
```

### GET `/auth/permissions`

```json
// Response 200
{
    "success": true,
    "data": {
        "planName": "Free",
        "maxVocabularies": 50,
        "currentVocabularyCount": 23,
        "canCreateVocabulary": true,
        "canBatchImport": false,
        "canExportData": false,
        "planExpirationDate": null
    }
}
```

---

## 2. Vocabularies (`/vocabularies`)

| Method | Endpoint | Auth | Mô tả |
|--------|----------|------|-------|
| GET | `/vocabularies` | ✅ | Danh sách vocab (paginated + search) |
| GET | `/vocabularies/{id}` | ✅ | Chi tiết 1 vocab |
| POST | `/vocabularies` | ✅ | Tạo vocab mới |
| PUT | `/vocabularies/{id}` | ✅ | Cập nhật meaning/context |
| PATCH | `/vocabularies/{id}/archive` | ✅ | Toggle archive (mastered) |
| DELETE | `/vocabularies/{id}` | ✅ | Xóa vĩnh viễn |
| POST | `/vocabularies/batch` | ✅ Premium | Batch import |
| GET | `/vocabularies/stats` | ✅ | Thống kê vocab |
| GET | `/vocabularies/export` | ✅ Premium | Export CSV/JSON |

### GET `/vocabularies?page=1&pageSize=20&isArchived=false&search=apple`

```json
// Response 200
{
    "success": true,
    "data": {
        "items": [{
            "id": "uuid",
            "wordText": "apple",
            "customMeaning": "quả táo",
            "contextSentence": "An apple a day...",
            "sourceUrl": "https://example.com/article",
            "repetitionCount": 3,
            "easinessFactor": 2.6,
            "intervalDays": 16,
            "nextReviewDate": "2026-03-22T00:00:00Z",
            "lastReviewedAt": "2026-03-06T10:00:00Z",
            "isArchived": false,
            "createdAt": "2026-02-15T08:00:00Z",
            "phoneticUk": "/ˈæp.əl/",
            "phoneticUs": "/ˈæp.əl/",
            "audioUrl": "https://...",
            "partOfSpeech": "noun"
        }],
        "totalCount": 23,
        "page": 1,
        "pageSize": 20
    }
}
```

### POST `/vocabularies`

```json
// Request
{
    "wordText": "serendipity",
    "customMeaning": "tình cờ may mắn",
    "contextSentence": "It was pure serendipity...",
    "sourceUrl": "https://example.com"
}

// Response 201 — VocabularyDto
// Response 403 — ERR_QUOTA_EXCEEDED (Free tier limit)
// Response 409 — Duplicate word
```

### POST `/vocabularies/batch` (Premium)

```json
// Request
{
    "words": [
        { "wordText": "apple", "customMeaning": "quả táo" },
        { "wordText": "banana", "customMeaning": "quả chuối" }
    ]
}

// Response 201
{ "success": true, "data": 2 }
// Response 403 — ERR_PREMIUM_REQUIRED
```

---

## 3. Reviews (`/reviews`)

| Method | Endpoint | Auth | Mô tả |
|--------|----------|------|-------|
| GET | `/reviews/session` | ✅ | Lấy cards cần ôn hôm nay |
| POST | `/reviews` | ✅ | Submit kết quả ôn tập |
| GET | `/reviews/history` | ✅ | Lịch sử ôn tập (paginated) |

### GET `/reviews/session?limit=50`

```json
// Response 200
{
    "success": true,
    "data": {
        "cards": [{
            "id": "uuid",
            "wordText": "serendipity",
            "customMeaning": "tình cờ may mắn",
            "contextSentence": "...",
            "phoneticUs": "/ˌsɛr.ənˈdɪp.ɪ.ti/",
            "audioUrl": "https://...",
            "repetitionCount": 1,
            "easinessFactor": 2.5
        }],
        "totalDueCount": 12
    }
}
```

### POST `/reviews`

```json
// Request
{
    "userVocabularyId": "uuid",
    "qualityScore": 4,
    "timeSpentMs": 3500
}
// qualityScore: 0=Blackout, 1=Incorrect, 2=IncorrectButRemembered,
//               3=CorrectWithDifficulty, 4=CorrectWithHesitation, 5=Perfect

// Response 200
{
    "success": true,
    "data": {
        "vocabularyId": "uuid",
        "newRepetitionCount": 2,
        "newEasinessFactor": 2.6,
        "newIntervalDays": 6,
        "nextReviewDate": "2026-03-12T10:00:00Z"
    }
}
```

---

## 4. Analytics (`/analytics`)

| Method | Endpoint | Auth | Mô tả |
|--------|----------|------|-------|
| GET | `/analytics/dashboard` | ✅ | Tổng quan dashboard |
| GET | `/analytics/heatmap` | ✅ | Dữ liệu heatmap theo năm |
| GET | `/analytics/streak` | ✅ | Streak hiện tại + dài nhất |

---

## 5. Payments (`/payments`)

| Method | Endpoint | Auth | Mô tả |
|--------|----------|------|-------|
| GET | `/payments/billing` | ✅ | Billing overview |
| GET | `/payments/history` | ✅ | Lịch sử giao dịch |
| GET | `/payments/plans` | ❌ | Danh sách plans |
| POST | `/payments/create-order` | ✅ | Tạo PayPal order |
| POST | `/payments/capture-order` | ✅ | Capture sau khi user approve |
| POST | `/payments/webhook` | ❌ | PayPal webhook (server-to-server) |

---

## 6. Settings (`/settings`)

| Method | Endpoint | Auth | Mô tả |
|--------|----------|------|-------|
| GET | `/settings` | ✅ | Lấy user settings |
| PUT | `/settings` | ✅ | Cập nhật settings |

---

## 7. Admin (`/admin`) — Requires Admin Role

| Method | Endpoint | Auth | Mô tả |
|--------|----------|------|-------|
| GET | `/admin/users` | Admin | Danh sách users (paginated) |
| GET | `/admin/users/{id}` | Admin | Chi tiết user |
| PUT | `/admin/users/{id}/role` | Admin | Thay đổi role |
| PUT | `/admin/users/{id}/status` | Admin | Activate/Deactivate |
| POST | `/admin/users/{id}/subscriptions` | Admin | Cấp subscription thủ công |
| DELETE | `/admin/users/{id}/subscriptions` | Admin | Hủy subscription |
| GET | `/admin/metrics` | Admin | System metrics |
| GET | `/admin/audit-logs` | Admin | Query audit logs |

---

## 8. Master Vocabulary (`/master-vocab`)

| Method | Endpoint | Auth | Mô tả |
|--------|----------|------|-------|
| GET | `/master-vocab/search?q=apple&limit=10` | ✅ | Autocomplete search |

---

## HTTP Status Codes

| Code | Ý nghĩa | Khi nào |
|------|---------|---------|
| 200 | OK | Success (read/update) |
| 201 | Created | Success (create) |
| 400 | Bad Request | Validation errors |
| 401 | Unauthorized | Missing/invalid token |
| 403 | Forbidden | Insufficient permissions / quota exceeded |
| 404 | Not Found | Resource không tồn tại |
| 409 | Conflict | Duplicate (email, word) |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Internal Server Error | Unexpected errors |
