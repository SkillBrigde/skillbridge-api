# SkillBridge API

Hệ thống Backend xây dựng theo kiến trúc **Modular Monolith** trên nền tảng **.NET 10** cho nền tảng kết nối người hướng dẫn (Mentoring Platform) SkillBridge.

---

## 🏛️ Kiến trúc hệ thống

- `src/Bootstrapper/SkillBridge.Api`: Điểm khởi chạy (Composition Root), quản lý HTTP pipeline, health checks, OpenAPI/Scalar, CORS, rate limiting.
- `src/BuildingBlocks/SkillBridge.BuildingBlocks`: Chứa các primitives kỹ thuật dùng chung (`Entity`, `AggregateRoot`, `IDomainEvent`, `Result<T>`, `IModule`).
- `src/Modules/*`: Các module nghiệp vụ độc lập (`Identity`, `Profiles`, `Catalog`, `Booking`, `Scheduling`, `Payments`, `Learning`, `Messaging`, `Reviews`, `Recommendations`).
- **Ranh giới dữ liệu**: Mỗi module tự quản lý quy tắc nghiệp vụ và schema PostgreSQL riêng biệt (`identity.*`, `profiles.*`,...).
- **Giao tiếp liên module**: Các module **không reference chéo trực tiếp**. Sử dụng contracts và integration events qua RabbitMQ.

---

## 📚 Hệ Thống Tài Liệu Kỹ Thuật (Documentation Index)

Dự án đã chuẩn bị đầy đủ bộ tài liệu chuẩn Enterprise cho toàn bộ đội ngũ kỹ thuật:

| Tài liệu | Đường dẫn | Nội dung chính |
| :--- | :--- | :--- |
| 📘 **Hướng Dẫn Cài Đặt & Phát Triển** | [docs/setup-guide.md](docs/setup-guide.md) | Các bước setup, chạy Docker, test API, quy chuẩn CI trước khi tạo PR. |
| 🗺️ **Tổng Quan Hệ Thống & 10 Modules** | [docs/system-overview.md](docs/system-overview.md) | Bức tranh tổng quan, chi tiết 10 Bounded Contexts, Outbox Pattern, Event-driven. |
| 🗄️ **Thiết Kế Cơ Sở Dữ Liệu Chi Tiết** | [docs/database-design.md](docs/database-design.md) | Sơ đồ ERD, thiết kế chi tiết từng bảng, kiểu dữ liệu, index của cả 10 schemas. |
| ⚙️ **Chi Tiết Luồng Thực Thi Mã Nguồn** | [docs/code-runtime-flow.md](docs/code-runtime-flow.md) | Giải thích file-by-file vòng đời khởi động và luồng xử lý một HTTP Request. |
| 🤖 **Bộ Prompt / Skill Cho ChatGPT** | [docs/chatgpt-skill-prompt.md](docs/chatgpt-skill-prompt.md) | Bộ chỉ dẫn để nạp vào ChatGPT sinh code chuẩn DDD không vi phạm module. |
| 📜 **Quyết Định Kiến Trúc (ADRs)** | [docs/adr/](docs/adr/) | Các quyết định kiến trúc đã được chấp thuận (Modular Monolith, Async Events). |

---

## 🚀 Khởi động nhanh (Quick Start)

### 1. Yêu cầu môi trường
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/)

### 2. Khởi động hạ tầng cục bộ (Database, Broker, Cache)
Chạy lệnh duy nhất để bật PostgreSQL 17 (kèm sẵn 10 schemas), RabbitMQ và Redis:

```powershell
docker compose up -d
```

| Dịch vụ | Cổng Host | Thông tin đăng nhập / Web UI |
| :--- | :--- | :--- |
| **PostgreSQL 17** | `5432` | DB: `skillbridge` / User: `skillbridge` / Pass: `skillbridge` |
| **RabbitMQ** | `5672` (AMQP)<br/>`15672` (UI) | [http://localhost:15672](http://localhost:15672) (User: `skillbridge` / Pass: `skillbridge`) |
| **Redis** | `6379` | Mặc định localhost:6379 |

### 3. Chạy ứng dụng API

```powershell
dotnet restore
dotnet build
dotnet run --project src/Bootstrapper/SkillBridge.Api
```

### 4. Tài liệu API trực quan & Health Checks

- 📖 **Giao diện Scalar API (Test API trực quan)**: [http://localhost:5000/scalar/v1](http://localhost:5000/scalar/v1)
- 📄 **OpenAPI Spec JSON**: [http://localhost:5000/openapi/v1.json](http://localhost:5000/openapi/v1.json)
- 💓 **Health Check**: [http://localhost:5000/health/live](http://localhost:5000/health/live)
- 🔍 **Probe Module Identity**: [http://localhost:5000/api/v1/identity/_module](http://localhost:5000/api/v1/identity/_module)

---

## 🛡️ Quy chuẩn Code & Kiểm tra CI trước khi tạo Pull Request

Trước khi tạo Pull Request, bạn cần đảm bảo mã nguồn vượt qua 2 lệnh kiểm tra của CI:

```powershell
# 1. Kiểm tra định dạng code theo chuẩn .editorconfig
dotnet format SkillBridge.slnx --verify-no-changes

# 2. Biên dịch chế độ Release và chặn toàn bộ Warning
dotnet build SkillBridge.slnx --configuration Release --warnaserror
```

---

## 📁 Quy ước cấu trúc nội bộ của mỗi Module

Khi thêm mới use case hoặc tạo module mới trong `src/Modules/{ModuleName}/`:

```text
Domain/          Entities, Value Objects, Domain Events và Domain Errors
Application/     Commands, Queries, Validators và DTOs
Infrastructure/  Persistence (ModuleDbContext theo schema PostgreSQL riêng), Repositories
Endpoints/       Minimal API endpoint mappings
{Name}Module.cs  Kế thừa ModuleDefinition, đăng ký DI services, migrate DB và map endpoints
```
