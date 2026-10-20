# Tiến độ backend SkillBridge

Cập nhật 20/10/2026. Trạng thái dưới đây mô tả mã nguồn của nhánh này; chưa đồng nghĩa đã merge vào `develop` hoặc đã triển khai production. Endpoint `/_module` không được tính là API nghiệp vụ.

## Đã triển khai

| Module | REST | Phạm vi |
|---|---:|---|
| Identity | 12 | Xác thực, phiên, hồ sơ user, quản trị user, quên/đặt lại mật khẩu |
| Catalog | 17 | Categories/skills/tags và 6 API dịch vụ Mentor, version và snapshot gói nhiều buổi |
| Profiles | 14 | Hồ sơ Mentor/Mentee, kỹ năng, kinh nghiệm, chứng chỉ và xác minh |
| Scheduling | 10 | Settings, rules, sinh slot theo timezone, ngày nghỉ và giữ/xác nhận/nhả nhiều slot nội bộ |
| Messaging | 8 + 1 Hub | REST và SignalR, quyền thành viên, lưu tin nhắn, đọc/sửa/xóa và bảo vệ phiên |

Tổng 61 REST + 1 Hub theo contract hiện có (gồm 6 API mentor services bổ sung). Scheduling Slots/Time-offs thuộc đợt ND-10 này, cần merge PR trước khi sử dụng từ `develop`.

## Booking core trong nhánh kế tiếp

Nhánh `dang/booking-packages` bổ sung 9 route Booking: snapshot điều khoản/giá, đủ slot cho gói, idempotency, quyền sở hữu, audit, hủy/hết hạn và dời một buổi có bên còn lại chấp thuận. Ý định lưu bền để phục hồi nếu tiến trình dừng sau khi Scheduling đã commit.

**Chưa có Payments xác minh gateway thật, adapter bằng chứng Learning hoặc dispatcher outbox.** Booking đầu cuối chưa hoàn tất: API complete trả 503 khi cần nguồn Learning chưa có. Tổng 70 REST có route trên nhánh này, gồm 9 route Booking còn phụ thuộc; không phải 70 API production sẵn sàng. Scheduling ND-10 nằm trong PR #27; Booking dựa trên phần nền đó.

## Chưa hoàn thiện

| Module | REST theo kế hoạch | Phụ thuộc cần nối |
|---|---:|---|
| Booking | 9 (core đã có) | Nối Payments, Learning, dispatcher outbox và kiểm chứng đầu cuối |
| Payments | 7 | Gateway, webhook có xác minh, ledger/escrow, idempotency và đối soát |
| Learning | 16 | Tạo đủ buổi từ booking, quyền tham gia, điểm danh, bài tập và tài liệu |
| Reviews | 7 | Bằng chứng hoàn tất, một review/booking, sửa 48 giờ và thống kê |
| Recommendations | 5 | Dữ liệu thật từ hồ sơ, kỹ năng, booking và review |

Tại mốc ND-10 năm module trên còn stub; nhánh Booking kế tiếp đã bổ sung core như trên. Payments, Learning, Reviews và Recommendations vẫn chưa có API nghiệp vụ. Không coi tài liệu thiết kế hoặc code dở trong stash là tính năng đã chạy.

## Quyết định đã chốt với chủ dự án

- Hỗ trợ gói nhiều buổi ngay: một dịch vụ có 1–52 buổi, giá là tổng gói; Booking phải chọn đủ slot phù hợp và giữ nguyên tử cả gói.
- **Khóa giải ngân tự động.** Chủ dự án xác nhận ngày 20/10/2026 chưa chốt phí nền tảng, hoàn tiền và hạn khiếu nại. Không tự đặt mức phí, chia tiền hoặc lịch chi trả. Mọi triển khai Payments tiếp theo phải mặc định từ chối giải ngân tự động cho tới khi có quyết định rõ ràng và kiểm thử.
- Từ đợt này tiếp tục cả phần công việc trước đây phân công cho nguyendat; chia nhánh `dang/<feature-name>` và commit tiếng Việt theo từng phần liên quan.

## Giới hạn triển khai

- Email recovery hiện có mailbox dành cho Development; môi trường thật cần nhà cung cấp gửi email, thiếu cấu hình phải trả lỗi khả dụng.
- Outbox Messaging có lưu bền, chưa có dispatcher/inbox liên module đã chứng minh. Chưa xác nhận giao nhận RabbitMQ đầu cuối.
- Messaging hiện hỗ trợ nội dung văn bản và bảo vệ realtime trên một instance; cần kiểm tra scale nhiều instance trước production.
- Slots làm mới khi đọc/ghi lịch; chưa có worker nền. Cổng giữ chỗ đã kiểm thử riêng, chưa phải luồng Booking/Payments đầu cuối.
- Chưa có kiểm chứng gateway thật, upload production, triển khai infra hoặc kiểm thử web đầu cuối.

## Bằng chứng kiểm tra ND-10

Ngày 20/10/2026 trên Windows/.NET 10, database PostgreSQL 18 và Redis thử nghiệm cô lập:

- Release build với `--warnaserror`: 0 lỗi, 0 cảnh báo.
- SkillBridge.Checks: domain/DST, migration/schema, HTTP, security, REST/SignalR, password recovery và Scheduling đạt.
- Scheduling kiểm tra ngày nghỉ chồng nhau khi tạo đồng thời, quyền sở hữu, stable slot ID, buffer, rollback thay đổi ảnh hưởng slot được bảo vệ và hai gói tranh cùng nhiều slot chỉ một gói thắng; xác nhận đủ slot cùng transaction.
- xUnit: 3 architecture + 117 integration tests đạt, không bỏ qua test Redis.
- Host thật trên database trống riêng: migrations, health live/ready, OpenAPI và auth rate limit đạt.
- Đã dọn database/container/tiến trình thử nghiệm; không migrate database của người dùng.

Các kiểm tra này không chứng minh năm module còn stub hoặc các tích hợp production đã hoàn tất.

## Bằng chứng Booking core

Ngày 20/10/2026: Release build 0 warning/error; format đạt; PostgreSQL/HTTP checks đạt, cùng 3 architecture + 117 integration tests, có Redis thật.

- Cùng key tạo đồng thời trả cùng booking; hai booking tranh một gói chỉ một thắng. Kiểm tra quyền xem/audit/xác nhận, số buổi, version và khớp tiền khi retry thanh toán.
- Dời lịch không tự duyệt/khác chủ; swap đúng một slot, giữ ID buổi, retry không lặp sự kiện.
- Ngắt sau khi Scheduling commit hold hoặc swap: ý định còn bền vững, lần tiếp tục khôi phục đúng booking và lịch, không giữ/đổi lần hai.
- Hủy đã trả tiền dừng RefundReview; hết hạn nhả đủ slot. Host thật trên database mới kiểm tra migration, health/OpenAPI và worker không lỗi ở lượt thăm dò đầu.
- Chưa chứng minh gateway, consumer Learning hoặc thanh toán/giải ngân production.

## Ghi chú lịch sử

Phần dưới là kiểm kê **đợt đầu tháng 9/2026**, giữ để tra cứu các điểm bất nhất ban đầu; trạng thái hiện tại lấy từ các bảng phía trên.

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
