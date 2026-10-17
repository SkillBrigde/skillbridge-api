using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Scheduling.Application;

public static class AvailabilityRuleErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Scheduling.AvailabilityRuleNotFound", "Không tìm thấy quy tắc lịch rảnh của Mentor hiện tại.");

    public static readonly Error SettingsRequired = Error.Conflict(
        "Scheduling.SettingsRequired", "Hãy cấu hình cài đặt lịch trước khi quản lý quy tắc lịch rảnh.");

    public static readonly Error Overlap = Error.Conflict(
        "Scheduling.AvailabilityRuleOverlap", "Khung giờ bị chồng lấn với một quy tắc đang hoạt động trong cùng ngày.");
}
