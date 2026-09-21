# Tài Liệu API Contracts - Nền Tảng SkillBridge

Tài liệu này đóng vai trò là **Hợp đồng giao tiếp (API Contract)** chính thức giữa Backend (.NET 10 API) và Frontend (Next.js 16 BFF Web Portal).  
Mọi thay đổi về Request Body, Response Schema hoặc Status Code bắt buộc phải được cập nhật tại thư mục này trước khi triển khai code.

---

## 1. Danh Mục API Theo 10 Bounded Contexts (Chuẩn Hóa Enterprise)

Toàn bộ hệ thống được chia thành **10 Modules độc lập**, mỗi module sở hữu riêng **1 PostgreSQL Schema** và kiểm soát ranh giới dữ liệu khép kín:

| Phân Hệ (Bounded Context) | PostgreSQL Schema | Bảng CSDL Trọng Yếu (46 Bảng) | Trách Nhiệm Nghiệp Vụ Chính | Số Endpoints | Hợp Đồng Chi Tiết |
| :--- | :---: | :--- | :--- | :---: | :--- |
| **1. Identity** | `identity` | `users`, `roles`, `user_roles`, `refresh_tokens`, `external_logins` | Đăng ký, đăng nhập Email/Password, Google OAuth2, RBAC, Token Rotation, chống Brute-force Lockout. | 12 | [`identity-api.md`](./identity-api.md) |
| **2. Profiles** | `profiles` | `mentor_profiles`, `mentee_profiles`, `mentor_skills`, `mentor_experiences`, `mentor_certifications` | Hồ sơ chuyên gia/học viên, xác minh danh tính KYC (tích xanh), kinh nghiệm, chứng chỉ. | 14 | [`profiles-api.md`](./profiles-api.md) |
| **3. Catalog** | `catalog` | `categories`, `skills`, `tags`, `skill_tags`, `mentor_services` | Từ điển kỹ năng phân cấp cây, gắn nhãn Tags, đóng gói 3 gói dịch vụ chuẩn hóa (Tier 1-3). | 11 | [`catalog-api.md`](./catalog-api.md) |
| **4. Scheduling** | `scheduling` | `scheduling_settings`, `availability_rules`, `schedule_slots`, `time_offs` | Cấu hình lịch rảnh tuần lặp lại, tự động sinh bookable slots, chặn lịch nghỉ đột xuất (Time-off). | 10 | [`scheduling-api.md`](./scheduling-api.md) |
| **5. Booking** | `booking` | `bookings`, `booking_intake_forms`, `booking_sessions`, `reschedule_requests`, `booking_audit_logs` | Đặt lịch, khảo sát Intake Form 3 câu hỏi, khóa nguyên tử Redis 10 phút, máy trạng thái State Machine. | 9 | [`booking-api.md`](./booking-api.md) |
| **6. Payments** | `payments` | `wallets`, `wallet_transactions`, `payment_orders`, `payment_webhooks_audit`, `escrow_contracts`, `payout_requests` | **Dual Gateway (PayOS VietQR Napas 24/7 tiền thật + VNPAY Sandbox)**, Két Ký Quỹ Escrow, Sổ cái kép, Rút tiền. | 7 | [`payments-api.md`](./payments-api.md) |
| **7. Learning** | `learning` | `learning_sessions`, `session_attendances`, `session_notes`, `session_materials`, `session_action_items` | Phòng học Google Meet, tự động điểm danh, ghi chú 2 chiều, tài liệu đính kèm, giao bài tập hành động. | 16 | [`learning-api.md`](./learning-api.md) |
| **8. Messaging** | `messaging` | `conversations`, `conversation_participants`, `messages`, `message_reads`, `message_attachments` | Chat 1-1 thời gian thực, SignalR Hub, phân trang Cursor, tự động bắt cờ chống giao dịch ngoài sàn. | 8 + 1 Hub | [`messaging-api.md`](./messaging-api.md) |
| **9. Reviews** | `reviews` | `reviews`, `mentor_rating_summaries`, `disputes` | Đánh giá 4 tiêu chí (Tổng quan, Giao tiếp, Chuyên môn, Hữu ích), VIP badge, Phán xử tranh chấp SLA 48h. | 7 | [`reviews-api.md`](./reviews-api.md) |
| **10. Recommendations**| `recommendations` | `mentor_recommendation_metrics`, `mentee_interaction_logs`, `recommendation_logs` | Gợi ý Mentor cá nhân hóa dựa trên sở thích, theo dõi xu hướng Trending Skills, Dashboard hiệu suất. | 5 | [`recommendations-api.md`](./recommendations-api.md) |
| **TỔNG CỘNG** | **10 Schemas** | **46 Bảng Chuyên Trách** | **Kiến Trúc Chuẩn Doanh Nghiệp .NET 10 Modular Monolith** | **99 REST + 1 Hub** | |

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
