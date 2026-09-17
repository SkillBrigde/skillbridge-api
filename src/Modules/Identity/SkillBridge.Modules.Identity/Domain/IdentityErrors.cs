using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Identity.Domain;

public static class IdentityErrors
{
    public static readonly Error InvalidCredentials = Error.Unauthorized("Identity.InvalidCredentials", "Thông tin đăng nhập không hợp lệ.");
    public static readonly Error InvalidToken = Error.Unauthorized("Identity.InvalidToken", "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");
    public static readonly Error Locked = new("Identity.Locked", "Tài khoản tạm khóa. Vui lòng thử lại sau 15 phút.", ErrorType.Locked);
    public static readonly Error DuplicateEmail = Error.Conflict("Identity.DuplicateEmail", "Email đã được sử dụng.");
    public static readonly Error Replay = Error.Conflict("Identity.TokenReplay", "Token đã được sử dụng. Phiên đã bị thu hồi.");
    public static readonly Error UserNotFound = Error.NotFound("Identity.UserNotFound", "Không tìm thấy người dùng.");
    public static readonly Error SessionNotFound = Error.NotFound("Identity.SessionNotFound", "Không tìm thấy phiên đăng nhập.");
}
