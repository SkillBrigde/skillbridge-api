# Tiến độ backend SkillBridge

Cập nhật 20/10/2026, mã nguồn tổng hợp tại `dang/recommendations`. Đã triển khai phần mã nguồn của 19 nghiệp vụ còn lại; chưa đồng nghĩa đã merge vào develop, triển khai production hoặc kiểm chứng thanh toán tiền thật. Endpoint `/_module` không tính API nghiệp vụ.

## Phạm vi hiện có

| Module | REST nghiệp vụ | Phạm vi chính |
|---|---:|---|
| Identity | 12 | Auth, phiên, user/Admin, password recovery; email production cần provider |
| Catalog | 17 | Taxonomy và 6 API dịch vụ Mentor; snapshot tổng giá gói 1–52 buổi |
| Profiles | 14 | Hồ sơ, kỹ năng, kinh nghiệm, chứng chỉ, xác minh |
| Scheduling | 10 | Settings/rules, timezone/DST, slots/time-offs, giữ/xác nhận/nhả/đổi nhiều slot |
| Booking | 9 | Gói nhiều buổi, quyền, idempotency, audit, dời lịch và phục hồi giữ chỗ |
| Payments | 7 | Wallet/escrow, hai gateway, webhook có chữ ký, ledger và phục hồi; withdraw bị khóa theo chính sách |
| Learning | 17 | 16 route theo contract + download có quyền; điểm danh, notes, tasks, materials |
| Messaging | 8 + 1 Hub | REST/SignalR, quyền thành viên, bảo vệ phiên |
| Reviews | 7 | Bằng chứng hoàn tất cả gói, một review/booking, sửa 48 giờ, reply, summary và pending |
| Recommendations | 5 | Gợi ý theo dữ liệu thật, metrics batch, tương tác chống trùng và trending |

Tổng **106 nghiệp vụ REST + 1 Hub**. OpenAPI host thật có **107 cặp HTTP method/path** do VNPAY IPN có GET alias ngoài POST webhook. Số 106 gồm 6 mentor services và 1 download bổ sung so với danh mục 99 ban đầu. Payments có route rút tiền nhưng chủ đích trả 409 cho tới khi chốt chính sách; không diễn giải con số này thành 106 luồng production hoàn tất.

## Quyết định giữ nguyên

- Hỗ trợ gói nhiều buổi ngay, lấy giá/tổng số buổi từ snapshot Catalog, giữ nguyên tử cả gói và không tin số tiền/trạng thái paid do client khai.
- **Khóa giải ngân, rút tiền và hoàn tiền tự động.** Phí nền tảng, hoàn tiền và hạn khiếu nại chưa chốt. Không có switch cấu hình tự mở khóa; fee/net trong escrow trả null, hủy đã trả tiền dừng ReviewRequired/RefundReview.
- Nhánh `dang/<feature-name>` tiếng Anh; commit tiếng Việt và tách theo từng phần liên quan.

## Nhánh và thứ tự phụ thuộc

- Scheduling #27, Booking #28, Learning #29, Payments #30 và Reviews #31 đã squash merge vào develop tại `cac16bd`, `d7c1d18`, `cd0f4e0`, `25b16d2` và `81683fe`.
- Recommendations #32 đã đồng bộ develop sau khi Reviews được merge, giữ đăng ký module, migration, metrics worker và toàn bộ kiểm thử gợi ý/phân quyền/chống trùng tương tác.
- Nhánh `dang/recommendations` và PR #32 hiện lấy develop làm base. Đây là PR cuối trong chuỗi tích hợp 19 API bổ sung.
- PR #32 còn cần review/merge; chưa tự merge vào develop/main hoặc deploy. Sau khi merge vẫn cần nghiệm thu các giới hạn môi trường/chính sách nêu dưới đây.

## Bằng chứng kiểm tra ngày 20/10/2026

Release build --warnaserror: 0 warning/error; format verify đạt. PostgreSQL 18, Redis thật trong môi trường thử nghiệm cô lập:

- SkillBridge.Checks chạy domain, migration/schema/FK, HTTP/security, REST/SignalR và các luồng cả 10 module.
- Payments: ký/verify theo giao thức; callback giả bị từ chối, callback đồng thời không nhân tiền; ledger cân bằng, callback muộn giữ tiền để đối soát, crash sau Booking commit vẫn phục hồi bằng cùng paymentId. PayOS HTTP được giả lập, chưa gọi merchant tiền thật.
- Reviews: đủ gói mới review, sai session/mentor không qua, một request thắng khi tạo đồng thời; sửa trước 48h, khóa đúng mốc; public không lộ ID người học/booking; summary phản ánh sửa điểm.
- Recommendations: metrics từ Learning/Reviews thật, đúng role/chủ sở hữu, khớp skill, score đổi theo sở thích; spam đồng thời không tăng trending; Mentor bị khóa bị loại ngay dù snapshot còn tồn tại.
- xUnit: **3 architecture + 117 integration tests**, đạt và không bỏ qua Redis. Các checks nghiệp vụ chạy bổ sung trong SkillBridge.Checks, không cộng nhầm vào số 120 xUnit.
- Host thật trên database trống: migrations cả 10 module, health live/ready, OpenAPI 107 method/path, rate limit và lượt worker đầu không lỗi.
- Không migrate database đang dùng của người dùng; database/container/process thử nghiệm đã được dọn. Stash code cũ vẫn giữ nguyên.

## Bàn giao và phần còn phụ thuộc môi trường/chính sách

Web xem [Payments](api/payments-api.md), [Reviews](api/reviews-api.md), [Recommendations](api/recommendations-api.md). BFF gửi JWT, Idempotency-Key khi tạo Booking/Payment; returnUrl gateway không phải bằng chứng đã trả tiền; render comment/reply dạng text; xử lý null metrics và 409 DisbursementLocked.

Infra xem [.env.example](../.env.example), [migration](database-migrations.md) và [bàn giao 19 API](handoffs/final-19-api.md). Gateway tắt mặc định. Cần merchant credentials, webhook HTTPS và kiểm chứng VNPAY Sandbox/PayOS thực tế. Chưa chứng minh giao dịch tiền thật, sao kê/hoàn tiền hoặc ngân hàng cung cấp escrow riêng.

Các giới hạn ngoài 19 API: relay nội bộ PostgreSQL chưa phải RabbitMQ; dispatcher outbox toàn hệ thống chưa hoàn tất. Link Meet do Mentor cung cấp, điểm danh API không phải telemetry cuộc gọi. Files bytea tối đa 10 MiB, chưa có object storage/malware scan. Email production, monitoring, multi-instance SignalR, chính sách retention, load test và web/infra đầu cuối còn cần nghiệm thu. Recommendations rules-v1 chạy batch/tập ứng viên trong bộ nhớ, chưa có kiểm chứng chất lượng ML.

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
