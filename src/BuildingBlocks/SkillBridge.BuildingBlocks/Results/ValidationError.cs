namespace SkillBridge.BuildingBlocks.Results;

public sealed record ValidationError(IDictionary<string, string[]> Errors)
    : Error("Validation.Failed", "Một hoặc nhiều trường dữ liệu không hợp lệ.", ErrorType.Validation);
