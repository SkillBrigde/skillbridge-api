# Tiến độ backend SkillBridge

Cập nhật: 24/09/2026. Đây là kiểm kê mã nguồn và phạm vi triển khai đợt đầu; tài liệu thiết kế không đồng nghĩa tính năng đã hoạt động. Endpoint `/_module` chỉ xác nhận module được đăng ký.

## Kiểm kê 10 module

| Module | Trạng thái đầu đợt | Phạm vi đợt này / điều kiện tiếp theo |
|---|---|---|
| Identity | Có 8 API nghiệp vụ, mô hình EF và migration | Củng cố xác thực, validation, refresh token; thêm cập nhật hồ sơ cơ bản và chi tiết user cho Admin, tổng 10 API |
| Catalog | Chỉ có module stub | Triển khai 11 API categories, skills, tags theo contract; domain, EF, migration và phân quyền Admin |
| Profiles | Chỉ có module stub | Hồ sơ Mentor/Mentee, quyền sở hữu và xử lý đăng ký sau khi thống nhất hợp đồng sự kiện |
| Scheduling | Chỉ có module stub | Cài đặt, lịch tuần, ngày nghỉ, sinh slot; cần thống nhất múi giờ và quy tắc slot |
| Booking | Chỉ có module stub | Cần dịch vụ của Mentor, slot, intake form, khóa giữ chỗ và trạng thái thanh toán đáng tin cậy |
| Payments | Chỉ có module stub | Cần contract Booking, sổ giao dịch, idempotency, chữ ký gateway và quy tắc escrow |
| Learning | Chỉ có module stub | Tạo buổi học từ booking đã xác nhận, kiểm tra người tham gia, điểm danh và tài liệu |
| Messaging | Chỉ có module stub | Hội thoại, tin nhắn lưu bền và kiểm tra thành viên trước REST/SignalR |
| Reviews | Chỉ có module stub | Cần bằng chứng session hoàn tất, một review/booking, cửa sổ sửa 48 giờ và thống kê |
| Recommendations | Chỉ có module stub | Cần dữ liệu kỹ năng, hồ sơ, booking, session, review và tương tác thực tế |

Số API ở trên không tính endpoint probe. Bắt đầu đợt này chỉ Identity có migration; không có căn cứ coi 46 bảng trong blueprint là đã triển khai.

## Các điểm tài liệu chưa thống nhất

- **Hợp đồng liên module:** ADR 0002 yêu cầu RabbitMQ, transactional outbox và inbox. Runtime ban đầu chỉ có `InMemoryEventBus`; `OutboxMessage` có sẵn chưa chứng minh có dispatcher hoặc giao nhận bền vững. Sự kiện hiện đặt trong assembly của module; cần tách hợp đồng phù hợp trước khi có consumer, không reference chéo module và không đưa nghiệp vụ vào BuildingBlocks.
- **Identity:** Contract liệt kê 12 API; mã nguồn ban đầu có 8. Đợt này thêm cập nhật `/users/me` và chi tiết user cho Admin; quên/đặt lại mật khẩu còn hoãn. Blueprint nhắc `user_sessions` nhưng danh mục bảng Identity không có bảng này. Ghi sự kiện đăng ký vào outbox chưa đồng nghĩa hồ sơ Profiles đã được tạo.
- **Catalog:** Blueprint có `mentor_services`, nhưng contract Catalog hiện chỉ mô tả categories, skills và tags. Gói dịch vụ cần contract riêng trước khi triển khai Booking; không tự suy diễn API từ tên bảng.
- **Scheduling:** Contract dùng ngày nghỉ dạng ngày và cài đặt `timezone`; blueprint dùng khoảng UTC và thiếu timezone. Phần mô tả nói hủy slot, endpoint lại chuyển `Available` sang `Locked`. Cần thống nhất ngày kết thúc, múi giờ, DST và ảnh hưởng tới slot đã giữ/đặt.
- **Booking:** Tạo booking dùng `serviceId` và intake form, nhưng response chi tiết vẫn theo mô hình `topic`/một slot. Blueprint có nhiều `booking_sessions`. Quy tắc dời lịch đưa vào `Rescheduled`, trạng thái này không có trong state machine/blueprint. Confirm yêu cầu tiền đã vào escrow nhưng vẫn gọi trạng thái `PendingPayment`; không được xác nhận chỉ dựa vào trạng thái này.
- **Payments:** Contract dùng `balance` và escrow `Released`; blueprint tách available/held balance và dùng `PartiallyReleased`/`FullyReleased`. Thời gian khiếu nại ghi 24–48 giờ, tổng quan ghi 24 giờ và T+3. Trước khi thu tiền cần chốt mô hình milestone, phí và lịch giải ngân; số tiền booking phải lấy từ dữ liệu tin cậy, không tin `amount` do client gửi.
- **Learning:** Contract liên kết `booking_id`, blueprint liên kết `booking_session_id`; trạng thái và trường thời gian cũng khác nhau. Cần quyết định quan hệ nhiều buổi học cho một gói trước migration.
- **Messaging / Reviews / Recommendations:** Contract và blueprint khác cấu trúc thành viên/đã đọc, trường review/tổng hợp và loại tương tác. Disputes xuất hiện trong blueprint nhưng chưa có API contract. Quyền `Authenticated` không thay thế việc kiểm tra thành viên, chủ sở hữu hoặc người được giao.
- **Runtime và tài liệu ngoài repo:** Runtime guide có ví dụ cũ về endpoint users và pipeline. Các tham chiếu Word/XLSX chưa tìm thấy trong repo và thư mục cha được kiểm tra; không coi nội dung các tệp đó là đã đọc hoặc xác nhận.

## Thứ tự hoàn thiện

1. **Nền tảng và Catalog:** Củng cố 8 API Identity đang có, thêm 2 API hồ sơ/quản trị; triển khai 11 API Catalog; kiểm tra schema, migration, validation, phân quyền và lỗi trùng dữ liệu. Đây là phạm vi đợt hiện tại.
2. **Danh tính và hồ sơ:** Hoàn thiện phần Identity còn lại, Profiles và hợp đồng sự kiện dùng chung đúng ranh giới. Thực hiện outbox/inbox có lưu bền trước khi phụ thuộc vào sự kiện để tạo dữ liệu liên module.
3. **Dịch vụ và lịch:** Chốt contract `mentor_services`; triển khai Scheduling với thời gian/múi giờ rõ ràng và kiểm tra chồng lịch.
4. **Booking và thanh toán:** Chốt state machine, giữ slot 10 phút, price snapshot, xử lý callback trùng/đến muộn, ledger và escrow trong transaction. Gateway cần cấu hình và xác minh theo tài liệu chính thức.
5. **Buổi học và đánh giá:** Learning, bằng chứng hoàn tất, Reviews, khiếu nại và giải ngân theo quy tắc đã thống nhất. Messaging có thể triển khai độc lập khi danh tính/quyền thành viên đã ổn định.
6. **Gợi ý:** Chỉ tính điểm và xây read model khi đã có các nguồn dữ liệu phía trên.

## Chưa triển khai trong đợt đầu

- Google OAuth, gửi email và quên/đặt lại mật khẩu.
- Dispatcher outbox bền vững, RabbitMQ và inbox/deduplication cho consumer.
- `mentor_services` và tám module còn đang là stub.
- Kết nối gateway thật, thu/chi tiền, upload lưu trữ và các tác vụ lịch nền.

## Bằng chứng kiểm tra

Đã chạy trên Windows, .NET SDK 10.0.201 và PostgreSQL 18.6 cục bộ; kiểm thử PostgreSQL và Bootstrapper được chạy lại ngày 24/09/2026 sau khi giữ các cơ chế bảo mật của prototype:

- `dotnet build SkillBridge.slnx --configuration Release --no-restore --warnaserror`: đạt, 0 warning, 0 error.
- `dotnet format SkillBridge.slnx --verify-no-changes --no-restore`: đạt.
- `dotnet run --project tests/SkillBridge.Checks --configuration Release --no-build -- --database`: đạt. Bao gồm validation, JSON, ranh giới module/schema/FK, migration nâng cấp hai tài khoản cũ, 10 API Identity và 11 API Catalog; quyền Admin, cookie bảo mật, refresh đồng thời, thu hồi phiên, đăng nhập sai đồng thời và lockout, outbox, cây danh mục, tags, tìm kiếm, soft delete và chống chu trình khi ghi đồng thời.
- Khởi chạy trực tiếp `SkillBridge.Api.dll` trên database trống riêng: đạt tự migrate, `/health/live`, `/health/ready`, OpenAPI và rate limit `429` sau 10 request/phút, kể cả đổi hoa/thường hoặc dấu `/` cuối URL.
- Database và tiến trình kiểm thử được dọn sau khi chạy; không áp dụng migration vào database đang có của người dùng.

GitHub Actions với PostgreSQL 17 đã đạt trên commit `2c4c7f3`: [API CI run 11](https://github.com/SkillBrigde/skillbridge-api/actions/runs/35947077382), gồm build, format, logic/architecture và PostgreSQL/HTTP checks. Các kiểm tra trên không xác nhận tính năng của tám module còn là stub hay tích hợp gateway/email/RabbitMQ.

Migration `CompleteIdentitySecurity` giữ các user từ migration ban đầu, chuẩn hóa email và khóa tài khoản chưa có mật khẩu. Email trùng sau chuẩn hóa làm migration thất bại để đối chiếu thủ công, không tự gộp/xóa người dùng. Database từng chạy blueprint SQL cần phương án chuyển đổi riêng; không áp dụng chồng migration.

## Thay thế prototype trên `feat/dang`

Mã hiện tại dựa trên `main`, thay thế prototype Identity trước đó trên `feat/dang`. Giữ chống Admin tự khóa tài khoản, thu hồi toàn bộ phiên khi refresh token bị dùng lại, đăng xuất thu hồi JWT ngay và thời hạn refresh cố định từ lần đăng nhập. Client phải tuần tự hóa refresh; hai request dùng cùng token sẽ thu hồi toàn bộ phiên của tài khoản.

Ba API `/api/v1/sessions`, `SuperAdmin`/đa vai trò, cập nhật avatar, lọc user theo role/status và bảng security audit của prototype chưa được chuyển sang contract này. Lịch sử Git giữ lại prototype để đối chiếu; đây không phải bản nâng cấp tương thích hoàn toàn.

**Database đã chạy `20260916144706_IdentityAuthentication` của prototype không tương thích với migration hiện tại**: khác security stamp (UUID/string), mật khẩu (PBKDF2/BCrypt), vai trò và cấu trúc session/token. Chỉ chạy trên database mới hoặc database có migration `20260911052416_Initial_Identity` từ `main`. Dữ liệu prototype cần kế hoạch chuyển đổi riêng trước triển khai; không xóa dữ liệu và không coi hash PBKDF2 là BCrypt.
