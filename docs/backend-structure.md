# Cấu trúc backend

Solution có 10 module. Identity, Profiles, Catalog, Scheduling và Messaging đã có nghiệp vụ; Booking, Payments, Learning, Reviews và Recommendations hiện có đăng ký module và probe endpoint. Mỗi module giữ bốn tầng Domain, Application, Infrastructure, Endpoints và chỉ reference BuildingBlocks.

```text
src/
├── Api/                         Host và nơi ghép các module
│   ├── Program.cs
│   ├── Common/                  DotEnv, health check, adapter liên module
│   ├── Extensions/              Service registration và HTTP pipeline
│   ├── Modules/ModuleRegistry.cs
│   ├── Endpoints/HostEndpoints.cs
│   └── Security/                JWT authentication, CurrentUser
├── BuildingBlocks/              Result, CQRS, event bus, bảo mật kỹ thuật
└── Modules/
    ├── Identity/
    │   ├── Domain/
    │   ├── Application/
    │   │   ├── Auth/
    │   │   │   ├── Login.cs     Command, validator và handler cùng file
    │   │   │   ├── Register.cs
    │   │   │   ├── AuthenticationResult.cs
    │   │   │   └── DTOs/
    │   │   │       ├── Requests/
    │   │   │       └── Responses/
    │   │   └── Users/
    │   │       ├── GetUsers.cs
    │   │       └── DTOs/
    │   │           ├── Requests/
    │   │           └── Responses/
    │   ├── Infrastructure/Persistence/
    │   └── Endpoints/
    └── {Profiles,Catalog,Scheduling,Messaging}/
        ├── Domain/
        ├── Application/
        │   ├── DTOs/
        │   │   ├── Requests/
        │   │   └── Responses/
        │   └── ...              Handler/service, interface, validator
        ├── Infrastructure/
        └── Endpoints/
tests/
├── ArchitectureTests/
├── IntegrationTests/
└── SkillBridge.Checks/
```

## Cách chia thư mục Application

Chỉ giữ nhóm nghiệp vụ có ích khi tìm code: Identity dùng Auth/Users, Profiles dùng Mentors/Mentees. Không tạo một thư mục riêng chỉ để chứa một file use case. Ví dụ `Auth/Login.cs` giữ LoginCommand, LoginCommandValidator và LoginCommandHandler; namespace là `SkillBridge.Modules.Identity.Application.Auth`.

Khi một use case có nhiều file và tài nguyên riêng, có thể tạo thư mục cho nó. Không tạo sẵn thư mục rỗng. Không chuyển các Command/Query nội bộ thành HTTP request DTO.

## Request, kết quả xử lý nội bộ và Response

- `DTOs/Requests/`: DTO đầu vào HTTP, tên kết thúc bằng `Request`. Endpoint ánh xạ sang Command/Query hoặc gọi application service hiện có.
- `DTOs/Responses/`: DTO đầu ra HTTP/SignalR, tên kết thúc bằng `Response`. Response mô tả đúng những trường JSON được trả cho client.
- Kết quả nội bộ không phải response HTTP nằm cạnh phần xử lý. Ví dụ `Auth/AuthenticationResult.cs` chứa refresh token để endpoint ghi cookie; `TokenResponse` chỉ có accessToken và expiresIn.
- Identity đặt DTOs/Requests/Responses trong Auth và Users. Bốn module còn lại đặt trực tiếp dưới Application vì cấu trúc hiện tại chưa cần thêm cấp thư mục cho các hợp đồng HTTP.
- Không khai báo request DTO ở cuối file endpoint hay trộn request và response trong file Contracts. File Contracts còn lại giữ interface, validator và kiểu nội bộ; DTO HTTP nằm trong thư mục tương ứng.

Luồng đăng nhập: `Auth/DTOs/Requests/LoginRequest.cs` → `Auth/Login.cs` → `AuthenticationResult` → `Auth/DTOs/Responses/TokenResponse.cs` và cookie refreshToken. IP đăng nhập lấy từ HttpContext. Refresh và logout chỉ đọc cookie; không tạo DTO body không được sử dụng.

Luồng danh sách người dùng: `Users/DTOs/Requests/GetUsersRequest.cs` → `Users/GetUsers.cs` → `PagedResult<UserSummaryResponse>` → `Users/DTOs/Responses/UsersResponse.cs`. Giữ cả query parameter search và alias searchTerm. GET /me dùng CurrentUserResponse; GET /{userId} dùng UserDetailsResponse.

Việc tổ chức lại giữ nguyên route, field JSON, query parameter, giá trị mặc định, status code, phân quyền và thuộc tính JSON validation. Không đổi entity, schema, migration ID hoặc dữ liệu database.

## Host và dependency

1. Program nạp .env, tạo builder và lấy 10 module từ ModuleRegistry.
2. AddApiServices đăng ký BuildingBlocks, bảo mật, services của module, rate limiter và các adapter liên module tại host.
3. UseApiPipeline chạy exception handler → correlation ID → CORS → authentication → authorization → rate limiter.
4. Development khởi tạo/migrate từng module và bật OpenAPI/Scalar. Production cần áp migration riêng.
5. Host và từng module đăng ký endpoint; liveness kiểm tra tiến trình, readiness kiểm tra database và pending migrations của các module có persistence.

Module không reference module khác. Các adapter trong Api/Common kết nối interface của application với dữ liệu từ module khác. BuildingBlocks không chứa entity/nghiệp vụ và không phụ thuộc Npgsql hay JWT Bearer. Module dùng PostgreSQL/JWT Bearer tự khai báo package kỹ thuật cần thiết; phiên bản package tập trung ở Directory.Packages.props.

DbContext của Identity ở Infrastructure/Persistence; các module có persistence khác hiện đặt DbContext/configuration/migration dưới Infrastructure. Mỗi module sở hữu schema và bảng lịch sử migration riêng. Xem [hướng dẫn migration](database-migrations.md) trước khi sử dụng database cũ; SQL blueprint là tài liệu thiết kế, không thay thế EF migrations.

## Kiểm tra

ArchitectureTests bảo vệ dependency giữa các project. IntegrationTests kiểm tra host, hợp đồng HTTP Identity, JWT, migration model và rate limiter. SkillBridge.Checks kiểm tra domain, SQL migration và các luồng PostgreSQL/HTTP/SignalR thật trong database tạm. Các kiểm thử hợp đồng bảo vệ JSON trong lúc thay tên kiểu C# và đường dẫn source.
