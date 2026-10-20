# Learning API — buổi học theo gói

Base path `/api/v1/learning/sessions`, JWT bắt buộc. Đã triển khai 16 route theo contract và **1 route tải file có kiểm tra quyền**, tổng 17 REST. Dữ liệu buổi học được sinh từ Booking đã xác nhận, không có endpoint để client tự tạo session hoặc khai bằng chứng hoàn tất.

## Tích hợp Booking

- Mỗi booking session có đúng một learning session, khóa unique `bookingSessionId`. `id` Learning và `bookingSessionId` là hai định danh khác nhau; giữ nguyên cả hai khi dời lịch.
- Host chuyển sự kiện Booking bằng outbox lưu bền, consumer có inbox chống trùng. Dispatcher thăm dò mỗi giây, theo lô tối đa 100, dùng SKIP LOCKED; lỗi trì hoãn 30 giây và không chặn các bản ghi khác. Đây là relay nội bộ monolith, **chưa phải RabbitMQ**.
- Consumer đọc trạng thái Booking mới nhất dưới khóa chung `booking:<id>` trước khi đồng bộ; sự kiện cũ không ghi đè lịch mới hoặc tạo lại session. Booking và Learning dùng cùng database PostgreSQL, cùng namespace khóa để tuần tự hóa đổi lịch, điểm danh và kiểm tra hoàn tất; không có FK hoặc project reference chéo module.
- Hủy booking khóa dữ liệu học mới, đồng bộ session chưa hoàn tất thành Cancelled và đóng điểm danh. Thay đổi lịch chỉ áp dụng cho buổi Scheduled; không ghi đè lịch sử đã bắt đầu/kết thúc.
- Booking complete lấy ID các buổi Completed từ Learning, kiểm tra đủ cả gói và thời điểm kết thúc. Không kích hoạt giải ngân tự động.

## Các route

| Method | Đường dẫn sau base path | Quyền và kết quả |
|---|---|---|
| GET | `/` | Hai bên xem buổi liên quan, Admin xem tất cả; 200 phân trang |
| GET | `/{sessionId}` | Hai bên hoặc Admin; 200 chi tiết |
| POST | `/{sessionId}/start` | Đúng Mentor, role Mentor; 204 |
| POST | `/{sessionId}/end` | Đúng Mentor, role Mentor; 204 |
| POST | `/{sessionId}/join` | Hai bên, role Mentor/Mentee; 204 |
| POST | `/{sessionId}/leave` | Hai bên, role Mentor/Mentee; 204 |
| GET | `/{sessionId}/notes` | Hai bên hoặc Admin; 200 phân trang, ghi chú riêng chỉ tác giả |
| POST | `/{sessionId}/notes` | Hai bên; 201 NoteResponse |
| PUT | `/{sessionId}/notes/{noteId}` | Tác giả; 204 |
| DELETE | `/{sessionId}/notes/{noteId}` | Tác giả; 204 |
| GET | `/{sessionId}/materials` | Hai bên hoặc Admin; 200 metadata phân trang |
| POST | `/{sessionId}/materials` | Hai bên; upload raw binary, 201 MaterialResponse |
| DELETE | `/{sessionId}/materials/{materialId}` | Người upload; 204 |
| GET | `/{sessionId}/materials/{materialId}/download` | Hai bên hoặc Admin; 200 binary attachment |
| GET | `/{sessionId}/action-items` | Hai bên hoặc Admin; 200 phân trang |
| POST | `/{sessionId}/action-items` | Đúng Mentor; 201 ActionItemResponse |
| PATCH | `/{sessionId}/action-items/{itemId}` | Đúng Mentee được giao, role Mentee; 204 |

Người ngoài nhận 404, kể cả truy cập ID ghi chú/file/bài tập đúng nhưng sai session. Admin được đọc dữ liệu chung, không được xem ghi chú riêng của người khác hay đóng vai thành viên để ghi dữ liệu. Sau Completed vẫn cho hai bên bổ sung ghi chú/tài liệu/bài tập; Cancelled không nhận dữ liệu mới.

## Dữ liệu và phân trang

Danh sách session có `bookingId` và `status` tùy chọn, `page=1`, `pageSize=10`; sắp `scheduledStartUtc`, `id`. Các danh sách con mặc định pageSize 50, sắp thời gian tạo/ID. Page tối thiểu 1, pageSize 1–100, offset không vượt Int32.MaxValue. Body PagedResult chuẩn `items`, `page`, `pageSize`, `totalCount`, `totalPages`, `hasPreviousPage`, `hasNextPage`.

SessionResponse: `id`, `bookingId`, `bookingSessionId`, `sessionNumber`, `mentorId` (ID hồ sơ), `menteeId` (ID Identity), `status`, `roomUrl`, `scheduledStartUtc`, `scheduledEndUtc`, `actualStartUtc`, `actualEndUtc`. Giờ đều UTC. State hiện có Scheduled/InProgress/Completed/Cancelled; chưa tự suy ra NoShow hoặc chính sách tài chính từ việc vắng mặt.

## Bắt đầu, tham gia và kết thúc

Start body:

```json
{ "roomUrl": "https://meet.google.com/abc-defg-hij" }
```

Mentor cung cấp link Meet thật đã tạo ngoài hệ thống. API chỉ kiểm tra HTTPS, đúng host meet.google.com, path một mã phòng, không query/fragment/user-info/cổng lạ; **không tạo phòng bằng Google API và không xác minh cuộc gọi tồn tại**. Chỉ bắt đầu từ giờ đã đặt đến trước giờ kết thúc, không mở sớm. Khi InProgress, Mentor có thể gọi lại start với link hợp lệ để sửa link; giữ actualStartUtc và không phát lại Started. Mentee lấy chi tiết mới nhất để mở link phòng.

Start không tự điểm danh. Hai bên gọi join bằng token của chính mình; active attendance duy nhất cho mỗi người/buổi, gọi lại không nhân bản. Leave đóng lượt đang mở, gọi lại không tạo dữ liệu mới. Tối đa 100 lượt/người/buổi; join chỉ khi InProgress và trước scheduledEndUtc + 30 phút.

End chỉ thành công khi InProgress, đã qua giờ kết thúc dự kiến và có khoảng điểm danh hai bên chồng nhau. Server lưu thời gian, không nhận timestamp từ client. End đóng các lượt đang mở và phát đúng một Completed; retry giữ nguyên thời gian/sự kiện. Đây là bằng chứng thao tác/điểm danh qua API và xác nhận kết thúc của Mentor, **không phải telemetry xác minh thời lượng Google Meet**.

## Ghi chú

```json
{ "content": "Nội dung ghi chú", "isPrivate": true }
```

Hai trường bắt buộc; content 1–10000 ký tự, hỗ trợ xuống dòng/tab, không nhận ký tự điều khiển khác. Chỉ tác giả sửa/xóa; isPrivate=true loại khỏi danh sách của cả người còn lại và Admin. NoteResponse gồm `id`, `authorId`, `content`, `isPrivate`, `createdAtUtc`, `updatedAtUtc`. Nội dung là text; web cần render text an toàn.

## Bài tập

POST body:

```json
{
  "assignedTo": "019a0000-0000-7000-8000-000000000001",
  "title": "Hoàn thiện phần backend",
  "description": "Ghi chú hướng dẫn",
  "dueDate": "2026-11-20T10:00:00Z"
}
```

Thay UUID/ngày ví dụ bằng dữ liệu thật. Chỉ giao cho Mentee của buổi; title bắt buộc tối đa 200, description tùy chọn tối đa 4000 ký tự. DueDate tùy chọn và không ở quá khứ lúc tạo. PATCH nhận `{ "isCompleted": true }` (bắt buộc), false được dùng để mở lại. Chỉ người được giao đánh dấu. Response gồm `id`, `assignedTo`, `title`, `description`, `dueDate`, `isCompleted`, `completedAtUtc`.

## Tài liệu và tải file

POST gửi **raw bytes, không dùng multipart**:

- Authorization Bearer như các API khác.
- `X-File-Name`: tên file encodeURIComponent/percent-encoded UTF-8, tối đa 150 ký tự sau decode, không đường dẫn/ký tự điều khiển.
- Content-Type đúng nội dung hoặc application/octet-stream. Không gửi file URL từ client.
- Tối đa 10 MiB, không nhận file rỗng. Chỉ PDF/PNG/JPEG theo đuôi và chữ ký đầu file, TXT UTF-8 hợp lệ; không nhận HTML/SVG/executable. Đây không phải bộ quét malware hoặc parser đầy đủ nội dung file.

Blob bytea và metadata lưu cùng transaction PostgreSQL; không có đường dẫn public hoặc token tải trong URL. MaterialResponse: `id`, `uploadedBy`, `fileName`, `contentType`, `fileSizeBytes`, `sha256`, `downloadUrl`, `createdAtUtc`; danh sách không chứa blob. Web tải bằng JWT hoặc BFF, không mở downloadUrl như tài nguyên public.

Download kiểm tra quyền rồi trả attachment, nosniff, private/no-store và CSP sandbox. Chỉ người upload được xóa. Lưu bytea phù hợp phạm vi hiện tại; cần chuyển object storage khi khối lượng tài liệu tăng.

## Lỗi và triển khai

400 đầu vào, 401 thiếu xác thực, 403 sai vai trò, 404 không tồn tại/khác chủ, 409 sai trạng thái/lịch/điểm danh, 413 file quá lớn, 503 nguồn Booking chưa khả dụng. Request JSON từ chối trường ngoài contract.

Infra cần áp dụng cả migration Learning và Booking AddOutboxRetryAfter trước khi khởi chạy worker production. Development tự migrate theo quy ước Host. Outbox Learning/Messaging và RabbitMQ chưa có dispatcher chung được xác minh. Gateway thanh toán thật, phí/hoàn tiền/hạn khiếu nại chưa chốt; giải ngân tự động tiếp tục bị khóa.
