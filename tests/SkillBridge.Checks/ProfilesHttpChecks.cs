namespace SkillBridge.Checks;

internal static class ProfilesHttpChecks
{
    public static async Task RunAsync(HttpClient client, string menteeAccessToken)
    {
        const string mentorsMe = "/api/v1/profiles/mentors/me";
        const string menteesMe = "/api/v1/profiles/mentees/me";

        using var mentorRegistered = await DatabaseChecks.SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/register", 201,
            new { email = "mentor@checks.test", password = "Password123!", fullName = "Mentor", role = "Mentor" });
        var mentor = await DatabaseChecks.LoginAsync(client, "mentor@checks.test");

        using var anonymousMentor = await DatabaseChecks.SendAsync(client, HttpMethod.Get, mentorsMe, 401);
        using var wrongMentorRole = await DatabaseChecks.SendAsync(client, HttpMethod.Get, mentorsMe, 403, bearer: menteeAccessToken);
        using var missingMentor = await DatabaseChecks.SendAsync(client, HttpMethod.Get, mentorsMe, 404, bearer: mentor.AccessToken);
        using var invalidMentor = await DatabaseChecks.SendAsync(client, HttpMethod.Put, mentorsMe, 400, new
        {
            headline = "Mentor",
            bio = "Bio",
            hourlyRate = 501,
            currency = "USD",
            timezone = "Asia/Ho_Chi_Minh",
            isAcceptingMentees = true
        }, mentor.AccessToken);
        using var mentorCreated = await DatabaseChecks.SendAsync(client, HttpMethod.Put, mentorsMe, 204, new
        {
            headline = "Backend Mentor",
            bio = "Hướng dẫn phát triển backend .NET",
            hourlyRate = 500000,
            currency = "VND",
            timezone = "Asia/Ho_Chi_Minh",
            isAcceptingMentees = true,
            avatarUrl = "https://example.com/mentor.png"
        }, mentor.AccessToken);
        using var mentorRead = await DatabaseChecks.SendAsync(client, HttpMethod.Get, mentorsMe, 200, bearer: mentor.AccessToken);
        var mentorJson = await DatabaseChecks.JsonAsync(mentorRead);
        var mentorId = mentorJson.GetProperty("id").GetGuid();
        Program.Check(mentorJson.GetProperty("headline").GetString() == "Backend Mentor" &&
            mentorJson.GetProperty("skills").GetArrayLength() == 0,
            "Mentor profile response does not match its contract.");
        using var mentorUpdated = await DatabaseChecks.SendAsync(client, HttpMethod.Put, mentorsMe, 204, new
        {
            headline = "Senior Backend Mentor",
            bio = "Hướng dẫn phát triển backend .NET",
            hourlyRate = 450,
            currency = "USD",
            timezone = "Asia/Ho_Chi_Minh",
            isAcceptingMentees = false
        }, mentor.AccessToken);
        using var mentorReadAgain = await DatabaseChecks.SendAsync(client, HttpMethod.Get, mentorsMe, 200, bearer: mentor.AccessToken);
        var mentorAgainJson = await DatabaseChecks.JsonAsync(mentorReadAgain);
        Program.Check(mentorAgainJson.GetProperty("id").GetGuid() == mentorId &&
            mentorAgainJson.GetProperty("headline").GetString() == "Senior Backend Mentor",
            "PUT mentor profile did not update the existing profile idempotently.");

        using var wrongMenteeRole = await DatabaseChecks.SendAsync(client, HttpMethod.Get, menteesMe, 403, bearer: mentor.AccessToken);
        using var missingMentee = await DatabaseChecks.SendAsync(client, HttpMethod.Get, menteesMe, 404, bearer: menteeAccessToken);
        using var invalidMentee = await DatabaseChecks.SendAsync(client, HttpMethod.Put, menteesMe, 400, new
        {
            careerGoal = "Backend Developer",
            currentLevel = "Beginner",
            timezone = "Asia/Ho_Chi_Minh",
            budgetMin = 600000,
            budgetMax = 500000,
            currency = "VND"
        }, menteeAccessToken);
        using var overflowingMenteeBudget = await DatabaseChecks.SendAsync(client, HttpMethod.Put, menteesMe, 400, new
        {
            careerGoal = "Backend Developer",
            currentLevel = "Beginner",
            timezone = "Asia/Ho_Chi_Minh",
            budgetMin = 0,
            budgetMax = 10_000_000_000_000_000m,
            currency = "VND"
        }, menteeAccessToken);
        using var menteeCreated = await DatabaseChecks.SendAsync(client, HttpMethod.Put, menteesMe, 204, new
        {
            careerGoal = "Trở thành Backend Developer",
            currentLevel = "Beginner",
            timezone = "Asia/Ho_Chi_Minh",
            budgetMin = 100000,
            budgetMax = 500000,
            currency = "VND",
            avatarUrl = "https://example.com/mentee.png"
        }, menteeAccessToken);
        using var menteeRead = await DatabaseChecks.SendAsync(client, HttpMethod.Get, menteesMe, 200, bearer: menteeAccessToken);
        var menteeJson = await DatabaseChecks.JsonAsync(menteeRead);
        var menteeId = menteeJson.GetProperty("id").GetGuid();
        Program.Check(menteeJson.GetProperty("careerGoal").GetString() == "Trở thành Backend Developer",
            "Mentee profile response does not match its contract.");
        using var menteeUpdated = await DatabaseChecks.SendAsync(client, HttpMethod.Put, menteesMe, 204, new
        {
            careerGoal = "Trở thành Senior Backend Developer",
            currentLevel = "Intermediate",
            timezone = "Asia/Ho_Chi_Minh",
            budgetMin = 200000,
            budgetMax = 700000,
            currency = "VND"
        }, menteeAccessToken);
        using var menteeReadAgain = await DatabaseChecks.SendAsync(client, HttpMethod.Get, menteesMe, 200, bearer: menteeAccessToken);
        var menteeAgainJson = await DatabaseChecks.JsonAsync(menteeReadAgain);
        Program.Check(menteeAgainJson.GetProperty("id").GetGuid() == menteeId &&
            menteeAgainJson.GetProperty("currentLevel").GetString() == "Intermediate",
            "PUT mentee profile did not update the existing profile idempotently.");
    }
}
