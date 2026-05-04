# LexiVocab API 🚀

Hệ thống Backend cho LexiVocab, được xây dựng trên nền tảng .NET 10, PostgreSQL và Redis.

## 🛠️ Yêu cầu hệ thống
- **Docker** & **Docker Compose**
- **.NET 10 SDK** (nếu muốn chạy không dùng Docker)

## ⚡ Hướng dẫn chạy nhanh (với Docker)

1. **Cấu hình môi trường:**
   Tạo file `.env` từ file mẫu:
   ```bash
   cp .env.example .env
   ```
   *Lưu ý: Điền các thông số cần thiết như API Key, JWT Secret trong file `.env`.*

2. **Khởi động hệ thống:**
   ```bash
   docker-compose up -d
   ```

3. **Truy cập các dịch vụ:**
   - **API:** [http://localhost:5000](http://localhost:5000) (Swagger: `/swagger`)
   - **Logs (Seq):** [http://localhost:8899](http://localhost:8899)
   - **Database (pgAdmin):** [http://localhost:5050](http://localhost:5050)

## 💻 Chạy ở chế độ Development (Local)

Nếu bạn muốn chạy trực tiếp bằng lệnh dotnet:

1. Đảm bảo Postgres và Redis đang chạy (có thể dùng docker-compose cho 2 cái này).
2. Chạy lệnh:
   ```bash
   dotnet run --project src/LexiVocab.API
   ```

## 📂 Cấu trúc dự án
- `src/`: Mã nguồn chính của API.
- `tests/`: Unit & Integration tests.
- `docker-compose.yml`: Cấu hình container hóa toàn bộ stack.
