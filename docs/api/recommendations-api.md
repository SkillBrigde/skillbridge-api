# Recommendations API — dữ liệu thật, thuật toán rules-v1

Base `/api/v1/recommendations`, 5 API. Không sinh số liệu demo hoặc gán điểm trung bình cho Mentor chưa có review.

| Method | URL | Quyền | Response |
|---|---|---|---|
| GET | `/mentors?page=1&pageSize=20` | Mentee | Mentor được xếp theo score, reasons, metrics; lưu log rules-v1 của trang trả về |
| GET | `/mentors/for-skill/{skillId}?page=1&pageSize=10` | Anonymous | Mentor có kỹ năng Catalog hoạt động; 404 kỹ năng không hoạt động/không có |
| POST | `/interactions` | Mentee | 204, ghi tương tác của chính user từ JWT |
| GET | `/mentors/{mentorId}/metrics` | Admin hoặc Mentor sở hữu profile | 200 metrics snapshot, 403 sai chủ, 404 không có profile, 503 snapshot chưa sẵn sàng |
| GET | `/trending-skills` | Anonymous | Tối đa 20 kỹ năng, trendingScore và tên Catalog hiện tại |

Phân trang page >= 1, pageSize 1–100, có kiểm tra tràn số. List: `items/page/pageSize/totalCount/totalPages`; tie score được sắp theo mentorId để thứ tự ổn định.

## Dữ liệu và cách tính

Host cung cấp dữ liệu qua IRecommendationSources; các module không reference nhau. Chỉ gợi ý Mentor có tài khoản hoạt động, role Mentor, profile nhận học viên, không Rejected và có dịch vụ Catalog đang hoạt động. Điều kiện này kiểm tra lại khi request, không đợi hết chu kỳ metrics khi tài khoản bị khóa.

Worker làm mới metrics khi khởi động và mỗi phút. Batch cập nhật nguyên tử; lỗi giữ snapshot trước đó. GET metrics đọc snapshot, không tự chạy lại batch. Sources gồm Profiles/Catalog, lịch sử Booking có paymentId và audit xác nhận, buổi Learning Completed, review visible hiện tại. Không dùng các tổng giả/stale trên profile làm nguồn điểm.

- acceptanceRate = số Booking từng có audit Confirmed / số Booking đã nhận thanh toán; gồm cả các đơn hiện đang chờ Mentor trong mẫu số.
- completionRate = số Booking Completed / số Booking từng Confirmed. Gói đang học vẫn thuộc mẫu số.
- averageResponseTimeMinutes = trung bình chênh lệch từ audit AwaitingMentor đến Confirmed; chưa có cặp audit hợp lệ trả null.
- totalSessions = số buổi Learning Completed có actualEndUtc <= thời gian server.
- repeatMenteeCount = số Mentee có ít nhất hai Booking Completed với Mentor.
- averageRating/totalReviews lấy từ Reviews visible; không review trả null/0. Không mẫu số cho tỷ lệ trả null, không giả định đạt 100%.

Score = 45% khớp kỹ năng quan tâm + 20% rating/5 + 10% completionRate + 10% acceptanceRate + 5% `1/(1+responseMinutes/60)` + 10% khớp ngân sách. Skill match là tỷ lệ trọng số kỹ năng Mentor có trên tổng sở thích. Sở thích lấy từ tương tác có skillId trong 90 ngày: Click trọng số 2, ViewProfile trọng số 1. Trường chưa có bằng chứng đóng góp 0; score nằm [0,1], không phải xác suất học thành công. Không có lịch sử thì xếp theo bằng chứng hiện có và mentorId; reasons nêu rõ chưa đủ dữ liệu.

Ngân sách lấy từ MenteeProfile, so với **tổng giá gói dịch vụ đang hoạt động** cùng loại tiền; không đổi USD sang VND hoặc so giá theo giờ với giá cả gói. Với endpoint for-skill, skill được yêu cầu là sở thích duy nhất và không dùng ngân sách cá nhân.

Response mỗi gợi ý: `mentorId`, `score`, `reasons`, `metrics` gồm averageRating, totalSessions, acceptanceRate, completionRate. Metrics chi tiết bổ sung averageResponseTimeMinutes, repeatMenteeCount, totalReviews, lastCalculatedAtUtc. Các trường thiếu mẫu có thể null.

## Tương tác và xu hướng

```json
{ "mentorId": "00000000-0000-0000-0000-000000000001", "skillId": "00000000-0000-0000-0000-000000000002", "interactionType": "Click" }
```

Chỉ `Click`/`ViewProfile`; skillId có thể bỏ qua nhưng nếu có phải là kỹ năng Catalog hoạt động thuộc Mentor đó. Không có field userId để giả danh. Tương tác không skill vẫn được ghi nhưng không tạo sở thích kỹ năng hoặc trending.

Cùng user/mentor/skill/type/ngày UTC chỉ ghi một lần, kể cả gửi đồng thời. Giới hạn 1000 tổ hợp/user/ngày. Trending là số **Mentee khác nhau** đã tương tác với kỹ năng trong 30 ngày, không tăng do gửi lặp, đổi type hoặc click nhiều Mentor cùng kỹ năng. Kỹ năng không hoạt động bị loại; response array `{skillId,name,trendingScore}`. Đây là xu hướng tương tác, không suy đoán số booking theo skill vì dịch vụ hiện chưa có quan hệ skillId đáng tin cậy.

## Infra và giới hạn

Migration AddRecommendationReadModels, lịch sử `recommendations.__EFMigrationsHistory`. Bảng `mentor_metrics`, `interactions`, `recommendation_logs`, không FK liên module. Metrics khóa PostgreSQL giữa nhiều instance, thay snapshot trong transaction; interactions có unique deduplication key và khóa theo user.

Batch hiện đọc dữ liệu để dựng snapshot và xếp hạng tập ứng viên trong bộ nhớ. Cần phân lô/incremental khi lượng dữ liệu lớn; đây là thuật toán rules-v1, chưa phải ML/A-B experiment engine. Log lưu IDs/score/algorithmVersion/thời gian để kiểm chứng, chưa có job retention; infra cần chốt thời hạn lưu dữ liệu và monitoring. Không khẳng định chất lượng gợi ý đã được đánh giá trên người dùng thật.
