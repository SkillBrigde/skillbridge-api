# SkillBridge API

Hệ thống Backend xây dựng theo kiến trúc **Modular Monolith** trên nền tảng **.NET 10** cho nền tảng kết nối người hướng dẫn (Mentoring Platform) SkillBridge.

---

## 🏛️ Kiến trúc hệ thống

- `src/Api`: Điểm khởi chạy (Composition Root), quản lý HTTP pipeline, health checks, OpenAPI/Scalar và CORS; danh sách module nằm trong `Modules/ModuleRegistry.cs`.
- `src/BuildingBlocks`: Chứa các primitives kỹ thuật dùng chung (`Entity`, `AggregateRoot`, `IDomainEvent`, `Result<T>`, `IModule`); CQRS, pipeline behaviors và integration events nằm trong `Messaging/`.
- `src/Modules/*`: 10 project module độc lập, đã có mã nguồn 106 nghiệp vụ REST + 1 Hub (107 method/path REST gồm VNPAY GET alias). Xem [tiến độ và giới hạn](docs/backend-progress.md); gateway tiền thật cần cấu hình, giải ngân đang khóa.
- **Ranh giới dữ liệu**: Mỗi module sở hữu schema và migration riêng, không tạo khóa ngoại vật lý chéo schema. Mô hình nghiệp vụ dự kiến được trình bày trong tài liệu thiết kế.
- **Giao tiếp liên phân hệ**: Các module không reference chéo trực tiếp. Event bus hiện được đăng ký bằng `InMemoryEventBus`; RabbitMQ/outbox là hướng thiết kế tích hợp.

Xem [cấu trúc toàn backend](docs/backend-structure.md) để biết vai trò của API, BuildingBlocks, các module, test và cách thêm chức năng. `Directory.Build.props` quản lý cấu hình build chung; `Directory.Packages.props` quản lý phiên bản NuGet cho cả 15 project.

---

## 📚 Hệ Thống Tài Liệu Kỹ Thuật & Nghiệp Vụ (Documentation Index)

Dự án đã chuẩn bị đầy đủ bộ tài liệu chuẩn Enterprise cho toàn bộ đội ngũ kỹ thuật:

| Tài liệu                                        | Đường dẫn                                                                                                                 | Nội dung chính                                                                                                                      |
| :---------------------------------------------- | :------------------------------------------------------------------------------------------------------------------------ | :---------------------------------------------------------------------------------------------------------------------------------- |
| 👑 **Tài Liệu Thiết Kế Tổng Thể (Master Docx)** | [SkillBridge_Master_System_Architecture_and_Design_v4.docx](../SkillBridge_Master_System_Architecture_and_Design_v4.docx) | **Tài liệu chính thức v4.0 (Word 842KB)**: Đầy đủ 10 chương, 9 sơ đồ đồ họa HD, kiến trúc Dual Payment VNPAY & PayOS, 24 bảng CSDL. |
| 📊 **Ma Trận User Stories (Excel BA)**          | [SkillBridge_User_Stories_Master_Matrix.xlsx](../SkillBridge_User_Stories_Master_Matrix.xlsx)                             | **Bảng 35 User Stories Agile**: Kèm tiêu chí nghiệm thu Gherkin (Given-When-Then), điểm Story Points, phân bổ 6 Sprints.            |
| 🔌 **Đặc Tả Chi Tiết 65 APIs (Excel BA)**       | [SkillBridge_API_Master_Specification.xlsx](../SkillBridge_API_Master_Specification.xlsx)                                 | **Bảng 65 Endpoints Đầy Đủ**: Chi tiết từng biến đầu vào/đầu ra, kiểu dữ liệu, ràng buộc validation, mã lỗi HTTP.                   |
| 📘 **Hướng Dẫn Cài Đặt & Phát Triển**           | [docs/setup-guide.md](docs/setup-guide.md)                                                                                | Các bước setup, chạy Docker, test API, quy chuẩn CI trước khi tạo PR.                                                               |
| 🗺️ **Tổng Quan Hệ Thống & 7 Bounded Contexts**  | [docs/system-overview.md](docs/system-overview.md)                                                                        | Bức tranh tổng quan, C4 Container, Next.js BFF Zero-Token, Dual Payment Gateway.                                                    |
| 🗄️ **Thiết Kế Cơ Sở Dữ Liệu Chi Tiết**          | [docs/database-design.md](docs/database-design.md)                                                                        | Sơ đồ ERD, 7 Bounded Contexts / 24 bảng chuyên trách, kiểu dữ liệu, index.                                                          |
| **Migration database**                        | [docs/database-migrations.md](docs/database-migrations.md)                                                                | Tạo/xuất SQL, rà soát chuỗi migration và dữ liệu legacy trước khi nâng cấp. |
| ⚙️ **Chi Tiết Luồng Thực Thi Mã Nguồn**         | [docs/code-runtime-flow.md](docs/code-runtime-flow.md)                                                                    | Giải thích file-by-file vòng đời khởi động và luồng xử lý một HTTP Request.                                                         |
| 🤖 **Bộ Prompt / Skill Cho AI Coding**          | [docs/chatgpt-skill-prompt.md](docs/chatgpt-skill-prompt.md)                                                              | Bộ chỉ dẫn để nạp vào AI sinh code chuẩn DDD không vi phạm module.                                                                  |
| **Rà soát bảo mật 2026-10-17**                 | [docs/security-review.md](docs/security-review.md)                                                                      | Các chỉnh sửa quản lý secret và vấn đề phân quyền, phiên đăng nhập còn cần xử lý. |
| 📜 **Quyết Định Kiến Trúc (ADRs)**              | [docs/adr/](docs/adr/)                                                                                                    | Các quyết định kiến trúc đã được chấp thuận (Modular Monolith, Async Events).                                                       |

---

## 🚀 Khởi động nhanh (Quick Start)

### 1. Yêu cầu môi trường

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/)

### 2. Khởi động hạ tầng cục bộ (Database, Cache)

Khi chạy hoàn toàn local, dùng repo `skillbridge-infra`: chạy Docker Desktop, sau đó chạy `./scripts/start.ps1` trong repo đó để bật PostgreSQL 17 và Redis. RabbitMQ/MinIO chỉ bật khi cần bằng `./scripts/start.ps1 -Optional`. Nếu dùng PostgreSQL development chung của nhóm, chỉ chạy `docker compose up -d redis` trong repo infra và đặt chuỗi kết nối API theo endpoint/tài khoản được cấp. Không khởi động đồng thời `docker-compose.yml` của repo API vì sẽ trùng cổng.

Bootstrap của repo infra chỉ chạy khi volume PostgreSQL còn mới. EF migrations sở hữu các bảng được triển khai; [database-blueprint.sql](docs/database-blueprint.sql) chỉ là bản thiết kế tham khảo. Volume hiện có giữ nguyên; database từng tạo theo blueprint `snake_case` cần đối chiếu riêng theo [hướng dẫn migration](docs/database-migrations.md#nguồn-quản-lý-schema-và-bootstrap).

```powershell
Set-Location ../skillbridge-infra
.\scripts\start.ps1
Set-Location ../skillbridge-api
```

Mặc định: PostgreSQL `localhost:5432` (DB/user `skillbridge`) và Redis `localhost:6379`, chỉ lắng nghe ở `127.0.0.1`. Mật khẩu local mẫu trong repo infra là `skillbridge_dev_only`.

### 3. Chạy ứng dụng API

API cần `Jwt__SecretKey`, `ConnectionStrings__Database`, `Redis__ConnectionString` từ `.env` hoặc biến môi trường triển khai; các credential đã được bỏ khỏi `appsettings*.json`. Với checkout mới, tạo `.env` từ `.env.example` rồi sinh key theo [hướng dẫn cấu hình](docs/setup-guide.md#4-cấu-hình-ứng-dụng); dòng `Jwt__SecretKey` trong file mẫu cố ý để trống. Vì `.env.example` của API còn phục vụ Compose cũ, khi dùng repo infra hãy sửa hai dòng sau trong `.env` của API:

```dotenv
ConnectionStrings__Database=Host=localhost;Port=5432;Database=skillbridge;Username=skillbridge;Password=skillbridge_dev_only
Redis__ConnectionString=localhost:6379,password=skillbridge_dev_only
```

Nếu `skillbridge-infra/.env` đã có cấu hình riêng, dùng đúng DB/user/password/port trong file đó thay cho giá trị mẫu.

CORS và địa chỉ phụ thuộc môi trường dùng `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, `AllowedHosts`, `RabbitMq__Host` trong `.env`/biến môi trường. `launchSettings.json` tiếp tục là cấu hình chạy từ IDE; JWT issuer/audience là định danh logic, vẫn có mặc định trong JSON.

Khi API chạy sau Nginx qua Docker trên EC2, đặt `ReverseProxy__KnownProxy` thành đúng IP gateway mà container API nhìn thấy từ Nginx (kiểm tra bằng `docker inspect` trên máy chủ). Chỉ IP này được phép cung cấp `X-Forwarded-For` và `X-Forwarded-Proto`; middleware chỉ đọc một proxy hop trước bộ giới hạn lượt gọi và đăng nhập. Không bật `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` thay cho cấu hình proxy tin cậy vì chế độ đó chấp nhận forwarded headers từ nguồn không được giới hạn.

Key JWT phải được sinh ngẫu nhiên bằng bộ sinh mật mã, tối thiểu 32 byte UTF-8; không đưa key thật vào Git hoặc `appsettings`. Mỗi môi trường triển khai phải cấp secret riêng. Đổi key cần khởi động lại API và làm access token ký bằng key cũ hết hiệu lực.

Với database đã có dữ liệu, đọc [hướng dẫn migration](docs/database-migrations.md) trước khi chạy API Development: host tự áp dụng migration của các module. Đặc biệt không để nhiều branch có migration khác nhau cùng khởi động vào một database chung nếu chưa có quy trình áp dụng migration riêng. Tạo migration/xuất SQL chưa áp dụng thay đổi vào database hiện có.

```powershell
dotnet restore
dotnet build
dotnet run --project src/Api
```

### 4. Tài liệu API trực quan & Health Checks

- 📖 **Giao diện Scalar API (Test API trực quan)**: [http://localhost:5000/scalar/v1](http://localhost:5000/scalar/v1)
- 📄 **OpenAPI Spec JSON**: [http://localhost:5000/openapi/v1.json](http://localhost:5000/openapi/v1.json)
- 💓 **Health Check**: [http://localhost:5000/health/live](http://localhost:5000/health/live)
- 🔍 **Probe Module Identity**: [http://localhost:5000/api/v1/identity/\_module](http://localhost:5000/api/v1/identity/_module)

---

## 🛡️ Quy chuẩn Code & Kiểm tra CI trước khi tạo Pull Request

Trước khi tạo Pull Request, bạn cần đảm bảo mã nguồn vượt qua 3 lệnh kiểm tra của CI:

```powershell
# 1. Kiểm tra định dạng code theo chuẩn .editorconfig
dotnet format SkillBridge.slnx --verify-no-changes

# 2. Biên dịch chế độ Release và chặn toàn bộ Warning
dotnet build SkillBridge.slnx --configuration Release --warnaserror

# 3. Chạy các project xUnit trong tests/ArchitectureTests và tests/IntegrationTests
dotnet test SkillBridge.slnx --configuration Release --no-build --no-restore
```

Kiểm thử PostgreSQL và HTTP thực tế: [hướng dẫn checks](tests/SkillBridge.Checks/README.md).

---

## 📁 Quy ước cấu trúc nội bộ của mỗi Module

Khi thêm mới use case hoặc tạo module mới trong `src/Modules/{ModuleName}/`:

```text
Domain/          Entities, Value Objects, Domain Events và Domain Errors
Application/     Nhóm nghiệp vụ (Identity: Auth, Users), mỗi nhóm có use case, Requests và Responses
Infrastructure/  Persistence/ chứa DbContext, Configurations và Migrations của module
Endpoints/       Minimal API endpoint mappings
{Name}Module.cs  Kế thừa ModuleDefinition, đăng ký DI services, migrate DB và map endpoints
```

Ví dụ: `Application/Auth/Login/Login.cs` chứa command, validator và handler; hợp đồng HTTP nằm riêng ở `Application/Auth/Requests/LoginRequest.cs` và `Application/Auth/Responses/AuthResponse.cs`. Xem [quy ước chi tiết](docs/setup-guide.md#bước-4-tạo-cấu-trúc-4-tầng-nội-bộ-trong-module). Chỉ tạo thư mục khi có mã nguồn; các module còn là khung không cần tạo sẵn cây rỗng.

Kiểm thử ranh giới module nằm trong `tests/ArchitectureTests/SkillBridge.ArchitectureTests.csproj`; kiểm thử endpoint và cấu hình bảo mật nằm trong `tests/IntegrationTests/SkillBridge.IntegrationTests.csproj`.
