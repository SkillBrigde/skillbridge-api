# VH-02 — Contract dịch vụ Mentor, phiên bản 1

**Ngày:** 2026-10-16. **Owner:** vuhaidang (Catalog); consumer: nguyendat (Profiles/Booking).

**Trạng thái: đề xuất để hai người review, chưa triển khai endpoint.** Chỉ bắt đầu
`vuhaidang/feat-catalog-mentor-services` sau khi cả hai duyệt contract này.
Sáu API dưới đây là phần bổ sung, không tính vào 40 API còn lại của vuhaidang.

## 1. Quyết định bàn giao

| Nội dung | Quy tắc đề xuất |
|---|---|
| Sở hữu dữ liệu | Catalog sở hữu `catalog.mentor_services`; Profiles sở hữu hồ sơ Mentor. |
| Mentor ID | `mentorId` là `MentorProfile.Id`, **khác** `Identity.User.Id`. `mentorUserId` là ID dùng kiểm tra quyền. |
| Ba tier | Tập giá trị cố định: `Tier1_ReviewCV`, `Tier2_MockInterview`, `Tier3_Roadmap`. Mentor không tự tạo loại thứ tư. |
| Số dịch vụ | Mentor có thể tạo nhiều dịch vụ cùng tier; tier phân loại nội dung, không phải ba bản ghi bắt buộc. |
| Gói nhiều buổi | Một dịch vụ có `totalSessions` từ 1 đến 52; một booking mua trọn một gói. Đã có yêu cầu hỗ trợ nhiều buổi. |
| Thời lượng | `durationMinutes` là thời lượng **mỗi buổi**, số nguyên 15–240, không phải thời lượng cả gói. |
| Giá | `price` là **tổng giá gói**, không nhân thêm `totalSessions` hoặc `durationMinutes`. |
| Tiền tệ | `VND` hoặc `USD`, phù hợp contract Profiles hiện tại; không quy đổi ngầm. VND là số nguyên; USD tối đa hai chữ số thập phân. |
| Giới hạn giá | `0 < price <= 100000000` VND hoặc `0 < price <= 5000` USD cho cả gói. Đây là giới hạn đề xuất cần duyệt. |
| Active | Tạo mới mặc định inactive; Mentor bật active khi nội dung đầy đủ. Inactive không nhận booking mới. |
| Tạo/sửa/xóa | JWT role `Mentor`, chỉ dịch vụ của chính mình. Không nhận quyền sở hữu từ request body. Admin chưa có API sửa hộ. |
| Xóa | Xóa mềm, ẩn khỏi mọi danh sách; không tái sử dụng ID và không làm mất snapshot của booking cũ. |
| Phiên bản | `version` bắt đầu 1, tăng sau mỗi thay đổi kể cả đổi active hoặc xóa. Dùng chống ghi đè và đối chiếu snapshot. |

Tên tier không ép mọi dịch vụ phải cùng giá, cùng thời lượng hoặc cùng số buổi.
Giá dịch vụ độc lập với `MentorProfile.HourlyRate`.
Hỗ trợ USD trong Catalog không đồng nghĩa Payments đã hỗ trợ thu USD.

## 2. API dự kiến

Base: `/api/v1/catalog`. UUID hợp lệ; lỗi dùng Problem Details.

| Method | Route | Quyền | Thành công |
|---|---|---|---|
| GET | `/mentors/{mentorId}/services` | Anonymous | 200, danh sách active chưa xóa của Mentor |
| GET | `/services/{serviceId}` | Anonymous | 200, chi tiết active chưa xóa |
| GET | `/services/me` | Mentor | 200, dịch vụ active/inactive của chính mình, chưa xóa |
| POST | `/services` | Mentor | 201, DTO + `Location` trỏ chi tiết |
| PUT | `/services/{serviceId}` | Mentor sở hữu | 200, DTO mới |
| DELETE | `/services/{serviceId}` | Mentor sở hữu | 204 |

`GET /services/me` là route literal riêng, ID routes dùng constraint `:guid`.
Public GET trả 404 cho dịch vụ inactive/đã xóa; PUT/DELETE trả 404 cho ID không
tồn tại, đã xóa hoặc của Mentor khác, tránh lộ dữ liệu người khác.
Owner xem lại dịch vụ inactive qua `/services/me`; `Location` không cấp quyền
anonymous đọc một dịch vụ mới còn inactive.

Hai API danh sách nhận `page=1`, `pageSize=10` (1–100), `serviceType` tùy chọn.
`/services/me` nhận thêm `isActive` tùy chọn. Sắp xếp `createdAtUtc` giảm dần rồi
`id` tăng dần; offset vượt `Int32.MaxValue` trả 400. Response theo `PagedResult`:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 10,
  "totalCount": 0,
  "totalPages": 0,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

### Request POST/PUT

```json
{
  "title": "Lộ trình backend .NET trong 4 buổi",
  "description": "Đánh giá mục tiêu, lập lộ trình và review tiến độ.",
  "serviceType": "Tier3_Roadmap",
  "durationMinutes": 60,
  "price": 2000000,
  "currency": "VND",
  "totalSessions": 4,
  "deliverables": "Lộ trình cá nhân và ghi chú sau mỗi buổi.",
  "isActive": false
}
```

- POST/PUT dùng cùng các trường trên; PUT thay thế toàn bộ nội dung có thể sửa.
- `title`: trim, bắt buộc, 1–200 ký tự; `description`: trim, bắt buộc, 1–4000.
- `deliverables`: văn bản thuần, trim, bắt buộc, 1–4000; không nhận HTML hoặc URL file như một upload.
- Văn bản không chứa ký tự điều khiển, trừ xuống dòng/tab trong description/deliverables.
- `currency` trim và uppercase trước validation. Tiền dùng decimal, không làm tròn âm thầm.
- `durationMinutes`, `totalSessions`, `price`, `serviceType`, `currency`, `isActive`
  bắt buộc; thiếu hoặc null trả 400. POST không nhận `id`, `mentorId`, `mentorUserId`,
  `version`, hoặc timestamps; các trường server quản lý nếu được gửi lên phải bị từ chối.
- Không có ràng buộc unique title: hai dịch vụ cùng tên vẫn có ID riêng.
- POST không tự biến một inactive service khác thành active.

### DTO đọc/ghi thành công

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "mentorId": "22222222-2222-4222-8222-222222222222",
  "title": "Lộ trình backend .NET trong 4 buổi",
  "description": "Đánh giá mục tiêu, lập lộ trình và review tiến độ.",
  "serviceType": "Tier3_Roadmap",
  "durationMinutes": 60,
  "price": 2000000,
  "currency": "VND",
  "totalSessions": 4,
  "deliverables": "Lộ trình cá nhân và ghi chú sau mỗi buổi.",
  "isActive": false,
  "version": 1,
  "createdAtUtc": "2026-10-16T03:00:00Z",
  "updatedAtUtc": null
}
```

Public DTO không trả `mentorUserId` hoặc dữ liệu riêng tư của Profiles/Identity.
GET chi tiết và response POST/PUT có strong `ETag: "1"` theo version hiện tại.
Owner lấy version của inactive service từ DTO trong `/services/me`.
PUT/DELETE bắt buộc `If-Match: "{version}"`: thiếu trả 428, sai định dạng trả 400,
khác phiên bản hiện tại trả 412. Không nhận `*` hoặc weak ETag. Kiểm tra và ghi
trong cùng transaction; không có cập nhật thành công bị ghi đè âm thầm.

### Lỗi

| HTTP | Tình huống |
|---|---|
| 400 | Body/query không hợp lệ; currency/tier/giá/số buổi sai |
| 401 | Thiếu JWT hoặc session không hợp lệ |
| 403 | Có JWT nhưng không có role Mentor |
| 404 | Không tìm thấy tài nguyên được phép truy cập |
| 409 | `Catalog.MentorProfileRequired`: chưa có hồ sơ Mentor cho user hiện tại |
| 412 | `Catalog.ServiceVersionMismatch`: version đã thay đổi |
| 428 | `Catalog.ServiceVersionRequired`: thiếu If-Match |
| 503 | `Catalog.MentorDirectoryUnavailable`: chưa nối được nguồn Profiles đáng tin cậy |

Path ID không có dạng UUID không khớp route `:guid`, trả 404; UUID rỗng nhưng đúng
định dạng không xác định tài nguyên hợp lệ, cũng trả 404.

## 3. Ranh giới Profiles và Catalog

- Không reference project module khác và không foreign key liên schema.
- Catalog Application khai báo port tra cứu Mentor của user hiện tại. Adapter tại
  composition root gọi Application contract công khai của Profiles hoặc HTTP theo ADR 0002.
  Không đọc trực tiếp bảng Profiles trong Catalog.
- Catalog lấy `mentorUserId` từ `ICurrentUser.UserId`; `mentorId` được port trả về từ
  `MentorProfile.UserId`. Không giả định hai ID bằng nhau và không cho client tự gán mentorId.
- Chưa có adapter thì ghi dịch vụ trả 503; không sinh profile giả hoặc chấp nhận
  bất kỳ UUID do client gửi. Profile chưa tồn tại là 409 để web hướng dẫn tạo hồ sơ.
- Thay đổi quyền sở hữu một service không được hỗ trợ. Mentor bị vô hiệu hóa ở
  Identity không thể dùng JWT cũ để ghi. Khi Booking nhận đơn mới, Booking còn phải
  kiểm tra hồ sơ Mentor đủ điều kiện/nhận học viên theo contract Profiles.
- Public danh sách Catalog chỉ thể hiện trạng thái service; không khẳng định Mentor
  đã verified, đang nhận học viên hoặc có slot trống.

## 4. Snapshot bàn giao cho Booking

Catalog cung cấp query Application chỉ đọc cho integration adapter, không phải
endpoint công khai bổ sung. Query trả service **active, chưa xóa** cùng version,
và trusted `mentorUserId`. Booking không tin các trường giá/mentor/số buổi từ body client.

Booking lưu bản sao bất biến tại thời điểm tạo booking PendingPayment:

| Trường snapshot | Ý nghĩa |
|---|---|
| `serviceId`, `serviceVersion` | Nguồn dữ liệu và phiên bản đã mua |
| `mentorId`, `mentorUserId` | Profile ID để hiển thị; Identity user ID để kiểm tra quyền |
| `serviceTitle`, `serviceDescription`, `serviceType`, `deliverables` | Nội dung cam kết khi mua |
| `price`, `currency` | Tổng giá gói; là nguồn cho tổng tiền Booking và Payments |
| `durationMinutes`, `totalSessions` | Thời lượng mỗi buổi và tổng số buổi đã mua |
| `capturedAtUtc` | Thời điểm lấy snapshot |

Ví dụ gói 4 buổi, 60 phút/buổi, giá 2.000.000 VND: tổng tiền booking là
**2.000.000 VND**, không phải 8.000.000. Booking tạo 4 booking-session có UUID riêng
và số thứ tự 1–4; Learning về sau dùng `bookingSessionId`, không gộp cả gói thành một buổi.

Giá, nội dung, trạng thái hoặc soft-delete sau đó không sửa booking cũ. Một snapshot
active hợp lệ đã đọc có thể được mua theo đúng version đó dù Catalog thay đổi ngay sau
lần đọc; không hứa transaction nguyên tử xuyên hai module. Snapshot phải được lưu
cùng transaction tạo booking và các session/hold tương ứng, trước khi yêu cầu thanh toán.
Nếu không lấy được snapshot thì chưa tạo booking, không dùng giá cache không có version.

Các quyết định thuộc ND-11 vẫn cần chốt riêng: chọn giữ toàn bộ slot của gói ngay
khi đặt hay lên lịch dần, thời hạn/điều kiện đổi lịch và hủy từng buổi, trạng thái cả
gói, cùng phí/hoàn tiền. Contract này không biến request Booking cũ có một `slotId`
thành contract đủ dùng cho gói nhiều buổi; nguyendat cần cập nhật ND-11 trước khi code Booking.

## 5. Điều kiện bắt đầu nhánh triển khai

- [ ] vuhaidang duyệt bảng quyết định và sáu API.
- [ ] nguyendat xác nhận profile ID/user ID, port Profiles, snapshot và gói nhiều buổi.
- [ ] Thống nhất các giới hạn đề xuất: giá, 52 buổi, thời lượng 15–240 phút.
- [ ] Ghi nhận review trên PR này; sửa contract và merge về develop trước nhánh feature.

Khi triển khai: migration riêng `catalog`, lịch sử migrations trong `catalog`,
mọi configuration ignore DomainEvents; test riêng cho MentorServices. Checks cần
bao phủ quyền sở hữu, public không thấy inactive/deleted, FluentValidation, giá hai
currency, gói nhiều buổi, concurrency/If-Match, snapshot không đổi sau cập nhật/xóa,
không reference module hoặc FK liên schema. Chưa đánh dấu TESTED các API chưa tồn tại.
