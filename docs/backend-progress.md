# Tiến độ backend SkillBridge

Cập nhật 20/10/2026, theo mã nguồn nhánh `dang/learning-sessions`. Có route/kiểm thử không đồng nghĩa đã merge vào develop hoặc triển khai production. Endpoint `/_module` chỉ là probe đăng ký module, không tính API nghiệp vụ.

## Đã triển khai trong mã nguồn

| Module | REST | Phạm vi và giới hạn |
|---|---:|---|
| Identity | 12 | Auth, phiên, user/Admin, quên/đặt lại mật khẩu; email thật cần provider, mailbox chỉ cho Development |
| Catalog | 17 | Taxonomy và 6 API dịch vụ Mentor, version/snapshot tổng giá gói 1–52 buổi |
| Profiles | 14 | Hồ sơ, kỹ năng, kinh nghiệm, chứng chỉ, xác minh |
| Scheduling | 10 | Settings/rules, timezone/DST, slots/time-offs, cổng giữ/xác nhận/nhả/đổi slot nhiều buổi |
| Booking | 9 | Gói nhiều buổi, idempotency, audit, dời lịch, hết hạn, đối soát ý định lưu bền; đã nối bằng chứng Learning, còn thiếu gateway Payments |
| Learning | 17 | 16 route theo contract + 1 download có quyền; điểm danh, ghi chú riêng, bài tập, tài liệu; Mentor cung cấp link Meet |
| Messaging | 8 + 1 Hub | REST/SignalR, quyền thành viên và phiên; text và một instance, chưa chứng minh scale nhiều instance |

Tổng **87 REST + 1 Hub có route**. Còn 19 REST theo kế hoạch: Payments 7, Reviews 7, Recommendations 5. Tổng kế hoạch hiện tại 106 REST + 1 Hub, gồm 6 mentor services và 1 download bổ sung so với danh mục 99 ban đầu. Chưa hoàn thiện toàn bộ backend production.

## Quyết định đã chốt

- Hỗ trợ gói nhiều buổi ngay: giá tổng gói và 1–52 buổi lấy từ Catalog; chọn đủ slot phù hợp, giữ nguyên tử cả gói, không tin giá/số buổi/thanh toán do client khai.
- **Giải ngân tự động bị khóa.** Chủ dự án xác nhận chưa chốt phí nền tảng, hoàn tiền và hạn khiếu nại. Không tự đặt tỷ lệ phí, lịch chi trả hay tự hoàn tiền. Hủy booking đã trả tiền dừng ở RefundReview, không có nghĩa đã hoàn tiền.
- Từ đợt này tiếp tục cả phần trước đây giao nguyendat; nhánh `dang/<feature-name>` tiếng Anh, commit tiếng Việt và tách từng phần liên quan.

## Các PR và phụ thuộc

- #27 Scheduling: đã squash merge vào develop tại `cac16bd`; nhánh này đã đồng bộ bản sửa conflict ND-11 `8c5242a`.
- #28 Booking core: 8 commit mới, PR Draft vào develop, CI GitHub đã đạt. Nhánh còn chứa 6 commit nền của #27; tổng 14 commit so với develop ở thời điểm mở PR, không phải 14 thay đổi Booking riêng.
- Learning nằm trên nhánh kế tiếp của Booking, bổ sung Producer snapshot chỉ đọc, migration Learning và Booking AddOutboxRetryAfter. Cần đồng bộ/merge các phần nền theo thứ tự; chưa tự merge vào develop.

## Tích hợp đã kiểm chứng

- Scheduling bảo vệ slot Held/Booked; thay settings/rules/ngày nghỉ ảnh hưởng lịch đó trả 409 và rollback. Hai booking tranh cùng nhiều slot chỉ một thắng.
- Booking lưu ý định trước khi gọi Scheduling. Ngắt sau hold/swap đã commit vẫn tiếp tục được, không giữ/đổi lần hai. Giữ ID buổi khi dời lịch.
- Relay Host đọc outbox Booking, consumer Learning có inbox chống trùng, đọc Booking mới nhất dưới khóa chung trước khi đồng bộ. Sự kiện cũ không lùi lịch đã duyệt. Retry sau inbox commit vẫn acknowledge được, không nhân bản buổi học.
- Luồng kiểm thử dùng cổng thanh toán tin cậy nội bộ → Confirmed Booking → đủ buổi → điểm danh hai bên → Mentor kết thúc sau giờ đặt → complete cả gói đã đạt. Thiếu một buổi không cho complete. **Chưa phải giao dịch gateway hoặc bằng chứng cuộc gọi Meet thật.**
- Ghi chú riêng chỉ tác giả, kể cả Admin không xem nội dung đó. Bài tập chỉ giao đúng Mentee, tài liệu chỉ uploader được xóa, download yêu cầu quyền và không có URL public.

## Giới hạn và công việc còn lại

1. Payments: gateway có xác minh webhook, ledger/escrow, idempotency và đối soát callback trùng/muộn; mọi giải ngân tự động tiếp tục khóa đến khi chính sách được chốt.
2. Reviews: bằng chứng hoàn thành cả gói, một review/booking, sửa 48 giờ và thống kê.
3. Recommendations: dữ liệu thật từ hồ sơ, kỹ năng, booking và reviews.
4. Relay hiện là gọi nội bộ cùng database PostgreSQL, **chưa phải RabbitMQ**; outbox Learning/Messaging và dispatcher toàn hệ thống chưa được chứng minh. Chưa có consumer Payments xử lý tài chính từ lifecycle events.
5. Room URL do Mentor cung cấp, chưa có Google API tạo phòng/telemetry. Điểm danh là thao tác API và xác nhận Mentor, không chứng minh thời lượng cuộc gọi video.
6. Tài liệu tối đa 10 MiB/file lưu bytea cùng metadata trong transaction; cần object storage khi tăng tải. Chỉ kiểm tra đuôi/chữ ký file hoặc TXT UTF-8, không có bộ quét malware đầy đủ.
7. Slots làm mới khi đọc/ghi lịch, chưa có worker sinh trước độc lập. Email production, triển khai infra và web đầu cuối chưa được kiểm chứng.

Infra phải migrate cả Learning và Booking AddOutboxRetryAfter trước khi chạy worker production; Development tự migrate theo quy ước Host. Không áp dụng migration vào database người dùng trong đợt kiểm tra này.

## Bằng chứng kiểm tra

Windows/.NET 10, PostgreSQL 18 và Redis thử nghiệm cô lập:

- Release build `--warnaserror`: 0 warning/error; format đạt.
- SkillBridge.Checks đạt: domain/DST, schema/migration, HTTP/security, REST/WebSocket, recovery, Scheduling, Booking và Learning.
- xUnit: 3 architecture + 117 integration tests đạt, không bỏ qua Redis.
- File tests kiểm tra tên đường dẫn, MIME, size, quyền tải/xóa, attachment/no-store và byte roundtrip; notes kiểm tra quyền riêng tư, bài tập kiểm tra đúng người được giao.
- Native Host trên database mới kiểm tra migration, health live/ready, OpenAPI và auth rate limit. Phiên bản Learning được kiểm tra lại trước khi mở PR.
- Database, Redis container và tiến trình thử nghiệm được dọn; không sửa database đang dùng. Bản code dở cũ trong stash vẫn được giữ, không tự áp lại vào cấu trúc đã refactor.

Các kiểm tra này không chứng minh production, gateway, cuộc gọi Meet, RabbitMQ hoặc những module còn thiếu đã hoàn tất.

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
