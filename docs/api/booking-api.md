# Tài liệu API Contract - Booking Module (SkillBridge)

## Giới thiệu
Tài liệu định nghĩa API Contract cho **Booking Module** của hệ thống SkillBridge. Trách nhiệm của module là xử lý quy trình đặt lịch hẹn: `Draft → PendingPayment → Confirmed → InProgress → Completed / Cancelled / Refunded`.

---

## Các API Endpoints

### 1. Tạo booking mới (Productized Service + Intake Form + Khóa 10p Redis)
- **Method:** `POST`
- **URL:** `/api/v1/booking/bookings`
- **Mô tả:** [Mentee] Đặt lịch theo Gói dịch vụ cụ thể, điền Intake Form 3 câu hỏi bắt buộc, kích hoạt Khóa nguyên tử Redis 10 phút.
- **Role:** `Mentee`

#### Request Body
```json
{
  "serviceId": "7c8d9e0f-1a2b-3c4d-5e6f-7a8b9c0d1e2f",
  "slotId": "b1bc7e1f-7b0b-4b10-a0bc-9f07a216521a",
  "intakeForm": {
    "coreQuestion": "Cần review kiến trúc Clean Architecture và chia module C#.NET 10",
    "attachedLinks": "https://github.com/my-repo/project, https://drive.google.com/cv.pdf",
    "targetGoals": "Tối ưu hóa boundary giữa Booking và Payments module sau 60 phút"
  }
}
```

#### Response (201 Created)
```json
{
  "id": "e3b0c442-989b-4643-9b0c-1b8f042e88a0",
  "bookingCode": "SB-2026-98124",
  "status": "PendingPayment",
  "totalAmount": 500000.0,
  "heldUntilUtc": "2026-09-14T14:40:00Z", // Hết hạn giữ chỗ sau 10 phút
  "createdAtUtc": "2026-09-14T14:30:00Z"
}
```

#### Phản hồi lỗi
- **400 Bad Request:** Thông tin không hợp lệ
- **404 Not Found:** Không tìm thấy Mentor hoặc Slot
- **409 Conflict:** Slot đã được đặt hoặc không khả dụng

---

### 2. Xem chi tiết booking
- **Method:** `GET`
- **URL:** `/api/v1/booking/bookings/{bookingId}`
- **Mô tả:** Lấy thông tin chi tiết một booking.
- **Role:** `Authenticated` (Mentee/Mentor liên quan đến booking hoặc Admin)

#### Response (200 OK)
```json
{
  "id": "e3b0c442-989b-4643-9b0c-1b8f042e88a0",
  "bookingCode": "SB-2026-98124",
  "menteeId": "d0e1b2f3-c4a5-48b6-9c7d-e8f90a1b2c3d",
  "mentorId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "slotId": "b1bc7e1f-7b0b-4b10-a0bc-9f07a216521a",
  "status": "PendingPayment",
  "topic": "Tư vấn cấu trúc hệ thống Microservices",
  "menteeNote": "Mong mentor tập trung vào phần giao tiếp giữa các services",
  "mentorNote": null,
  "scheduledStartUtc": "2026-09-15T14:00:00Z",
  "scheduledEndUtc": "2026-09-15T15:00:00Z",
  "actualStartUtc": null,
  "actualEndUtc": null,
  "amount": 500000.0,
  "currency": "VND",
  "cancellationReason": null,
  "cancelledBy": null,
  "createdAtUtc": "2026-09-14T02:55:38Z",
  "updatedAtUtc": "2026-09-14T02:55:38Z"
}
```

---

### 3. Danh sách bookings
- **Method:** `GET`
- **URL:** `/api/v1/booking/bookings`
- **Mô tả:** Danh sách bookings của user, có phân trang và lọc theo trạng thái.
- **Role:** `Authenticated`

#### Query Parameters
- `status` (string, optional): Lọc theo trạng thái.
- `page` (int): Trang hiện tại (default 1).
- `pageSize` (int): Số lượng trên một trang (default 10).

#### Response (200 OK)
```json
{
  "items": [
    {
      "id": "e3b0c442-989b-4643-9b0c-1b8f042e88a0",
      "bookingCode": "SB-2026-98124",
      "status": "Confirmed",
      "topic": "Tư vấn cấu trúc hệ thống Microservices",
      "scheduledStartUtc": "2026-09-15T14:00:00Z",
      "scheduledEndUtc": "2026-09-15T15:00:00Z",
      "createdAtUtc": "2026-09-14T02:55:38Z"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 25,
  "totalPages": 3
}
```

---

### 4. Xác nhận booking
- **Method:** `POST`
- **URL:** `/api/v1/booking/bookings/{bookingId}/confirm`
- **Mô tả:** [Mentor] Xác nhận đồng ý booking sau khi tiền đã vào Escrow (PendingPayment).
- **Role:** `Mentor`

#### Request Body
```json
{
  "mentorNote": "Sẽ chuẩn bị sẵn slide về gRPC và RabbitMQ"
}
```

#### Response (204 No Content)

#### Phản hồi lỗi
- **400 Bad Request:** Status không hợp lệ để confirm (không phải PendingPayment).
- **403 Forbidden:** Không phải mentor của booking này.

---

### 5. Hủy booking
- **Method:** `POST`
- **URL:** `/api/v1/booking/bookings/{bookingId}/cancel`
- **Mô tả:** Hủy booking. Mentee chỉ được hủy trước 24h. Mentor hủy bất kỳ lúc nào nhưng sẽ ghi nhận tỷ lệ.
- **Role:** `Mentee`, `Mentor`

#### Request Body
```json
{
  "reason": "Bận việc đột xuất không thể tham gia"
}
```

#### Response (204 No Content)

#### Phản hồi lỗi
- **400 Bad Request:** Quá hạn thời gian hủy cho Mentee hoặc trạng thái không cho phép.
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Cancellation Failed",
  "status": 400,
  "detail": "Mentee can only cancel booking 24 hours prior to the scheduled start time.",
  "instance": "/api/v1/booking/bookings/e3b0c442-989b-4643-9b0c-1b8f042e88a0/cancel",
  "extensions": {
    "correlationId": "xyz123"
  }
}
```

---

### 6. Hoàn thành booking
- **Method:** `POST`
- **URL:** `/api/v1/booking/bookings/{bookingId}/complete`
- **Mô tả:** Đánh dấu booking đã hoàn tất sau khi session kết thúc.
- **Role:** `Mentee`, `Mentor`

#### Response (204 No Content)

---

### 7. Yêu cầu dời lịch
- **Method:** `POST`
- **URL:** `/api/v1/booking/bookings/{bookingId}/reschedule`
- **Mô tả:** Tạo yêu cầu thay đổi slot giờ.
- **Role:** `Mentee`, `Mentor`

#### Request Body
```json
{
  "newSlotId": "c4d5e6f7-a1b2-3c4d-5e6f-7a8b9c0d1e2f",
  "reason": "Xin dời qua ngày mai do có lịch công tác"
}
```

#### Response (201 Created)
```json
{
  "requestId": "f4g5h6j7-k8l9-m0n1-o2p3-q4r5s6t7u8v9"
}
```

---

### 8. Phản hồi yêu cầu dời lịch
- **Method:** `POST`
- **URL:** `/api/v1/booking/reschedule-requests/{requestId}/respond`
- **Mô tả:** Đồng ý hoặc từ chối yêu cầu dời lịch.
- **Role:** `Mentee`, `Mentor` (Bên nhận yêu cầu)

#### Request Body
```json
{
  "isApproved": true,
  "responseNote": "Đồng ý dời lịch"
}
```

#### Response (204 No Content)

---

### 9. Xem lịch sử thay đổi trạng thái (Audit Logs)
- **Method:** `GET`
- **URL:** `/api/v1/booking/bookings/{bookingId}/audit-logs`
- **Mô tả:** Xem lịch sử chuyển trạng thái của một booking.
- **Role:** `Authenticated`

#### Response (200 OK)
```json
{
  "items": [
    {
      "id": "1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d",
      "oldStatus": "Draft",
      "newStatus": "PendingPayment",
      "changedByUserId": "d0e1b2f3-c4a5-48b6-9c7d-e8f90a1b2c3d",
      "reason": "Mentee initiated payment",
      "createdAtUtc": "2026-09-14T03:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 1,
  "totalPages": 1
}
```

## Các Quy tắc nghiệp vụ (Business Rules)
1. **State Machine:** Trạng thái phải chuyển đúng luồng: Draft → PendingPayment → Confirmed → InProgress → Completed/Cancelled/Refunded.
2. **Cancellation:** Mentee chỉ có thể hủy trước lịch hẹn ít nhất 24 giờ. Mentor có thể hủy mọi lúc nhưng bị hệ thống lưu lại vết (ảnh hưởng rating).
3. **Mã Booking:** Sinh tự động theo format `SB-{year}-{random5digit}` (VD: `SB-2026-98124`).
4. **Reschedule:** Nếu được Accept, `newSlotId` thay thế `slotId`, trạng thái thành `Rescheduled`. Lịch trình cũ bị hủy, khóa slot mới.
5. **Event Emission:** Khi Confirm, tạo ra `BookingConfirmedIntegrationEvent` để module Learning tạo session. Khi Cancel, tạo ra `BookingCancelledIntegrationEvent` để Payments hoàn tiền.
