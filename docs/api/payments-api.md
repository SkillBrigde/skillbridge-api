# Tài liệu API Contract - Payments Module (SkillBridge)

## Giới thiệu
Tài liệu định nghĩa API Contract cho **Payments Module** của hệ thống SkillBridge. Trách nhiệm của module là xử lý thanh toán, ví điện tử (wallet), giao dịch ký quỹ (Escrow), hoàn tiền (Refund), và quyết toán cho Mentor.

---

## Các API Endpoints

### 1. Xem ví cá nhân
- **Method:** `GET`
- **URL:** `/api/v1/payments/wallet`
- **Mô tả:** Lấy thông tin ví hiện tại của người dùng.
- **Role:** `Authenticated`

#### Response (200 OK)
```json
{
  "id": "5a4b3c2d-1e0f-9a8b-7c6d-5e4f3a2b1c0d",
  "userId": "d0e1b2f3-c4a5-48b6-9c7d-e8f90a1b2c3d",
  "balance": 1500000.0,
  "currency": "VND",
  "createdAtUtc": "2026-08-10T10:00:00Z",
  "updatedAtUtc": "2026-09-14T02:00:00Z"
}
```

---

### 2. Lịch sử giao dịch ví
- **Method:** `GET`
- **URL:** `/api/v1/payments/wallet/transactions`
- **Mô tả:** Lấy danh sách biến động số dư (phân trang).
- **Role:** `Authenticated`

#### Query Parameters
- `page` (int): Trang hiện tại (default 1).
- `pageSize` (int): Số lượng trên một trang (default 10).

#### Response (200 OK)
```json
{
  "items": [
    {
      "id": "11223344-5566-7788-99aa-bbccddeeff00",
      "walletId": "5a4b3c2d-1e0f-9a8b-7c6d-5e4f3a2b1c0d",
      "type": "EscrowHold",
      "amount": -500000.0,
      "balanceBefore": 2000000.0,
      "balanceAfter": 1500000.0,
      "referenceId": "e3b0c442-989b-4643-9b0c-1b8f042e88a0",
      "referenceType": "Booking",
      "description": "Tạm giữ tiền cho booking SB-2026-98124",
      "createdAtUtc": "2026-09-14T03:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 45,
  "totalPages": 5
}
```

---

### 3. Khởi tạo thanh toán (Dual Gateway: VNPAY & PayOS)
- **Method:** `POST`
- **URL:** `/api/v1/payments/create-payment`
- **Mô tả:** Khởi tạo thanh toán cho đơn đặt lịch hoặc nạp tiền ví, hỗ trợ cả 2 cổng VNPAY Sandbox (Redirect thẻ test) và PayOS (VietQR quét tiền thật).
- **Role:** `Authenticated`

#### Request Body
```json
{
  "bookingId": "e3b0c442-989b-4643-9b0c-1b8f042e88a0",
  "amount": 500000.0,
  "gateway": "PayOS", // "PayOS" hoặc "VnPay"
  "description": "Thanh toan lich hen SB98124",
  "returnUrl": "https://skillbridge.vn/checkout/result",
  "cancelUrl": "https://skillbridge.vn/checkout/cancel"
}
```

#### Response khi chọn PayOS (200 OK)
```json
{
  "orderCode": 98124,
  "gateway": "PayOS",
  "amount": 500000.0,
  "qrCode": "00020101021238580010A0000007270128000697042201140000123456780208QRIBFTTA530370454065000005802VN62170813SB981246304A1B2",
  "paymentUrl": "https://pay.payos.vn/web/...",
  "accountNumber": "0383123456",
  "accountName": "NGUYEN VAN A",
  "bin": "970422",
  "expiresAtUtc": "2026-09-14T14:40:00Z"
}
```

#### Response khi chọn VnPay Sandbox (200 OK)
```json
{
  "orderCode": 98124,
  "gateway": "VnPay",
  "amount": 500000.0,
  "paymentUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_Amount=50000000&vnp_Command=pay&vnp_CreateDate=20260914143000&vnp_CurrCode=VND&vnp_IpAddr=127.0.0.1&vnp_Locale=vn&vnp_OrderInfo=Thanh+toan+lich+hen+SB98124&vnp_OrderType=other&vnp_ReturnUrl=https%3A%2F%2Fskillbridge.vn%2Fcheckout%2Fresult&vnp_TmnCode=VNPAYDEMO&vnp_TxnRef=SB98124&vnp_Version=2.1.0&vnp_SecureHash=...",
  "expiresAtUtc": "2026-09-14T14:45:00Z"
}
```

---

### 4. Nhận Webhook từ Payment Gateway (Dual Gateway Webhook)
- **Method:** `POST`
- **URL:** `/api/v1/payments/webhooks/{gateway}`
- **Mô tả:** Endpoint công khai tiếp nhận tín hiệu thanh toán từ PayOS hoặc VNPAY IPN. Đảm bảo Idempotency (chống cộng tiền lặp) và chữ ký số.
- **Role:** `Anonymous` (Xác thực chữ ký HMAC)

#### Path Variables
- `gateway`: `payos` hoặc `vnpay`.

#### Xử lý cổng PayOS (`/api/v1/payments/webhooks/payos`):
- **Header:** Nhận signature từ PayOS payload hoặc webhook signature header.
- **Thuật toán:** HMAC-SHA256 với `ChecksumKey` được cấp bởi PayOS.
- **Payload:**
```json
{
  "code": "00",
  "desc": "success",
  "data": {
    "orderCode": 98124,
    "amount": 500000,
    "description": "Thanh toan lich hen SB98124",
    "accountNumber": "0383123456",
    "reference": "FT26091412345678",
    "transactionDateTime": "2026-09-14 14:31:00",
    "paymentLinkId": "c4d3e2f1-0a9b-8c7d-6e5f-4a3b2c1d0e9f",
    "code": "00",
    "desc": "success"
  },
  "signature": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
}
```
- **Response PayOS yêu cầu:**
```json
{
  "success": true
}
```

#### Xử lý cổng VNPAY IPN (`/api/v1/payments/webhooks/vnpay`):
- **Phương thức:** `GET` hoặc `POST` (VNPAY IPN truyền qua Query String).
- **Thuật toán:** HMAC-SHA512 với `HashSecret` của Merchant Sandbox.
- **Response VNPAY yêu cầu:**
```json
{
  "RspCode": "00",
  "Message": "Confirm Success"
}
```

---

### 5. Xem trạng thái Escrow
- **Method:** `GET`
- **URL:** `/api/v1/payments/bookings/{bookingId}/escrow`
- **Mô tả:** Lấy thông tin hợp đồng Escrow của một booking.
- **Role:** `Authenticated` (Mentee/Mentor của booking đó)

#### Response (200 OK)
```json
{
  "id": "b1c2d3e4-f5a6-b7c8-d9e0-f1a2b3c4d5e6",
  "bookingId": "e3b0c442-989b-4643-9b0c-1b8f042e88a0",
  "totalAmount": 500000.0,
  "platformFeeAmount": 50000.0,
  "mentorNetAmount": 450000.0,
  "status": "Holding",
  "releasedAtUtc": null,
  "createdAtUtc": "2026-09-14T03:00:00Z"
}
```

---

### 6. Rút tiền (Withdraw)
- **Method:** `POST`
- **URL:** `/api/v1/payments/withdraw`
- **Mô tả:** Mentor yêu cầu rút tiền từ ví về tài khoản ngân hàng.
- **Role:** `Mentor`

#### Request Body
```json
{
  "amount": 2000000.0,
  "bankCode": "VCB",
  "accountNumber": "1234567890",
  "accountName": "NGUYEN VAN A"
}
```

#### Response (200 OK)
```json
{
  "transactionId": "f0e1d2c3-b4a5-9876-5432-10fedcba9876",
  "status": "Pending"
}
```

#### Phản hồi lỗi
- **422 Unprocessable Entity:** Số dư không đủ để rút.
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Insufficient Balance",
  "status": 422,
  "detail": "Wallet balance is not enough to process withdrawal.",
  "instance": "/api/v1/payments/withdraw",
  "extensions": {
    "correlationId": "xyz987"
  }
}
```

---

### 7. Lịch sử rút tiền
- **Method:** `GET`
- **URL:** `/api/v1/payments/payouts`
- **Mô tả:** Danh sách các yêu cầu rút tiền của Mentor.
- **Role:** `Mentor`

#### Response (200 OK)
```json
{
  "items": [
    {
      "id": "f0e1d2c3-b4a5-9876-5432-10fedcba9876",
      "amount": 2000000.0,
      "status": "Completed",
      "bankCode": "VCB",
      "createdAtUtc": "2026-09-10T08:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

## Các Quy tắc nghiệp vụ (Business Rules)
1. **Escrow (Ký quỹ):** Tiền của Mentee bị giữ (Holding) khi Booking được tạo hoặc thanh toán. Sau khi buổi học kết thúc (SessionCompleted), chờ thời gian tranh chấp (Dispute window: 24-48h). Nếu không có khiếu nại, hợp đồng Escrow đổi trạng thái thành `Released`, tiền được chuyển cho Mentor (trừ phí).
2. **Platform Fee:** Nền tảng tự động trích một phần trăm (%) từ tổng số tiền Booking mỗi khi hoàn thành giao dịch (PlatformFee transaction).
3. **Double-entry Ledger:** Mọi giao dịch ví (`wallet_transactions`) phải có `balance_before` và `balance_after` để đảm bảo tính toàn vẹn (tổng = 0 hoặc khớp số dư tuyệt đối).
4. **Idempotency & Webhook Verification:** 
   - Webhook phải kiểm tra chữ ký (Signature) hợp lệ để tránh giả mạo.
   - Idempotent: Phải check `gateway_transaction_id` để tránh xử lý trùng lặp giao dịch nạp tiền.
5. **Refunds:** Nghe sự kiện `BookingCancelledIntegrationEvent`, thực hiện hoàn tiền vào ví Mentee nếu bị hủy hợp lệ và Escrow chưa bị Released.
