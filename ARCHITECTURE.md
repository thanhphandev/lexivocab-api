# LexiVocab API - Architecture & Codebase Overview

## 1. Tổng quan Kiến trúc (Architecture Overview)

Dự án **LexiVocab API** được thiết kế dựa trên mô hình **Clean Architecture** kết hợp với **CQRS Pattern** (sử dụng MediatR). Điều này giúp phân tách rõ ràng các tầng (layers), tăng cường khả năng test (testability) và bảo trì (maintainability) của hệ thống.

Hệ thống được chia thành 4 project riêng biệt trong `src/`:
- **LexiVocab.Domain**: Lõi của hệ thống (Core). Chứa các Entities, Enums, và Interfaces không phụ thuộc vào bất kỳ công nghệ bên ngoài nào.
- **LexiVocab.Application**: Chứa toàn bộ business logic (Business Rules). Chứa các Features (Commands/Queries/Validators), DTOs, và các Interfaces định nghĩa Services. Phụ thuộc vào `Domain`.
- **LexiVocab.Infrastructure**: Tầng hạ tầng. Chứa implementations kết nối database (EF Core PostgreSQL), Authentication (JWT), Email, Payments (PayPal), và External APIs (Google Auth).
- **LexiVocab.API**: Tầng giao tiếp (Presentation layer). Chứa Controllers, Middlewares xử lý HTTP requests/responses, routing và cấu hình Dependency Injection.

---

## 2. Các Module Chính (Core Modules)

### 2.1. Spaced Repetition System (Hệ thống lặp lại khoảng cách - SRS)
- **Cốt lõi**: `SrsAlgorithmService.cs` (thuật toán SM-2 - SuperMemo 2).
- **Hoạt động**: Thuật toán nhận vào điểm Quality Score (từ 0-5 do người dùng đánh giá) để tính toán:
  - `RepetitionCount` (Số lần lặp lại).
  - `EasinessFactor` (Hệ số dễ nhớ - min 1.3).
  - `IntervalDays` (Số ngày tới lần ôn tập tiếp theo).
  - `NextReviewDate` (Ngày ôn tập tiếp theo, được lập chỉ mục index để query cực nhanh).
- Khi người dùng gửi kết quả ôn tập (Submit review), hệ thống tính toán lại các thông số và lưu vào `ReviewLog` (Append-only table) để dùng cho Analytics.

### 2.2. Authentication & Authorization (Xác thực & Phân quyền)
- **Phương thức hỗ trợ**:
  - Email/Password truyền thống (mật khẩu được băm dùng BCrypt `BcryptPasswordHasher`).
  - Google OAuth (xác thực token qua `GoogleAuthService` và liên kết với tài khoản).
- **Bảo mật**: Sử dụng **JWT (JSON Web Tokens)** với cơ chế Rotation Refresh Token (lưu mã băm của Refresh Token tại `User.RefreshTokenHash` có giới hạn thời gian thực thi).
- **Role-based Access Control**: Ba cấp độ `User`, `Premium`, `Admin`.

### 2.3. Freemium & Feature Gating (Quản lý Gói cước & Giới hạn tính năng)
- Hệ thống hỗ trợ giới hạn tính năng tuỳ theo gói cước qua `IFeatureGatingService` (Ví dụ: Giới hạn số từ vựng tạo ra đối với bản Free, tính năng Batch Import/Export yêu cầu tài khoản Premium).
- **Payment Gateway**: Tích hợp với cổng thanh toán thay thế được thông qua `IPaymentService` (hiện hỗ trợ `PayPalService`).
- Payment life-cycles theo dõi qua bảng `Subscription` và `PaymentTransaction` (lưu ID order bên ngoài giúp tránh lỗi Webhook dội ngược gửi nhiều lần - idempotency).

### 2.4. Gamification & Analytics (Phân tích & Thống kê Game hoá)
- Chứa các query phân tích trải nghiệm học tập của người dùng.
- **Tính năng nổi bật**:
  - Heatmap giống GitHub để theo dõi mức độ đóng góp theo thời gian `GetHeatmapDataAsync`.
  - Cơ chế Streak: Tính chuỗi ngày học liên tục và chuỗi kỷ lục dài nhất `GetStreakHandler`.
  - Cung cấp Dashboard overview hiển thị số từ cần học (`DueToday`), số lượng Mastered, và chỉ số review trung bình.

### 2.5. Data Synchronization (Đồng bộ hoá Extension)
- Tích hợp `UserSetting` đồng bộ hoá các tùy biến của người dùng từ Chrome Extension lên mây (Màu highlight, tắt bật Highlight, các domain ngoại trừ `ExcludedDomains` - lưu bằng mảng JSONB trong PostgreSQL).

---

## 3. Core Database Entities (EF Core - PostgreSQL)

1. **User**: Quản lý thông tin đăng nhập, Role, liên kết Auth Provider. 
2. **MasterVocabulary**: Kho từ vựng chung hệ thống dùng chung để tối ưu không bị trùng lặp dữ liệu (Cung cấp Phát âm US/UK, Audio, Từ loại).
3. **UserVocabulary**: **Đây là bảng trung tâm (có thể phát triển thành hàng triệu records)**. Liên kết giữa User và MasterVocabulary. Đồng thời chứa dữ liệu lưu trữ SRS cho riêng từng user với từng từ vựng.
4. **ReviewLog**: Lưu trữ dữ liệu Log mỗi lần ôn tập (Bao gồm điểm số thẻ và thời gian học), được lập chỉ mục BRIN trên PostgreSQL theo thời gian để sinh ra Heatmap rất nhanh mà không cần parse date string.
5. **Subscription / PaymentTransaction**: Lưu hành vi thanh toán / Gia hạn từ cổng PayPal.
6. **UserSetting**: Các thiết đặt Extension 1-1 với user.

---

## 4. Pipeline của một Request cơ bản (Ví dụ Create Vocabulary)

1. Gửi HTTP POST request với body `/api/vocabularies`.
2. Controller => Nhận model request -> Mapping sang `CreateVocabularyCommand`. Gửi sang pipeline MediatR.
3. MediatR bắt đầu chạy qua các bước (Behaviors):
   - `PerformanceBehavior`: Bấm giờ đo thời gian request (cảnh báo request chậm hơn 500ms).
   - `ValidationBehavior`: Quét toàn bộ Validator (FluentValidation). Trả ngay lỗi nếu Validate Command đầu vào sai.
4. Command tới Handler (`CreateVocabularyHandler`):
   - Service `ICurrentUserService` tự động trích xuất Id của User từ Header HttpContext JWT.
   - `IFeatureGatingService` kiểm tra user đã chạm ngưỡng giới hạn Account Free chưa.
   - Inject repository `IUnitOfWork` (Repository/DbTransaction pattern).
   - Insert vào cơ sở dữ liệu và lưu db `_uow.SaveChangesAsync()`.
5. Trả về cho client chuẩn dữ liệu `Result<T>` dưới dạng Response tiêu chuẩn.

---

## 5. Những chú ý kỹ thuật và Ưu điểm (Technical Highlights)

- **Return Result<T> thay cho Exception**: Sử dụng Wrapper `Result` xuyên suốt Application giúp tránh lạm dụng `throw Exception` xử lý Logic. Exceptions chỉ nên bắt các lỗi không đoán được từ Hệ thống (Db rớt, ...).
- **Hệ thống Repository hoàn thiện**: Dùng chung các interface tổng quát `IRepository<T>`, `IUnitOfWork` giúp dễ Mock khi viết Unit Test.
- **Cache Sẵn Sàng (Redis)**: Khung hạ tầng đã móc nối Dependency Injection với `AddStackExchangeRedisCache`. Mọi thứ đã có cơ chế cấu hình nếu có setup biến môi trường Connection String.
- **Idempotency Payment**: Việc lưu ngoại khoá `ExternalOrderId` từ PayPal, webhook sẽ xử lý chuẩn không bị tạo double order nếu PayPal gửi lại HTTP Timeout hai lần (Race condition xử lý).
