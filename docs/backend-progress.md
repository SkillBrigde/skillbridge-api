# Trạng thái triển khai backend

Ngày cập nhật: 2026-09-16.

## Phạm vi đợt triển khai đầu tiên

Repository ban đầu có 10 module nhưng 9 module chỉ có endpoint probe. Identity có một endpoint dữ liệu mẫu và một endpoint đọc users chưa bảo vệ. Đợt này xây dựng luồng tài khoản và phiên thực tế để các module sau dùng được xác thực.

Nguồn nghiệp vụ chính: Word v4, API Master Specification v2 và User Stories Master Matrix v2. Các tài liệu Markdown cũ vẫn được giữ để tham khảo; khi có khác biệt, phạm vi Identity/Sessions hiện tại theo bảng Excel v2.

| Nhóm | Trạng thái |
| --- | --- |
| Đăng ký email, PBKDF2, kiểm tra mật khẩu và email trùng | Đã triển khai |
| Đăng nhập, JWT 15 phút, khóa 15 phút sau 5 lần sai | Đã triển khai |
| Refresh token SHA-256, rotation và thu hồi toàn phiên khi replay | Đã triển khai |
| Hồ sơ tài khoản cá nhân, đổi mật khẩu | Đã triển khai |
| Danh sách user phân trang, chi tiết, khóa/mở khóa bằng Admin/SuperAdmin | Đã triển khai |
| Danh sách phiên, thu hồi một phiên, thu hồi các phiên khác | Đã triển khai |
| Migration, readiness database, auth rate limiter, Result validation | Đã triển khai |
| Outbox sự kiện đăng ký | Lưu cùng transaction; chưa có publisher RabbitMQ |
| Google OAuth, xác minh email, quên/reset mật khẩu, lịch sử đăng nhập | Chưa triển khai |
| RBAC | JWT hỗ trợ nhiều role; hiện lưu text[] và giữ Role cũ để tương thích. Chưa có roles/user_roles và quy trình gán thêm role |
| Profiles, Catalog, Scheduling, Booking, Payments, Learning, Messaging, Reviews, Recommendations | Chưa có API nghiệp vụ |

## API hiện có

13 endpoint nghiệp vụ, gồm 12 endpoint trong Excel v2 và một endpoint chi tiết user từ Markdown:

| Method | Route | Quyền |
| --- | --- | --- |
| POST | /api/v1/identity/auth/register | Anonymous |
| POST | /api/v1/identity/auth/login | Anonymous |
| POST | /api/v1/identity/auth/refresh | Refresh token |
| POST | /api/v1/identity/auth/logout | Authenticated |
| POST | /api/v1/identity/auth/change-password | Authenticated |
| GET, PUT | /api/v1/identity/users/me | Authenticated |
| GET | /api/v1/identity/users | Admin/SuperAdmin |
| GET | /api/v1/identity/users/{userId} | Admin/SuperAdmin |
| PUT | /api/v1/identity/users/{userId}/status | Admin/SuperAdmin |
| GET | /api/v1/sessions | Authenticated |
| DELETE | /api/v1/sessions/{sessionId} | Chủ phiên |
| DELETE | /api/v1/sessions/other | Authenticated |

Đăng nhập trả `accessToken`, `refreshToken`, `expiresIn`, `sessionId`, `user`.
Refresh nhận JSON `{ "refreshToken": "...", "sessionId": "..." }`.
Token dành cho BFF lưu phía server; phần BFF/cookie chưa nằm trong repository này.

Cập nhật hồ sơ nhận `fullName`, `avatarUrl` (HTTPS hoặc null).
Đổi mật khẩu nhận `currentPassword`, `newPassword`, `confirmNewPassword`.
Cập nhật trạng thái nhận `status` (Active/Locked) và `lockReason`.
Danh sách user nhận `search`, `role`, `status`, `page`, `pageSize` (1–100).
Lỗi nghiệp vụ trả ProblemDetails với mã trong `title`; validation không dùng exception.

## Quyết định và giới hạn

- Giữ nguyên 10 project module; 7 nhóm nghiệp vụ trong tài liệu không đồng nghĩa phải gộp project.
- Token có thời hạn phiên tuyệt đối 7 ngày, refresh không kéo dài vô hạn. `rememberMe` được nhận để tương thích nhưng chưa thay đổi thời hạn.
- Mỗi request có JWT hợp lệ kiểm tra user/session/security stamp trong PostgreSQL. Thu hồi phiên hoặc khóa user có hiệu lực ở request kế tiếp. Chưa dùng Redis session cache.
- Mọi thay đổi phiên cùng tài khoản khóa hàng user bằng PostgreSQL FOR UPDATE trong transaction; hai refresh đồng thời không thể cùng tiêu thụ một token.
- Đổi mật khẩu thu hồi tất cả phiên, kể cả phiên hiện tại. Client cần đăng nhập lại.
- Endpoint thu hồi phiên trả 404 nếu phiên thuộc người khác để không tiết lộ định danh của họ.
- LastActiveAtUtc hiện cập nhật khi đăng nhập/refresh, chưa cập nhật ở từng request.
- Không tạo tài khoản Admin/mật khẩu mặc định. Provisioning quản trị cần quy trình riêng.
- Chưa có consumer Profiles. Outbox giữ pending, không đánh dấu đã giao khi chưa gửi RabbitMQ.
- Auth rate limit mặc định 20 request/phút/IP, giới hạn trong từng process. BFF/reverse proxy cần cấu hình proxy tin cậy và giới hạn bổ sung trước khi triển khai nhiều instance. Không tin trực tiếp X-Forwarded-For từ client.
- API thành công trả DTO trực tiếp, lỗi dùng ProblemDetails như nền code hiện tại; chưa dùng envelope tổng quát trong trang tổng quan Excel.

## Database và nâng cấp

Docker init chỉ tạo 10 schema; EF migration là nguồn tạo bảng chính thức. DDL cũ đã chuyển vào `docs/reference/legacy-database-blueprint.sql`, không còn tự chạy.

Database được tạo bằng migration Identity cũ được nâng cấp bằng migration mới, giữ user và role cũ. User cũ chưa có password hash không thể đăng nhập bằng mật khẩu cho đến khi có luồng thiết lập/reset.

Nếu volume từng chạy DDL cũ với cột snake_case và chưa có lịch sử EF, **không chạy migration trực tiếp lên volume đó**: cấu trúc không tương thích. Sao lưu, dùng database mới cho môi trường phát triển hoặc lập migration chuyển đổi dữ liệu riêng. Thay đổi này không xóa hay tự sửa volume hiện có.

## Kiểm thử

Kết quả kiểm tra tại máy phát triển: build Release với `--warnaserror` thành công (0 warnings/errors); 24 test passed, 0 skipped, gồm PostgreSQL 17 portable trong thư mục tạm. Database test được tạo riêng và xóa sau khi test kết thúc.

```powershell
dotnet restore
dotnet build SkillBridge.slnx --configuration Release --no-restore --warnaserror
dotnet format SkillBridge.slnx --verify-no-changes --no-restore
dotnet test SkillBridge.slnx --configuration Release --no-build
```

Test tích hợp PostgreSQL dùng `SKILLBRIDGE_TEST_DATABASE` trỏ tới database kiểm thử và user có quyền CREATEDB. Test tạo database riêng tên `sb_tests_<guid>`, chạy migration, kiểm tra luồng HTTP, rồi chỉ xóa database test đó. Không đặt biến này trỏ tới môi trường production.

```powershell
$env:SKILLBRIDGE_TEST_DATABASE = "Host=localhost;Database=postgres;Username=skillbridge;Password=skillbridge"
dotnet test SkillBridge.slnx --configuration Release
```

CI đã cấu hình PostgreSQL 17 và biến kết nối để ca tích hợp chạy, không bị skip.

## Thứ tự triển khai tiếp

1. Hoàn thiện Identity: Google/BFF, email, RBAC chuẩn bảng, session audit và provisioning quản trị.
2. Publisher outbox, RabbitMQ, inbox idempotency và hợp đồng sự kiện độc lập để Profiles nhận sự kiện.
3. Catalog, hồ sơ Mentor, gói dịch vụ và KYC.
4. Lịch rảnh, slot, Redis hold, Booking và intake form.
5. PayOS/VNPAY, webhook xác thực, escrow/ledger, payout và đối soát.
6. Learning/proof, disputes/reviews, chat và recommendations.
