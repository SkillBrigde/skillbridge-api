using SkillBridge.BuildingBlocks.Domain;

namespace SkillBridge.Modules.Identity.Domain;

public sealed class User : AggregateRoot<Guid>
{
    // Private setter để bảo vệ dữ liệu, không cho phép gán bừa bãi từ bên ngoài
    public string Email { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public string Role { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    // Constructor rỗng bắt buộc cho ORM (EF Core) sau này
    private User() { }

    // Factory method để kiểm soát logic tạo mới một User hợp lệ
    public static User Create(string email, string fullName, string role)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            FullName = fullName.Trim(),
            Role = role,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        // Sau này khi có UserRegisteredDomainEvent, bạn có thể gọi:
        // user.RaiseDomainEvent(new UserRegisteredDomainEvent(user.Id, user.Email));

        return user;
    }
}
