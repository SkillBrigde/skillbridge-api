# Tài Liệu API Contracts - Nền Tảng SkillBridge

Tài liệu này đóng vai trò là **Hợp đồng giao tiếp (API Contract)** chính thức giữa Backend (.NET 10 API) và Frontend (Next.js 16 BFF Web Portal).  
Mọi thay đổi về Request Body, Response Schema hoặc Status Code bắt buộc phải được cập nhật tại thư mục này trước khi triển khai code.

---

## 1. Danh Mục API Theo 7 Bounded Contexts (Chuẩn Master DOCX v4)

Toàn bộ đặc tả chi tiết từng biến đầu vào/đầu ra của 65 API đã được lập trình viên và BA đối soát tại:  
👉 **Excel Chi Tiết:** [SkillBridge_API_Master_Specification.xlsx](../../SkillBridge_API_Master_Specification.xlsx)  
👉 **Excel User Stories:** [SkillBridge_User_Stories_Master_Matrix.xlsx](../../SkillBridge_User_Stories_Master_Matrix.xlsx)

| Phân Hệ (Bounded Context) | Bảng CSDL Liên Quan | Trách Nhiệm Nghiệp Vụ Chính | Số Endpoints |
| :--- | :--- | :--- | :---: |
| **1. Identity & RBAC** | `users`, `roles`, `user_roles`, `user_external_logins` | Đăng ký, đăng nhập Email/Password, Google OAuth2 qua BFF, phân quyền đa vai trò. | 10 |
| **2. Sessions & Thiết Bị** | `user_sessions`, `refresh_tokens` | Quản lý phiên đa thiết bị, đăng xuất từ xa, Token Rotation chống Replay Attack. | 4 |
| **3. Profiles & Services** | `mentor_profiles`, `mentor_services`, `skills`, `mentor_skills` | Hồ sơ Mentor, KYC CCCD tích xanh, đóng gói 3 gói dịch vụ kết quả, từ điển kỹ năng. | 13 |
| **4. Scheduling & Slots** | `mentor_availability_rules`, `schedule_slots` | Khung giờ rảnh lặp lại, sinh slot tự động 14 ngày gối đầu, báo nghỉ đột xuất Time-Off. | 7 |
| **5. Bookings & Sessions** | `bookings`, `booking_intake_forms`, `booking_sessions` | Đặt gói dịch vụ, Intake Form 3 câu hỏi, khóa nguyên tử Redis 10p, điểm danh Meet, ảnh nghiệm thu MinIO. | 11 |
| **6. Payments & Dual Escrow** | `wallets`, `wallet_transactions`, `payment_orders`, `escrow_contracts`, `payout_requests` | **Dual Gateway (PayOS VietQR tiền thật STK + VNPAY Sandbox)**, Két Escrow đa mốc, Payout T+3 & OTP. | 10 |
| **7. Trust & Disputes** | `reviews`, `disputes`, `conversations`, `messages` | Đánh giá 4 tiêu chí + VIP badge, Tranh chấp 24h đóng băng Escrow, Admin phân xử SLA 48h, Chat SignalR chống ăn mảnh. | 10 |
| **TỔNG CỘNG** | **24 Bảng Chuyên Trách** | **Chuẩn Kiến Trúc 7 Bounded Contexts** | **65** |

---

## 2. Quy Chuẩn Chung (Common API Conventions)

### Base URL
- **Môi trường Local**: `https://localhost:5001/api/v1` hoặc `http://localhost:5000/api/v1`
- **Môi trường Web BFF**: `https://localhost:3000/api` (Tự động proxy và đính kèm JWT Token nội bộ)

### Chuẩn Xác Thực (Authentication)
- Trình duyệt giao tiếp với Next.js BFF qua Cookie bảo mật cao: `Set-Cookie: __Host-session=...; HttpOnly; Secure; SameSite=Strict`.
- Next.js BFF gọi sang .NET 10 API qua Header: `Authorization: Bearer <access_token>`.

### Chuẩn Định Dạng Phản Hồi (Envelope Pattern)
```json
{
  "isSuccess": true,
  "data": { ... },
  "error": null
}
```

Khi có lỗi:
```json
{
  "isSuccess": false,
  "data": null,
  "error": {
    "code": "Slot.AlreadyBooked",
    "message": "Khung giờ này đã có người giữ chỗ hoặc đã được đặt.",
    "type": "Conflict"
  }
}
```
