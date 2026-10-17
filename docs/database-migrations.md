# Quản lý migration database

Hướng dẫn cho EF Core/`dotnet-ef` **10.0.12**, chạy tại thư mục gốc `skillbridge-api`. Tạo migration và xuất SQL chỉ tạo file; chúng không đồng nghĩa với việc đã cập nhật database.

## Nguồn quản lý schema và bootstrap

**EF migrations quản lý các bảng đã được triển khai trong code.** [01-init-schemas.sql](../scripts/init-db/01-init-schemas.sql) chỉ tạo extension và 10 schema rỗng khi PostgreSQL khởi tạo volume mới; không tạo bảng hoặc seed role. Mount của Docker Compose giữ nguyên. Identity có migration cho các bảng đang dùng; chín module còn là khung sẽ thêm migration khi có nghiệp vụ.

SQL thiết kế đầy đủ trước đây được giữ tại [database-blueprint.sql](database-blueprint.sql) để tham khảo, không phải script bootstrap hoặc nguồn nâng cấp database đang chạy. Blueprint dùng cột `snake_case`, trong khi EF Initial hiện dùng tên cột `PascalCase` có quote; chạy cả hai để sở hữu cùng bảng sẽ gây xung đột.

Volume/database hiện có không được khởi tạo lại hoặc thay đổi bởi việc thay file bootstrap. Nếu trước đây đã tạo bảng theo blueprint/script thủ công, cần đối chiếu dữ liệu, mapping và lịch sử EF trong một quy trình reconciliation riêng. `CompleteIdentitySchema` không tự chuyển schema `snake_case` đó sang schema EF. Không xóa volume để ép chạy lại bootstrap trên dữ liệu cần giữ.

## Cấu hình design time

[IdentityDbContextFactory.cs](../src/Api/DesignTime/IdentityDbContextFactory.cs) tạo DbContext cho EF tools mà không khởi động API, không kiểm tra JWT và không gọi luồng tự migrate của host. Factory đọc `.env` qua `DotEnv`, biến môi trường và các đối số cấu hình; không lấy chuỗi kết nối từ `appsettings*.json`.

Cần có `ConnectionStrings__Database` trong `.env` local hoặc biến môi trường. Không đưa chuỗi kết nối có mật khẩu vào lệnh terminal, tài liệu hoặc Git. Biến môi trường đã có được ưu tiên hơn `.env`; đối số cấu hình truyền cho factory có thể ghi đè giá trị. Bảng lịch sử vẫn là `identity.__EFMigrationsHistory`.

Khôi phục đúng phiên bản công cụ theo manifest của repository:

```powershell
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet ef --version
```

## Tạo migration và SQL

Migration [20261017173446_CompleteIdentitySchema.cs](../src/Modules/Identity/Infrastructure/Persistence/Migrations/20261017173446_CompleteIdentitySchema.cs) bổ sung schema còn thiếu sau `20260911052416_Initial_Identity`. Giữ nguyên migration ban đầu và lịch sử đã áp dụng; không xóa hoặc tạo lại Initial để sửa database có dữ liệu.

Lệnh tạo migration tương ứng ở dưới chỉ để tham khảo; **không chạy lại cùng tên nếu file đã có trong repository**. Với thay đổi tiếp theo, dùng tên mới và review diff của migration/snapshot trước khi xuất SQL.

```powershell
dotnet ef migrations add CompleteIdentitySchema `
    --project src/Modules/Identity/SkillBridge.Modules.Identity.csproj `
    --startup-project src/Api/SkillBridge.Api.csproj `
    --context SkillBridge.Modules.Identity.Infrastructure.Persistence.IdentityDbContext `
    --output-dir Infrastructure/Persistence/Migrations
```

Script đầy đủ từ `0` tới migration mới được lưu tại [CompleteIdentitySchema.sql](../scripts/migrations/identity/CompleteIdentitySchema.sql). Lệnh xuất script bên dưới không áp dụng vào database; cần build Release trước vì dùng `--no-build`:

```powershell
dotnet build src/Api/SkillBridge.Api.csproj --configuration Release
New-Item -ItemType Directory -Path scripts/migrations/identity -Force | Out-Null
dotnet ef migrations script 0 20261017173446_CompleteIdentitySchema `
    --project src/Modules/Identity/SkillBridge.Modules.Identity.csproj `
    --startup-project src/Api/SkillBridge.Api.csproj `
    --context SkillBridge.Modules.Identity.Infrastructure.Persistence.IdentityDbContext `
    --configuration Release `
    --no-build `
    --idempotent `
    --output scripts/migrations/identity/CompleteIdentitySchema.sql
```

Script idempotent kiểm tra `__EFMigrationsHistory` để chỉ áp dụng migration chưa chạy; nó không tự xử lý schema lệch hoặc database tạo bằng SQL thủ công. Trước khi áp dụng, đối chiếu schema và bảng lịch sử thực tế. Không tự đánh dấu migration đã chạy hoặc bỏ qua bước đối chiếu. Nếu chỉ cần SQL chênh lệch sau Initial, thay đối số `0` bằng `20260911052416_Initial_Identity` khi xuất sang file riêng.

## Nội dung và dữ liệu legacy

`CompleteIdentitySchema` thêm các cột User còn thiếu cùng bốn bảng `roles`, `user_roles`, `refresh_tokens`, `external_logins`. Không seed role, không đặt mật khẩu mặc định hoặc dùng chung cho các tài khoản cũ.

Với User tồn tại từ schema Initial:

- Backfill `NormalizedEmail` bằng cách bỏ khoảng trắng ở hai đầu rồi chuyển chữ hoa theo collation `C` cho email ASCII in được. Migration dừng an toàn khi gặp email trống, email không thuộc phạm vi này hoặc các email trùng nhau sau chuẩn hóa; thông báo lỗi không in giá trị email. Cần xử lý dữ liệu đó có kiểm soát rồi chạy lại. Đây không phải quy trình gộp tài khoản hay hỗ trợ tự động mọi email Unicode.
- Sinh `SecurityStamp` riêng cho từng User bằng `gen_random_uuid()`.
- Đặt `PasswordHash` thành dấu đánh dấu `!RESET_REQUIRED!` và `IsActive=false`. Dấu này **không phải mật khẩu hay BCrypt hash hợp lệ**; password hasher từ chối nó và các hash sai định dạng mà không ném lỗi.

Các cột cần backfill được tạo nullable trước, sau đó mới chuyển thành NOT NULL. Migration giữ khóa độc quyền trên `identity.users` trong transaction để kiểm tra và backfill nhất quán; cần bố trí thời điểm nâng cấp phù hợp với lưu lượng truy cập.

Chỉ bật lại `IsActive` không giúp tài khoản legacy đăng nhập được. Sau khi xác minh chủ tài khoản, người quản trị đáng tin cậy cần đặt BCrypt hash mới từ mật khẩu riêng của tài khoản, đổi `SecurityStamp`, rồi mới kích hoạt theo quy trình có kiểm soát. **Chưa có chức năng khôi phục/reset mật khẩu tự động hoặc màn hình quản trị cho bước này**; đợt migration không hoàn thành quy trình phục hồi tài khoản. Các giới hạn phiên/JWT đã ghi trong [báo cáo bảo mật](security-review.md) vẫn còn.

## Áp dụng và rollback

Trong lần bổ sung này chưa áp dụng migration vào database hiện có của người dùng. Cần review SQL/backfill, thống kê tài khoản legacy, chuẩn bị quy trình phục hồi và sao lưu có khả năng khôi phục trước khi nâng cấp thực tế. Việc tạo file migration/SQL không thay thế cho kiểm thử nâng cấp trên PostgreSQL biệt lập.

Sau khi xác nhận đúng database đích và hoàn tất các bước trên, lệnh cập nhật tương ứng là:

```powershell
dotnet ef database update 20261017173446_CompleteIdentitySchema `
    --project src/Modules/Identity/SkillBridge.Modules.Identity.csproj `
    --startup-project src/Api/SkillBridge.Api.csproj `
    --context SkillBridge.Modules.Identity.Infrastructure.Persistence.IdentityDbContext
```

Lệnh này thay đổi database. API chạy trong **Development** cũng đang gọi `MigrateAsync()` khi khởi động module; vì vậy không khởi chạy host trên database cũ trước khi đã review/chuẩn bị nâng cấp. Design-time factory chỉ tách EF tools khỏi luồng khởi động này, không tắt cơ chế runtime đó.

`Down` xóa các bảng/cột mới và dữ liệu trong chúng; không thể phục hồi giá trị `IsActive` ban đầu của tài khoản legacy. Rollback migration không thay thế cho khôi phục backup. Không thực hiện downgrade trên database có dữ liệu nếu chưa có kế hoạch bảo toàn dữ liệu.

## Phạm vi kiểm chứng lần bổ sung

Đã có 109 kiểm thử chạy thành công ngoài PostgreSQL và EF xác nhận không còn pending model changes. Docker engine chưa khởi động được, nên chưa xác nhận SQL upgrade/backfill/rollback trên PostgreSQL thật. Các kiểm tra này không chứng minh database hiện có đã tương thích hoặc đã được nâng cấp.
