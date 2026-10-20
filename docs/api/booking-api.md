# Booking API — gói nhiều buổi

Base path: `/api/v1/booking`, JWT bắt buộc. Đã có 9 route nghiệp vụ và migration schema `booking`. Trên nhánh Learning kế tiếp đã nối bằng chứng hoàn tất và relay Booking → Learning. **Payments/gateway thật còn thiếu; chưa dùng để thu tiền production.**

## Các route

| Method | Đường dẫn sau base path | Quyền | Kết quả |
|---|---|---|---|
| POST | `/bookings` | Mentee | 201 + chi tiết, Location |
| GET | `/bookings/{bookingId}` | Hai bên hoặc Admin | 200 chi tiết |
| GET | `/bookings` | Người dùng liên quan; Admin xem tất cả | 200 phân trang |
| POST | `/bookings/{bookingId}/confirm` | Đúng Mentor | 204 |
| POST | `/bookings/{bookingId}/cancel` | Hai bên | 204 |
| POST | `/bookings/{bookingId}/complete` | Hai bên | 204 khi đủ bằng chứng Learning; thiếu buổi hoàn tất trả 409 |
| POST | `/bookings/{bookingId}/reschedule` | Hai bên | 201 đối tượng yêu cầu, Location trỏ booking |
| POST | `/reschedule-requests/{requestId}/respond` | Bên nhận yêu cầu | 204 |
| GET | `/bookings/{bookingId}/audit-logs` | Hai bên hoặc Admin | 200 phân trang |

Người ngoài nhận 404 cả chi tiết, audit và phản hồi dời lịch. Admin được đọc, không đóng vai bên tham gia để xác nhận/hủy/dời lịch. Các lệnh của hai bên yêu cầu role Mentor hoặc Mentee, tạo chỉ Mentee, xác nhận chỉ Mentor. `mentorId` là ID hồ sơ Profiles; quyền chủ sở hữu lấy từ ID Identity tin cậy, không phải ID client gửi.

## Tạo booking và chống trùng

Header `Idempotency-Key` bắt buộc: 1–100 ký tự ASCII in được, không có khoảng trắng. Body:

```json
{
  "serviceId": "019a0000-0000-7000-8000-000000000001",
  "serviceVersion": 1,
  "slotIds": [
    "019a0000-0000-7000-8000-000000000002",
    "019a0000-0000-7000-8000-000000000003"
  ],
  "intakeForm": {
    "coreQuestion": "Cần hỗ trợ kiến trúc backend",
    "attachedLinks": "https://example.com/cv, https://example.com/source",
    "targetGoals": "Hoàn thiện lộ trình hai buổi"
  }
}
```

UUID ví dụ cần thay bằng dữ liệu thật. Ba nội dung intake bắt buộc, tối đa 4000 ký tự mỗi trường; links gồm 1–10 URL HTTP/HTTPS phân cách bằng dấu phẩy hoặc xuống dòng. Server không tải các URL đó. JSON request không nhận trường ngoài contract; không nhận giá, người sở hữu hoặc trạng thái thanh toán từ client.

Catalog cung cấp snapshot tổng giá gói, tiền tệ, phiên bản, số buổi và thời lượng mỗi buổi. Phiên bản dịch vụ không khớp trả 409. Phải chọn đủ 1–52 slot không trùng, cùng Mentor, đúng thời lượng và đáp ứng lead time, còn khả dụng. Không tự đặt dịch vụ của mình. Mentor phải có tài khoản hoạt động, đúng role và hồ sơ nhận Mentee, không bị từ chối xác minh.

Một user + key + nội dung chuẩn hóa trả cùng booking, kể cả tạo đồng thời; nội dung khác cùng key trả 409. Thứ tự slot và khoảng trắng đầu/cuối intake được chuẩn hóa. Ý định giữ chỗ lưu bền trước khi gọi Scheduling, cả gói cùng thành công hoặc cùng thất bại. Key thất bại giữ chỗ không được âm thầm dùng lại để tạo booking khác; chọn lịch mới dùng key mới.

## Dữ liệu trả và phân trang

Chi tiết gồm `id`, `bookingCode`, `menteeUserId`, `mentorId`, `status`, `serviceSnapshot`, `intakeForm`, `totalAmount`, `currency`, `heldUntilUtc`, `mentorNote`, `cancellationReason`, `cancelledBy`, `createdAtUtc`, `updatedAtUtc`, `sessions`, `rescheduleRequests`.

- `serviceSnapshot`: điều khoản và phiên bản lúc đặt; giá dịch vụ đổi không sửa booking cũ.
- `sessions`: `id`, `sessionNumber`, `slotId`, `startAtUtc`, `endAtUtc`; giờ UTC, có thể null khi Holding. ID buổi giữ nguyên khi dời lịch.
- `rescheduleRequests`: `id`, `bookingSessionId`, `oldSlotId`, `newSlotId`, `requestedByUserId`, `reason`, `status`, `responseNote`, `createdAtUtc`, để bên nhận biết request ID và phản hồi.
- Mã booking là `SB-{year}-{UUIDv7 không dấu gạch}`, tránh giới hạn không gian mã 5 chữ số.

Danh sách: `status` tùy chọn theo state machine, `page=1`, `pageSize=10`, sắp `createdAtUtc`, `id` giảm dần. Audit: `page=1`, `pageSize=50`, sắp thời gian/ID tăng dần. Cả hai giới hạn pageSize 1–100, page tối thiểu 1, offset không vượt Int32.MaxValue. Body PagedResult: `items`, `page`, `pageSize`, `totalCount`, `totalPages`, `hasPreviousPage`, `hasNextPage`.

Mỗi audit: `id`, `oldStatus`, `newStatus`, `changedByUserId`, `reason`, `createdAtUtc`. Dời lịch giữ nguyên trạng thái booking vẫn ghi audit; actor hệ thống là null.

## Trạng thái, thanh toán và hủy

Luồng: `Holding → PendingPayment → AwaitingMentor → Confirmed → Completed`. Hủy chưa thanh toán → `Cancelled`; hủy đã thanh toán → `RefundReview`, **chưa có nghĩa đã hoàn tiền**.

- Holding lưu ý định, PendingPayment chỉ xuất hiện sau khi giữ đủ slot. Hạn giữ tối đa 10 phút, không vượt thời điểm bắt đầu slot. PostgreSQL transaction/advisory lock là nguồn sự thật cho giữ chỗ; không dùng Redis làm nguồn sự thật. Redis vẫn dùng cho bảo mật Identity. Không có FK chéo schema.
- Confirm nhận `{ "mentorNote": "Chuẩn bị tài liệu" }`. Ghi chú tùy chọn, tối đa 2000 ký tự, không chứa ký tự điều khiển. Chỉ đúng Mentor xác nhận AwaitingMentor khi đã xác minh thanh toán và các buổi chưa bắt đầu. PendingPayment không đủ điều kiện; retry 204 không phát sự kiện trùng.
- Cổng nội bộ `ReceivePaymentAsync` chỉ cho adapter Payments tin cậy, khớp payment ID/tổng tiền/tiền tệ, booking chưa quá hạn và còn đủ slot. Retry không được đổi số tiền; một payment không dùng cho hai booking. Không có REST endpoint để client khai đã thanh toán.
- Cancel nhận `{ "reason": "Không thể tham gia" }`: lý do bắt buộc, tối đa 2000 ký tự. Chỉ hủy gói chưa bắt đầu; Mentee hủy gói đã trả tiền phải trước buổi đầu ít nhất 24 giờ theo contract hiện tại. Không tự hoàn phần gói. Khi Applying dời lịch trả 409 để thử lại sau.
- Hủy 204 nghĩa trạng thái hủy và yêu cầu nhả slot đã lưu bền. Nhả thử ngay, worker tiếp tục nếu phụ thuộc lỗi. Hủy trả tiền dừng RefundReview, không tự hoàn/chi tiền. Retry không sinh audit/sự kiện trùng.
- Complete không nhận bằng chứng từ client; cần Confirmed, đủ ID buổi hoàn thành do Learning xác minh và đã qua giờ kết thúc tất cả buổi. Adapter Learning trên nhánh này kiểm tra đủ bookingSessionId đã Completed; thiếu bằng chứng trả 409. Khi không cấu hình producer trả 503. Không tự đánh dấu hoàn tất và không kích hoạt giải ngân.

## Dời từng buổi

Tạo yêu cầu:

```json
{
  "bookingSessionId": "019a0000-0000-7000-8000-000000000004",
  "newSlotId": "019a0000-0000-7000-8000-000000000005",
  "reason": "Cần đổi giờ"
}
```

Gói phải Confirmed, buổi chưa bắt đầu, slot mới khác các slot của gói, lý do bắt buộc tối đa 2000 ký tự. Tối đa 100 yêu cầu/booking, một Pending/Applying cho mỗi buổi. Chưa giữ slot mới khi đề xuất; lịch cũ vẫn giữ.

Bên nhận phản hồi `{ "isApproved": true, "responseNote": "Đồng ý" }`. isApproved bắt buộc; ghi chú tối đa 2000 ký tự, không có ký tự điều khiển. Bên gửi tự duyệt trả 403; không đảo quyết định đã xử lý bằng retry.

Từ chối → Rejected, lịch cũ giữ nguyên. Đồng ý → lưu Applying trước khi đổi nguyên tử slot đã đặt sang slot mới tại Scheduling. Thành công → Approved, giữ ID buổi và state booking Confirmed; slot mới bị chiếm/không phù hợp → 409, request Rejected, giữ lịch cũ. Nếu bị ngắt sau khi Scheduling commit swap, lần tiếp tục hoàn thiện booking từ Applying, không đổi lần hai.

## Phục hồi và giới hạn

Worker mỗi 5 giây, theo lô 100, khôi phục Holding, hủy booking quá hạn/nhả slot và tiếp tục Applying. Khóa PostgreSQL bảo vệ các tiến trình cùng dùng database; timer không phải SLA. Một lỗi xử lý riêng không dừng phần còn lại của lô.

Outbox lưu cùng transaction cho Confirmed, Cancelled, RefundReview, Completed và Rescheduled. Đã có relay nội bộ sang Learning với inbox chống trùng và retry; chưa có consumer Payments để xử lý tài chính. Không đồng nghĩa đã hoàn tiền. Callback đến muộn/mất slot phải đưa sang đối soát tại Payments, không tự cấp slot mới hay chi tiền. Chưa kết nối gateway thật; kiểm thử gọi cổng tin cậy, không giả lập thanh toán thành công trong runtime.

**Phí, hoàn tiền và hạn khiếu nại chưa chốt; giải ngân tự động bị khóa theo quyết định chủ dự án.**

Lỗi ProblemDetails: 400 đầu vào, 401 thiếu xác thực, 403 sai role/tự duyệt, 404 không tồn tại/khác chủ, 409 xung đột nghiệp vụ, 503 phụ thuộc chưa khả dụng.
