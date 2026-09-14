# SkillBridge API

Hệ thống Backend xây dựng theo kiến trúc **Modular Monolith** trên nền tảng **.NET 10** cho nền tảng kết nối người hướng dẫn (Mentoring Platform) SkillBridge.

---

## 🏛️ Kiến trúc hệ thống

- `src/Bootstrapper/SkillBridge.Api`: Điểm khởi chạy (Composition Root), quản lý HTTP pipeline, health checks, OpenAPI/Scalar, CORS, rate limiting.
- `src/BuildingBlocks/SkillBridge.BuildingBlocks`: Chứa các primitives kỹ thuật dùng chung (`Entity`, `AggregateRoot`, `IDomainEvent`, `Result<T>`, `IModule`).
- `src/Modules/*`: Các phân hệ nghiệp vụ độc lập theo **7 Bounded Contexts** (1. Identity & RBAC, 2. Sessions & Thiết Bị, 3. Profiles & Services, 4. Scheduling & Slots, 5. Bookings & Sessions, 6. Payments & Dual Escrow, 7. Trust & Disputes).
- **Ranh giới dữ liệu**: Mỗi phân hệ quản lý 24 bảng chuyên trách, giao tiếp bằng decoupled GUIDv7, không khóa ngoại vật lý chéo schema.
- **Giao tiếp liên phân hệ**: Các phân hệ **không reference chéo trực tiếp**. Sử dụng contracts và integration events qua RabbitMQ.

---

## 📚 Hệ Thống Tài Liệu Kỹ Thuật & Nghiệp Vụ (Documentation Index)

Dự án đã chuẩn bị đầy đủ bộ tài liệu chuẩn Enterprise cho toàn bộ đội ngũ kỹ thuật:

| Tài liệu | Đường dẫn | Nội dung chính |
| :--- | :--- | :--- |
| 👑 **Tài Liệu Thiết Kế Tổng Thể (Master Docx)** | [SkillBridge_Master_System_Architecture_and_Design_v4.docx](../SkillBridge_Master_System_Architecture_and_Design_v4.docx) | **Tài liệu chính thức v4.0 (Word 842KB)**: Đầy đủ 10 chương, 9 sơ đồ đồ họa HD, kiến trúc Dual Payment VNPAY & PayOS, 24 bảng CSDL. |
| 📊 **Ma Trận User Stories (Excel BA)** | [SkillBridge_User_Stories_Master_Matrix.xlsx](../SkillBridge_User_Stories_Master_Matrix.xlsx) | **Bảng 35 User Stories Agile**: Kèm tiêu chí nghiệm thu Gherkin (Given-When-Then), điểm Story Points, phân bổ 6 Sprints. |
| 🔌 **Đặc Tả Chi Tiết 65 APIs (Excel BA)** | [SkillBridge_API_Master_Specification.xlsx](../SkillBridge_API_Master_Specification.xlsx) | **Bảng 65 Endpoints Đầy Đủ**: Chi tiết từng biến đầu vào/đầu ra, kiểu dữ liệu, ràng buộc validation, mã lỗi HTTP. |
| 📘 **Hướng Dẫn Cài Đặt & Phát Triển** | [docs/setup-guide.md](docs/setup-guide.md) | Các bước setup, chạy Docker, test API, quy chuẩn CI trước khi tạo PR. |
| 🗺️ **Tổng Quan Hệ Thống & 7 Bounded Contexts** | [docs/system-overview.md](docs/system-overview.md) | Bức tranh tổng quan, C4 Container, Next.js BFF Zero-Token, Dual Payment Gateway. |
| 🗄️ **Thiết Kế Cơ Sở Dữ Liệu Chi Tiết** | [docs/database-design.md](docs/database-design.md) | Sơ đồ ERD, 7 Bounded Contexts / 24 bảng chuyên trách, kiểu dữ liệu, index. |
| ⚙️ **Chi Tiết Luồng Thực Thi Mã Nguồn** | [docs/code-runtime-flow.md](docs/code-runtime-flow.md) | Giải thích file-by-file vòng đời khởi động và luồng xử lý một HTTP Request. |
| 🤖 **Bộ Prompt / Skill Cho AI Coding** | [docs/chatgpt-skill-prompt.md](docs/chatgpt-skill-prompt.md) | Bộ chỉ dẫn để nạp vào AI sinh code chuẩn DDD không vi phạm module. |
| 📜 **Quyết Định Kiến Trúc (ADRs)** | [docs/adr/](docs/adr/) | Các quyết định kiến trúc đã được chấp thuận (Modular Monolith, Async Events). |

---

## 🚀 Khởi động nhanh (Quick Start)

### 1. Yêu cầu môi trường
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/)

### 2. Khởi động hạ tầng cục bộ (Database, Broker, Cache)
Chạy lệnh duy nhất để bật PostgreSQL 17 (kèm sẵn 7 schemas / 24 bảng), RabbitMQ và Redis:

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
