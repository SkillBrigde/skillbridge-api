# Payments API — mã nguồn hiện tại

Base `/api/v1/payments`. JWT bắt buộc, ngoại trừ webhook có chữ ký. Có 7 nghiệp vụ REST; webhook VNPAY có thêm GET alias nên số cặp method/path thực tế là 8.

## Chính sách tiền

- **Khóa rút tiền, giải ngân và hoàn tiền tự động**, theo quyết định chủ dự án ngày 20/10/2026. Chưa chốt phí hoặc thời hạn khiếu nại; không dùng các tỷ lệ/thời gian ví dụ cũ.
- Chỉ thanh toán Booking bằng VND nguyên đồng; không nạp ví hoặc đổi ngoại tệ. Amount lấy từ snapshot Booking; trường `amount` tùy chọn chỉ để đối chiếu, không dùng làm giá.
- Tiền đã xác minh được ghi cùng receipt, hai bút toán đối ứng và escrow trong một transaction Payments. Tổng bút toán mỗi giao dịch bằng 0. Đây là sổ theo dõi nội bộ, không khẳng định ngân hàng cung cấp tài khoản escrow pháp lý riêng.
- Callback trùng không tạo thêm tiền. Callback sai tiền/tham chiếu hoặc không có đơn được lưu `gateway_receipts.rejection_code` để đối soát, không tự cộng ví hoặc xác nhận Booking.
- Tiền đến sau hủy/hết hạn giữ chỗ vẫn được ghi nhận; Booking không được hồi sinh. Escrow chuyển `ReviewRequired`, không tự hoàn tiền.
- `balance`/`availableBalance` của ví hiện bằng 0 vì chưa có nguồn tiền đã giải ngân. `heldBalance` là tổng tiền đang giữ từ gateway, không được chi tiêu.

## 7 API

| Method | URL | Quyền | Kết quả |
|---|---|---|---|
| GET | `/wallet` | Authenticated | Ví của chính user, số dư khả dụng, tiền đang giữ, `disbursementLocked: true` |
| GET | `/wallet/transactions?page=1&pageSize=10` | Authenticated | Các khoản `GatewayEscrowHold` của chính user; `amount` là tiền đưa vào escrow, `balanceBefore/After` là số dư khả dụng (không đổi) |
| POST | `/create-payment` | Mentee sở hữu Booking | Link PayOS/VNPAY có chữ ký; cần `Idempotency-Key` |
| POST | `/webhooks/{gateway}` | Chữ ký gateway | `payos` JSON hoặc `vnpay` query string; VNPAY hỗ trợ cả GET |
| GET | `/bookings/{bookingId}/escrow` | Hai người tham gia Booking | Escrow, `platformFeeAmount`/`mentorNetAmount`/`releasedAtUtc` bằng null khi chưa chốt chính sách |
| POST | `/withdraw` | Mentor | Validate thông tin rồi trả `409 Payments.DisbursementLocked`; không tạo yêu cầu/không trừ tiền |
| GET | `/payouts?page=1&pageSize=10` | Mentor | Danh sách rỗng khi chính sách đang khóa, chưa tồn tại yêu cầu rút tiền hợp lệ |

Phân trang: page >= 1, pageSize 1–100, tránh tràn số; response `items/page/pageSize/totalCount/totalPages`.

### Tạo thanh toán

```json
{
  "bookingId": "00000000-0000-0000-0000-000000000001",
  "gateway": "VnPay",
  "amount": 500000,
  "returnUrl": "https://web.example/checkout/result",
  "cancelUrl": "https://web.example/checkout/cancel"
}
```

`description` tùy chọn, tối đa 255 ký tự; mô tả chuyển khoản thực tế do server sinh từ orderCode. URL phải khớp **chính xác** allowlist, HTTPS, không user-info; không lấy URL tùy ý của client. Không dùng browser return/cancel để đổi trạng thái trả tiền.

Response 200: `paymentId`, `orderCode`, `gateway`, `amount`, `currency`, `paymentUrl`, `qrCode`, `accountNumber`, `accountName`, `bin`, `expiresAtUtc`. Các trường QR/tài khoản có thể null; hạn link không vượt hạn giữ chỗ. Một Booking chỉ có một đơn gateway; retry bằng key/nội dung ban đầu, không đổi gateway hoặc phát sinh đơn thứ hai.

Lỗi: 400 dữ liệu/URL/key, 401/403 auth, 404 không sở hữu Booking/escrow, 409 key đã dùng hoặc Booking đóng/sai tiền, 503 gateway chưa cấu hình/lỗi tạm thời. Timeout không tự đổi orderCode. PayOS retry tra cứu đơn đã tồn tại bằng cùng orderCode và xác minh response trước khi khôi phục link.

### Webhook

PayOS: xác minh HMAC-SHA256 trên `data` theo thứ tự khóa, so sánh constant-time, lấy trạng thái thành công từ `data.code` đã ký. Không tin `code`/`success` ngoài `data`. Từ chối JSON trùng khóa, payload quá 64 KiB, sai kiểu/sai currency/sai chữ ký. Thành công trả `{ "success": true }`.

VNPAY: HMAC-SHA512 trên các tham số `vnp_` đã URL encode theo thứ tự ordinal, bỏ SecureHash/SecureHashType. Kiểm tra TmnCode, TxnRef, amount chia 100, ResponseCode và TransactionStatus cùng 00. Từ chối query trùng giá trị; thành công trả **đúng tên hoa/thường** `{ "RspCode": "00", "Message": "Confirm Success" }`; chữ ký sai trả RspCode 97, không có đơn 01, sai tiền/tham chiếu 04.

Webhook đã lưu thành công có thể acknowledge dù Booking tạm lỗi: worker 5 giây tiếp tục các Payment `Verified`. Nếu đã cập nhật Booking nhưng tiến trình ngắt trước khi đánh dấu Payments, retry cùng paymentId không đặt lịch/cộng tiền lần hai. Lỗi nghiệp vụ Booking đóng chuyển `ReviewRequired`. Callback thất bại không ghi tiền. Không có API công khai nhận `paid=true`.

## Infra và kiểm chứng

Migration riêng `payments`, lịch sử `payments.__EFMigrationsHistory`. Các bảng: `payment_orders`, `gateway_receipts`, `escrows`, `ledger_entries`, `wallets`; không FK sang module khác. Development tự migrate theo Host; môi trường khác áp migration trước worker.

Gateway **tắt mặc định**. Cấu hình secret qua env/secret store, không commit khóa:

```text
Payments__VnPay__Enabled=true
Payments__VnPay__TmnCode=<merchant>
Payments__VnPay__HashSecret=<secret>
Payments__VnPay__PaymentUrl=https://sandbox.vnpayment.vn/paymentv2/vpcpay.html
Payments__PayOS__Enabled=true
Payments__PayOS__ClientId=<client>
Payments__PayOS__ApiKey=<secret>
Payments__PayOS__ChecksumKey=<secret>
Payments__AllowedReturnUrls__0=https://web.example/checkout/result
Payments__AllowedReturnUrls__1=https://web.example/checkout/cancel
```

VNPAY production URL được phép: `https://pay.vnpay.vn/vpcpay.html`. PayOS gọi cố định `https://api-merchant.payos.vn`, không cho redirect mang theo credential. PayOS dùng tiền thật; chỉ bật khi chủ dự án sẵn sàng kiểm chứng và đối soát. Cần cấu hình webhook HTTPS tại merchant và kiểm tra kết nối ngoài Internet; source code/test nội bộ không chứng minh giao dịch tiền thật hoặc đối soát sao kê đã hoàn tất.

Đối chiếu giao thức: [PayOS API](https://payos.vn/docs/api/), [SDK crypto chính thức](https://github.com/payOSHQ/payos-lib-dotnet/blob/main/src/Crypto/CryptoProvider.cs), [VNPAY 2.1.0](https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html).
