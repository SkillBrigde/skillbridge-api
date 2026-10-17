# VH-01 — Baseline Identity/Catalog

Ngày xác minh: **2026-10-16**. Người bàn giao: **vuhaidang**.

## Điểm xuất phát chung

- `origin/develop` và `origin/main` cùng ở commit
  **`53b63f76a830919afceadf161e2ac098ad7b9bab`** tại thời điểm kiểm tra.
- Đây là commit merge PR [#5](https://github.com/SkillBrigde/skillbridge-api/pull/5),
  đã chứa phần Identity/Catalog trước đây trên `feat/dang`.
- Tree của baseline giống tree commit `d7330f8` của nhánh cá nhân cũ.
  Không cần merge lại lịch sử cũ hoặc đưa code dở của giai đoạn sau vào branch chung.
- Nhánh tiếp theo tạo từ `origin/develop` mới nhất; không phát triển tiếp từ
  `feat/dang` đã bị xóa trên remote.

## Kết quả xác minh

Checkout sạch của baseline; .NET SDK **10.0.201**, PostgreSQL **18.6** cục bộ.

| Kiểm tra | Kết quả |
|---|---|
| `dotnet restore SkillBridge.slnx` | PASS |
| `dotnet build SkillBridge.slnx --configuration Release --no-restore --warnaserror` | PASS, 0 warnings / 0 errors |
| `dotnet format SkillBridge.slnx --verify-no-changes --no-restore` | PASS |
| `dotnet run --project tests/SkillBridge.Checks --configuration Release --no-build` | PASS: validation, lỗi HTTP, JSON, domain, ranh giới module, migration SQL |
| Cùng lệnh Checks với `-- --database` | PASS: PostgreSQL migrations, HTTP contracts, Admin authorization, refresh rotation/revocation, Catalog concurrency |
| Bootstrapper thật ở Development trên database mới | PASS: migrations, `/health/live`, `/health/ready`, OpenAPI |
| Auth rate limit qua biến thể hoa/thường và dấu `/` cuối URL | PASS: vượt giới hạn trả 429 |

Database checks và smoke test tạo database tên ngẫu nhiên riêng, dọn sau khi chạy;
không dùng database ứng dụng. Smoke test khởi động API ở cổng local ngẫu nhiên,
rồi dừng API và PostgreSQL test. JWT dùng khóa test riêng, không commit bí mật.

Tái chạy database checks với kết nối PostgreSQL test có quyền CREATEDB:

```powershell
$env:SKILLBRIDGE_TEST_DATABASE = '<connection string PostgreSQL test riêng>'
dotnet run --project tests/SkillBridge.Checks --configuration Release --no-build -- --database
```

CI tiếp tục dùng PostgreSQL 17 theo workflow hiện tại. VH-01 mở trigger cho PR/push
trên **develop** bên cạnh main, để branch chung có cùng checks. Kết quả GitHub
Actions cần xem trực tiếp trên PR, không suy từ kết quả local.

## Phạm vi đã ổn định

- **10 Identity API:** đăng ký/đăng nhập, refresh, logout, đổi mật khẩu,
  current user và quản trị user; JWT/security stamp, hashed refresh token,
  lockout và thu hồi session theo triển khai hiện tại.
- **11 Catalog API:** categories, skills, tags; quyền Admin cho ghi, validation,
  cây category, unique slug và transaction/concurrency.
- Module tách biệt, schema/migration history riêng; không FK liên module.

VH-01 không bổ sung API mới. Password recovery, Messaging, Payments, Learning và
Reviews đang làm dở được giữ ngoài nhánh bàn giao này.

## Giới hạn và bước tiếp theo

- `mentor_services` chưa có implementation; VH-02 cần duyệt contract trước nhánh feature.
- Profiles/Booking/Scheduling chưa có implementation trên baseline. Nhánh
  self-service Profiles của nguyendat chỉ được đọc để đối chiếu ID, không tự merge.
- Outbox Identity/Catalog đã lưu dữ liệu; RabbitMQ dispatcher/inbox chưa hoàn thiện.
- Các checks trên không xác nhận toàn hệ thống đã sẵn sàng production.

**VH-01:** baseline Identity/Catalog đã ở branch chung, các checks local đã qua và
commit chung đã được ghi rõ để hai người bắt đầu nhánh riêng.
