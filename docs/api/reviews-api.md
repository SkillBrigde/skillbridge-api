# Reviews API — đánh giá gói đã hoàn tất

Base `/api/v1/reviews`. 7 REST API, schema riêng `reviews`.

| Method | URL | Quyền | Kết quả |
|---|---|---|---|
| POST | `/` | Mentee của Booking | 201 PublicReview; đủ bằng chứng hoàn tất cả gói, mỗi booking một review |
| GET | `/{reviewId}` | Anonymous | 200 review public đang visible, 404 nếu không có/ẩn |
| PUT | `/{reviewId}` | Mentee tác giả | 204, chỉ trước thời điểm tạo + 48 giờ |
| POST | `/{reviewId}/reply` | Mentor của Booking | 204; cho phép cập nhật phản hồi của chính Mentor |
| GET | `/mentors/{mentorId}` | Anonymous | Danh sách visible, page/pageSize mặc định 1/10, tối đa 100 |
| GET | `/mentors/{mentorId}/summary` | Anonymous | Trung bình 4 tiêu chí và totalReviews; chưa có review trả 0 |
| GET | `/my-pending` | Mentee | Các Booking của chính mình đã Completed, chưa có review; phân trang |

## Request và bằng chứng

POST: `bookingId`, `sessionId`, `mentorId`, `overallRating`, `communicationRating`, `expertiseRating`, `helpfulnessRating`, `comment`.

`sessionId` là **ID buổi học Learning**, phải thuộc booking. Dù gói có nhiều buổi vẫn chỉ có một review/booking. Booking phải Completed và Learning có đủ đúng tập bookingSessionId đã Completed, actualEndUtc không ở tương lai. Mentor và tác giả lấy từ dữ liệu server; không tin quan hệ do client khai. Request sai mentor/session trả 409, không thuộc Mentee trả 404.

PUT: `overallRating`, `comment`; ba tiêu chí còn lại tùy chọn và được giữ nguyên khi bỏ qua. Điểm đều là số nguyên 1–5. Nhận xét/phản hồi bắt buộc, tối đa 4000 ký tự, không chứa ký tự điều khiển ngoài tab/xuống dòng. POST reply: `{ "reply": "Cảm ơn" }`.

POST create cùng lúc bị khóa theo booking và có unique index; một yêu cầu thắng, các yêu cầu còn lại trả 409. Sửa/phản hồi khóa theo review; validation thất bại không đổi dữ liệu hoặc phát outbox. 48 giờ tính từ lần tạo, không kéo dài sau mỗi lần sửa; đúng thời điểm +48h đã đóng.

## Response

PublicReview gồm `id`, `mentorId`, 4 điểm, `comment`, `mentorReply`, `mentorRepliedAtUtc`, `createdAtUtc`, `updatedAtUtc`. Không công khai bookingId, sessionId hoặc userId Mentee qua route Anonymous. Frontend hiển thị comment/reply như plain text, không render HTML tùy ý.

Summary: `mentorId`, `averageOverall`, `averageCommunication`, `averageExpertise`, `averageHelpfulness`, `totalReviews`. Summary đọc aggregate trực tiếp từ các review visible, phản ánh cả cập nhật điểm, không có bảng cache summary thứ hai dễ lệch. Pending: `items` chứa `bookingId`, `mentorId`, `sessionId` đại diện; chỉ xuất hiện sau khi hoàn tất cả gói.

Lỗi dùng Problem Details: 400 validation/phân trang, 401/403 auth/role, 404 sai chủ/không có, 409 chưa hoàn tất/trùng/quá hạn, 503 nguồn bằng chứng chưa khả dụng.

## Infra

Migration `AddVerifiedReviews`, lịch sử `reviews.__EFMigrationsHistory`. Bảng `reviews`, `outbox_messages`; không FK sang Booking/Learning/Profiles. Host nối cổng IReviewEvidence qua các context sở hữu dữ liệu; module Reviews không reference module khác.

Outbox lưu Submitted/Updated/Replied cùng transaction; summary không phụ thuộc dispatcher. Recommendations đọc snapshot rating hiện tại. Dispatcher outbox toàn hệ thống, dispute/moderation và badge ngoài 7 API này chưa được triển khai trong contract hiện tại.
