# Cấu trúc backend

Quy ước này áp dụng cho toàn bộ solution: API host, BuildingBlocks, cả 10 module và hai project test. Identity hiện có nghiệp vụ Auth/Users; chín module còn lại có điểm đăng ký và probe endpoint, sẽ bổ sung các tầng khi có mã nguồn tương ứng.

```text
Directory.Build.props              Target framework và cấu hình C# chung
Directory.Packages.props           Phiên bản NuGet dùng chung
scripts/init-db/01-init-schemas.sql Extension và 10 schema cho volume PostgreSQL mới
docs/database-blueprint.sql        Bản thiết kế SQL tham khảo, không dùng bootstrap
src/
├── Api/
│   ├── Program.cs                 Ghép các bước khởi động
│   ├── Common/DotEnv.cs           Nạp cấu hình local trước khi tạo host
│   ├── DesignTime/IdentityDbContextFactory.cs Tạo DbContext riêng cho EF tools
│   ├── Extensions/               Đăng ký service và HTTP middleware
│   ├── Modules/ModuleRegistry.cs  Danh sách và vòng đời của 10 module
│   ├── Endpoints/HostEndpoints.cs Root, health checks, OpenAPI/Scalar
│   ├── Middleware/
│   └── Security/                 Xác thực JWT Bearer và CurrentUser qua HTTP
├── BuildingBlocks/
│   ├── BuildingBlocksExtensions.cs
│   ├── Domain/                   Các kiểu nền tảng DDD
│   ├── Modules/                  IModule và ModuleDefinition
│   ├── Messaging/                CQRS, Behaviors, Events
│   ├── Results/                  Result, Error và Pagination
│   ├── Http/                     Chuyển Result thành HTTP response
│   ├── Persistence/Interceptors/ EF Core audit và domain events
│   └── Security/                 Hợp đồng và kỹ thuật bảo mật dùng chung
└── Modules/
    └── {ModuleName}/
        ├── SkillBridge.Modules.{ModuleName}.csproj
        ├── {ModuleName}Module.cs
        ├── Domain/
        ├── Application/
        │   └── {Feature}/
        │       ├── {UseCase}/{UseCase}.cs
        │       ├── Requests/
        │       └── Responses/
        ├── Infrastructure/Persistence/
        │   ├── {ModuleName}DbContext.cs
        │   ├── Configurations/
        │   └── Migrations/
        └── Endpoints/
tests/
├── ArchitectureTests/
└── IntegrationTests/
```

Các thư mục trong module chỉ được tạo khi có code. Quy ước áp dụng cho Identity, Profiles, Catalog, Scheduling, Booking, Payments, Learning, Messaging, Reviews và Recommendations.

## Luồng khởi động

1. `Program.cs` nạp `.env`, tạo builder và lấy danh sách từ `ModuleRegistry.Create()`.
2. `AddApiServices(configuration, modules)` đăng ký lỗi API, OpenAPI, CORS, health check, BuildingBlocks, bảo mật và dịch vụ của từng module.
3. `UseApiPipeline()` giữ thứ tự exception handler → correlation ID → CORS → authentication → authorization.
4. `MapApiDocumentation()` bật tài liệu trong Development; `Program.cs` chỉ gọi khởi tạo/migration module trong Development.
5. `MapHostEndpoints(modules)` và `MapModuleEndpoints(modules)` đăng ký các route hiện có trước `app.Run()`.

Thêm module bằng cách thêm project reference vào API và thêm instance trong `ModuleRegistry.Create()`. Mỗi module tự đăng ký services, endpoints và initialization qua `IModule`.

EF tools dùng `Api/DesignTime/IdentityDbContextFactory.cs` để tạo IdentityDbContext từ `.env`/biến môi trường hoặc đối số cấu hình, không khởi động host hay yêu cầu JWT. Tạo migration/xuất SQL chỉ tạo file; cập nhật database là thao tác riêng. Runtime Development vẫn tự migrate khi khởi động. Xem [hướng dẫn migration](database-migrations.md) trước khi trỏ host vào database cũ.

EF migrations là nguồn quản lý bảng đã triển khai. Bootstrap chỉ tạo extension/schema trên volume mới; [SQL blueprint](database-blueprint.sql) giữ vai trò thiết kế. Việc thay bootstrap không sửa volume hiện có; schema `snake_case` tạo thủ công cần reconciliation riêng trước khi dùng migration EF. Chín module khung chưa có bảng/migration nghiệp vụ được triển khai.

## Request, Command/Query và Response

Endpoint nhận `*Request`, ánh xạ sang Command/Query rồi gửi MediatR. File use case giữ command/query, validator và handler cùng nhau. Kết quả trả về dùng `*Response` trong cùng nhóm nghiệp vụ.

Ví dụ: `Auth/Requests/LoginRequest.cs` → `Auth/Login/Login.cs` (`LoginCommand`) → `Auth/Responses/AuthResponse.cs`. Nhóm Users có `Requests/GetUsersRequest.cs`, `GetUsers/GetUsers.cs` và `Responses/UserResponse.cs`. Tên kiểu và dữ liệu JSON hiện tại được giữ nguyên.

## Dependency

- `Directory.Build.props` quản lý target framework, nullable, implicit usings và phiên bản ngôn ngữ cho mọi project.
- `Directory.Packages.props` quản lý phiên bản package. Trong `.csproj`, dùng `<PackageReference Include="..." />` và không khai báo lại `Version`.
- Module chỉ reference BuildingBlocks. API reference các module và ghép chúng lại.
- JWT Bearer thuộc project API. BuildingBlocks dùng thư viện cấp token trực tiếp, không phụ thuộc JWT Bearer hay Npgsql.
- Module dùng PostgreSQL tự khai báo `Npgsql.EntityFrameworkCore.PostgreSQL`; Identity hiện có dependency này. Khi thêm package mới, khai báo phiên bản tập trung và reference tại project thực sự sử dụng nó.

## Kiểm tra

ArchitectureTests bảo vệ ranh giới project và dependency của BuildingBlocks. IntegrationTests bao gồm test HTTP của Identity, JWT và API host; host test dùng TestServer, kiểm tra root, cả 10 probe endpoint, health, CORS và authentication mà không khởi tạo database.

Đợt tổ chức lại cấu trúc trước đó giữ nguyên route, JSON, logic nghiệp vụ và migration. Việc hoàn thiện schema sau đó được tách thành `CompleteIdentitySchema`, với chính sách khóa tài khoản legacy và yêu cầu mật khẩu mới được mô tả trong [hướng dẫn migration](database-migrations.md). Các việc RBAC/phiên đăng nhập vẫn được theo dõi trong [báo cáo bảo mật](security-review.md). Code ghép từ nhánh khác cần cập nhật namespace theo thư mục mới.
