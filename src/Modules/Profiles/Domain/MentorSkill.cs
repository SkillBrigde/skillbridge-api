using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Profiles.Domain;

public sealed class MentorSkill : AggregateRoot<Guid>
{
    private static readonly string[] ProficiencyLevels = ["Beginner", "Intermediate", "Advanced", "Expert"];

    private MentorSkill() { }

    public Guid MentorProfileId { get; private set; }
    public Guid SkillId { get; private set; }
    public string SkillName { get; private set; } = string.Empty;
    public string ProficiencyLevel { get; private set; } = string.Empty;
    public int YearsOfExperience { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<MentorSkill> Create(
        Guid mentorProfileId,
        Guid skillId,
        string? skillName,
        string? proficiencyLevel,
        int yearsOfExperience,
        DateTimeOffset now)
    {
        if (mentorProfileId == Guid.Empty || skillId == Guid.Empty)
        {
            return Error.Validation("Profiles.InvalidMentorSkill", "Mã hồ sơ Mentor và mã kỹ năng phải hợp lệ.");
        }

        if (!ProfileInput.IsValidText(skillName, 150))
        {
            return Error.Validation("Profiles.InvalidSkillName", "Tên kỹ năng là bắt buộc, tối đa 150 ký tự.");
        }

        var normalizedLevel = ProficiencyLevels.FirstOrDefault(level =>
            string.Equals(level, proficiencyLevel?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (normalizedLevel is null)
        {
            return Error.Validation(
                "Profiles.InvalidProficiencyLevel",
                "Trình độ phải là Beginner, Intermediate, Advanced hoặc Expert.");
        }

        if (yearsOfExperience < 0)
        {
            return Error.Validation("Profiles.InvalidYearsOfExperience", "Số năm kinh nghiệm không được âm.");
        }

        return new MentorSkill
        {
            Id = Guid.CreateVersion7(),
            MentorProfileId = mentorProfileId,
            SkillId = skillId,
            SkillName = skillName!.Trim(),
            ProficiencyLevel = normalizedLevel,
            YearsOfExperience = yearsOfExperience,
            CreatedAtUtc = now
        };
    }
}
