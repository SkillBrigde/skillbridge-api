namespace SkillBridge.Api.Common;

public static class DotEnv
{
    /// <summary>
    /// Tự động tìm và nạp các biến môi trường từ file .env vào Environment Variables của tiến trình.
    /// File .env được tìm từ thư mục hiện tại và quét dần lên các thư mục cha.
    /// </summary>
    public static void Load(string fileName = ".env")
    {
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (currentDir is not null)
        {
            var envFilePath = Path.Combine(currentDir.FullName, fileName);
            if (File.Exists(envFilePath))
            {
                foreach (var line in File.ReadAllLines(envFilePath))
                {
                    var trimmed = line.Trim();

                    // Bỏ qua dòng trống hoặc dòng chú thích (bắt đầu bằng #)
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                    {
                        continue;
                    }

                    var parts = trimmed.Split('=', 2, StringSplitOptions.TrimEntries);
                    if (parts.Length == 2)
                    {
                        // Chỉ gán nếu biến môi trường chưa được set ở OS (cho phép OS override file .env)
                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(parts[0])))
                        {
                            Environment.SetEnvironmentVariable(parts[0], parts[1]);
                        }
                    }
                }

                break;
            }

            currentDir = currentDir.Parent;
        }
    }
}
