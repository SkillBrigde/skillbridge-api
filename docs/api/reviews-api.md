# Reviews Module API Contract

**Trách nhiệm:** Đánh giá sao và nhận xét sau buổi học.

## Database Schema
- **`reviews`**: id (UUID), booking_id, session_id, mentor_id, mentee_id, overall_rating (1-5), communication_rating (1-5), expertise_rating (1-5), helpfulness_rating (1-5), comment, mentor_reply, mentor_replied_at_utc, is_visible, created_at_utc, updated_at_utc
- **`mentor_rating_summaries`**: id, mentor_id, average_overall, average_communication, average_expertise, average_helpfulness, total_reviews, last_updated_at_utc

## Integration Events
- **Lắng nghe:** `SessionCompletedIntegrationEvent` → Mở khóa đánh giá cho Mentee.
- **Phát hành:** `ReviewSubmittedIntegrationEvent` → Cập nhật thống kê, báo Recommendations.

---

## Các Endpoints (REST API)

### 1. Đánh giá sau buổi học
- **URL:** `/api/v1/reviews`
- **Method:** `POST`
- **Auth Requirement:** `Mentee`
- **Description:** Đánh giá buổi học sau khi hoàn thành. Chỉ được đánh giá 1 lần cho mỗi booking.

#### Request Body
```json
{
  "bookingId": "7a885f64-5717-4562-b3fc-2c963f66afa6",
  "sessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "mentorId": "5b885f64-5717-4562-b3fc-2c963f66afa6",
  "overallRating": 5,
  "communicationRating": 5,
  "expertiseRating": 4,
  "helpfulnessRating": 5,
  "comment": "Buổi học rất bổ ích."
}
```
#### Response (201 Created)

#### Error Response (409 Conflict)
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Conflict",
  "status": 409,
  "detail": "Review already exists for this booking",
  "instance": "/api/v1/reviews"
}
```

### 2. Xem chi tiết review
- **URL:** `/api/v1/reviews/{reviewId}`
- **Method:** `GET`
- **Auth Requirement:** `Anonymous`
- **Description:** Xem chi tiết một đánh giá.

### 3. Sửa review
- **URL:** `/api/v1/reviews/{reviewId}`
- **Method:** `PUT`
- **Auth Requirement:** `Mentee` (Người tạo review)
- **Description:** Mentee có thể sửa review trong vòng 48h sau khi tạo.

#### Request Body
```json
{
  "overallRating": 5,
  "comment": "Cập nhật đánh giá..."
}
```
#### Response (204 No Content)

### 4. Phản hồi review
- **URL:** `/api/v1/reviews/{reviewId}/reply`
- **Method:** `POST`
- **Auth Requirement:** `Mentor`
- **Description:** Mentor phản hồi lại review của Mentee.

#### Request Body
```json
{
  "reply": "Cảm ơn bạn đã nhận xét!"
}
```
#### Response (204 No Content)

### 5. Danh sách reviews của Mentor
- **URL:** `/api/v1/reviews/mentors/{mentorId}`
- **Method:** `GET`
- **Auth Requirement:** `Anonymous`
- **Description:** Lấy danh sách các đánh giá public của một mentor (phân trang).

#### Response (200 OK)
```json
{
  "items": [
    {
      "id": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
      "overallRating": 5,
      "comment": "Rất hay",
      "mentorReply": "Cảm ơn!"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 25,
  "totalPages": 3
}
```

### 6. Tổng hợp rating của Mentor
- **URL:** `/api/v1/reviews/mentors/{mentorId}/summary`
- **Method:** `GET`
- **Auth Requirement:** `Anonymous`
- **Description:** Lấy thông tin thống kê rating (Read Model).

#### Response (200 OK)
```json
{
  "mentorId": "5b885f64-5717-4562-b3fc-2c963f66afa6",
  "averageOverall": 4.8,
  "averageCommunication": 4.9,
  "averageExpertise": 4.7,
  "averageHelpfulness": 4.8,
  "totalReviews": 25
}
```

### 7. Danh sách các buổi học chưa đánh giá
- **URL:** `/api/v1/reviews/my-pending`
- **Method:** `GET`
- **Auth Requirement:** `Mentee`
- **Description:** Danh sách các buổi học đã Completed nhưng Mentee chưa đánh giá.

#### Response (200 OK)
```json
{
  "items": [
    {
      "sessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "mentorId": "5b885f64-5717-4562-b3fc-2c963f66afa6"
    }
  ]
}
```

## Business Rules
- Chỉ được đánh giá khi trạng thái buổi học là `Completed`.
- 1 booking chỉ tạo được 1 review duy nhất.
- Hỗ trợ sửa review trong vòng 48h từ khi tạo.
- Điểm đánh giá (Rating) từ 1 đến 5 cho 4 tiêu chí.
- Bảng tổng hợp (`mentor_rating_summaries`) sẽ tự động cập nhật mỗi khi có review mới.
