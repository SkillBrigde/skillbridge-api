# Bàn giao 19 API cuối — 20/10/2026

Mã nguồn: Payments 7, Reviews 7, Recommendations 5; tổng backend 106 nghiệp vụ REST + 1 Hub, hoặc 107 method/path REST nếu tính GET alias của VNPAY. Chưa tự merge/deploy, chưa kiểm chứng giao dịch merchant tiền thật.

## Web/BFF

- Dùng [Payments contract](../api/payments-api.md), [Reviews contract](../api/reviews-api.md), [Recommendations contract](../api/recommendations-api.md); DTO trực tiếp, lỗi Problem Details.
- Tạo Payment: gửi JWT Mentee và Idempotency-Key, bookingId/gateway/returnUrl/cancelUrl; giá từ Booking. Giữ nguyên key/nội dung khi retry; không tạo key mới để đổi cổng cho cùng booking.
- returnUrl/cancelUrl phải khớp allowlist HTTPS của backend. Trang kết quả không tự xác nhận paid; đọc trạng thái Booking/escrow. Hiển thị ReviewRequired là chờ đối soát, không phải đã hoàn tiền.
- Withdraw hợp lệ trả 409 Payments.DisbursementLocked; không hiển thị ví tiền giữ như tiền có thể rút. Fee/net null nghĩa chính sách chưa chốt.
- Review chỉ xuất hiện sau khi Learning hoàn tất mọi buổi và Booking Completed. Dùng sessionId của Learning từ my-pending, không dùng bookingSessionId. Một review cả gói; PUT chỉ sửa trước +48h, ba tiêu chí tùy chọn được giữ nguyên.
- Public review không có booking/session/user ID người học; render comment/reply như plain text.
- Metrics có thể null/chưa có snapshot. Score rules-v1 không phải xác suất. Interactions chỉ Click/ViewProfile, gắn skill hiện hoạt động của Mentor; không gửi userId.

## Infra

- Booking #28, Learning #29, Payments #30 và Reviews #31 đã vào develop tại d7c1d18, cd0f4e0, 25b16d2 và 81683fe. Recommendations #32 là PR cuối, đã đồng bộ develop sau lần merge Reviews và hiện lấy develop làm base. Kiểm tra base/CI trước khi merge, đồng bộ nhánh tiếp theo nếu squash hoặc đổi base. Các module không reference nhau; adapter nằm ở src/Api/Common.
- Migrations 3 schema mới và SQL idempotent tại scripts/migrations/{payments,reviews,recommendations}; hướng dẫn [database-migrations.md](../database-migrations.md). Database từng dựng từ blueprint phải đối chiếu trước.
- Production không tự migrate. Cần migrations cả Booking/Learning/Payments/Reviews/Recommendations trước khi chạy worker. Không sửa history để bỏ qua migration.
- Gateway tắt mặc định; cấp secret qua env/secret store theo .env.example, cấu hình exact HTTPS allowlist và webhook merchant. PayOS gọi tiền thật, VNPAY mặc định Sandbox. Không commit secret và không bật thu tiền khi chưa có đối soát vận hành.
- Workers: Booking reconciliation 5s, Booking lifecycle relay 1s, Payments apply 5s, metrics lúc khởi động và mỗi phút. Nhiều instance dùng advisory lock; metrics giữ snapshot cũ nếu refresh lỗi.
- Theo dõi Verified chưa apply, escrow ReviewRequired, gateway_receipts có rejection_code, lỗi relay/worker và tuổi lastCalculatedAtUtc. Chốt quy trình đối soát khoản muộn/sai tiền, retention và cảnh báo trước production.
- Rút tiền/giải ngân/hoàn tiền tự động đang khóa trong mã nguồn, không có env switch mở khóa. Phải chốt phí, hoàn tiền, hạn khiếu nại rồi bổ sung luồng đã kiểm thử riêng.

## Kiểm chứng đã chạy

Release build --warnaserror và format verify đạt; PostgreSQL 18/Redis cô lập, HTTP/domain/security checks cả 10 module đạt; 120 xUnit tests đạt, không skipped. Host thật migrate database trống, health/OpenAPI 107 method/path và lượt worker đầu đạt.

PayOS HTTP giả lập, VNPAY callback fixture có chữ ký; chưa chứng minh thanh toán/hoàn tiền thật, sao kê, production deployment, web đầu cuối, Google Meet telemetry, RabbitMQ hoặc tải lớn. Batch Recommendations hiện đọc dữ liệu/tập ứng viên trong bộ nhớ; cần phân lô khi tăng dữ liệu. Files bytea và SignalR một instance giữ giới hạn đã ghi trong tiến độ backend.
