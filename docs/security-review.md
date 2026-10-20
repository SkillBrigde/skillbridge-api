# Rà soát bảo mật — 2026-10-17

Rà soát mã nguồn backend hiện tại, 32 commit trong lịch sử Git local và các file cấu hình liên quan của `skillbridge-infra`/`skillbridge-web`. Không ghi giá trị secret trong báo cáo. Đây là kiểm tra mã nguồn/cấu hình; không quét production, cloud hoặc thử khai thác database đang chạy.

## Đã xử lý trong working tree

- Bỏ credential JWT, database và RabbitMQ khỏi `appsettings*.json`; bỏ chuỗi kết nối DB dự phòng hardcode trong Identity. Cấu hình kết nối phải được cấp qua môi trường hoặc `.env` local.
- Sinh key JWT local ngẫu nhiên mới trong `.env` được bỏ qua bởi Git. Thông tin kết nối DB/broker local hiện có được giữ nguyên.
- Dừng khởi động nếu thiếu `Jwt__SecretKey` hoặc key ngắn hơn 32 byte UTF-8. `.env.example` để trống key; checkout mới phải tự sinh key, mỗi môi trường triển khai dùng secret riêng.
- Bổ sung loại `.env` và các biến thể khỏi Git và Docker build context; giữ file mẫu để hướng dẫn cấu hình.

Đây là thay đổi trong workspace, chưa xác nhận được triển khai lên môi trường ngoài. Không đổi mật khẩu của server DB/broker, không thu hồi phiên đăng nhập đang có trên môi trường khác và không viết lại lịch sử Git.

## Việc còn cần xử lý

| Mức | Phát hiện và bằng chứng | Sửa tối thiểu và ảnh hưởng |
| --- | --- | --- |
| P1 | API danh sách người dùng và khóa/mở khóa chỉ yêu cầu đăng nhập, chưa yêu cầu role Admin: [UsersEndpoints.cs](../src/Modules/Identity/Endpoints/UsersEndpoints.cs#L53), dòng 53 và 67. Người dùng thường có thể gọi các thao tác được mô tả là dành cho quản trị viên. | Áp policy/role Admin cho hai endpoint, kiểm tra quyền trong luồng xử lý nếu được gọi ngoài HTTP và thêm kiểm thử user thường nhận 403. Đây là thay đổi phân quyền, cần xác nhận role Admin đúng với JWT hiện tại. |
| P2 | Refresh token và token thay thế được lưu dạng có thể sử dụng trực tiếp: [RefreshToken.cs](../src/Modules/Identity/Domain/RefreshToken.cs#L8). Nếu dữ liệu bị đọc trái phép, token còn hạn có thể được dùng để lấy phiên mới. | Lưu hash của refresh token ngẫu nhiên, liên kết token thay thế bằng ID/hash. Cần migration và kế hoạch chuyển tiếp/thu hồi token đang lưu. |
| P2 | Đổi mật khẩu chỉ cập nhật hash mật khẩu, chưa thu hồi refresh session: [ChangePassword.cs](../src/Modules/Identity/Application/Auth/ChangePassword.cs#L62). | Thu hồi các refresh session theo chính sách đã chốt khi đổi mật khẩu. Muốn vô hiệu access token ngay cần thêm cơ chế kiểm tra phiên/token version; người dùng sẽ phải đăng nhập lại. |
| P2 | Refresh đọc token còn hoạt động rồi thu hồi/tạo token mới trong các bước riêng, chưa có cơ chế bảo đảm một token chỉ được đổi thành công một lần: [RefreshToken.cs](../src/Modules/Identity/Application/Auth/RefreshToken.cs#L41). Hai request đồng thời có thể cùng đi qua bước kiểm tra. | Dùng cập nhật có điều kiện hoặc concurrency control trong transaction, kiểm tra kết quả thu hồi và từ chối request còn lại. Cần kiểm thử hai request refresh đồng thời. |
| Đã xử lý | Response HTTP hiện dùng [TokenResponse.cs](../src/Modules/Identity/Application/Auth/DTOs/Responses/TokenResponse.cs), chỉ chứa accessToken và expiresIn. | AuthenticationResult là kết quả nội bộ; refresh token chỉ được ghi vào cookie HttpOnly như contract hiện tại. |
| P2 | Khóa tài khoản chưa vô hiệu JWT đã cấp; [AuthenticationExtensions.cs](../src/Api/Security/AuthenticationExtensions.cs) kiểm tra chữ ký/thời hạn nhưng không kiểm tra trạng thái tài khoản. Access token cũ còn dùng được tới lúc hết hạn. | Chốt chính sách khóa ngay hay chờ hết hạn; nếu khóa ngay, kiểm tra trạng thái/token version khi xác thực và thu hồi refresh session. Cần cân nhắc truy vấn DB/cache và kiểm thử access token cũ. |
| P2 | Compose local publish PostgreSQL 5432, RabbitMQ 5672/15672 và Redis 6379 trên mọi giao diện host; Redis chưa có mật khẩu: [docker-compose.yml](../docker-compose.yml#L11), dòng 11, 29–30, 44. | Với môi trường phát triển, bind cổng cần dùng vào `127.0.0.1`; dịch vụ không cần truy cập từ host thì không publish. Khi dùng chung mạng, cấu hình xác thực Redis và thông tin truy cập riêng; thay đổi có thể ảnh hưởng công cụ truy cập từ máy khác. |

## Lịch sử và giới hạn kết luận

Lịch sử Git vẫn chứa các signing key và mật khẩu mẫu development trước đây. Việc xóa khỏi file hiện tại không xóa chúng khỏi commit cũ. Không phát hiện file `.env` thật/private key trong các đường dẫn lịch sử đã kiểm tra, hoặc mẫu token nhà cung cấp/private key/credential production rõ ràng; điều này không chứng minh mọi secret đều đã được phát hiện.

Các cấu hình infra lân cận vẫn có giá trị mặc định development. Không phát hiện backend signing key được sao chép sang cấu hình infra/web trong phạm vi kiểm tra. Secret mẫu development trong lịch sử không phải bằng chứng production đã bị xâm phạm.

Nếu một môi trường đã dùng key hoặc mật khẩu từng được commit, cần thay chúng tại chính môi trường đó. Đổi signing key yêu cầu restart các API sử dụng key và làm access token ký bằng key cũ mất hiệu lực; refresh session cần xử lý riêng. Chưa thực hiện rotation secret ở dịch vụ triển khai hoặc thay mật khẩu DB/broker. Việc sửa các phát hiện P1/P2 ở trên cần thay đổi chức năng, migration hoặc hạ tầng riêng, ngoài đợt chuẩn hóa này.
