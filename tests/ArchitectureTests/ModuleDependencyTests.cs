using System.Xml.Linq;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.ArchitectureTests;

public sealed class ModuleDependencyTests
{
    [Fact]
    public void Modules_depend_only_on_the_shared_BuildingBlocks_project()
    {
        var modulesRoot = Path.Combine(RepositoryRoot(), "src", "Modules");
        foreach (var module in Directory.EnumerateDirectories(modulesRoot))
        {
            foreach (var project in Directory.EnumerateFiles(module, "*.csproj", SearchOption.AllDirectories))
            {
                var forbiddenReferences = References(project).Where(reference =>
                    !string.Equals(reference,
                        Path.Combine(RepositoryRoot(), "src", "BuildingBlocks", "SkillBridge.BuildingBlocks.csproj"),
                        StringComparison.OrdinalIgnoreCase));
                Assert.True(!forbiddenReferences.Any(),
                    $"{Path.GetFileName(project)} references a project outside BuildingBlocks: {string.Join(", ", forbiddenReferences)}");
            }
        }
    }

    [Fact]
    public void BuildingBlocks_does_not_install_host_authentication_or_a_specific_database_provider()
    {
        var project = Path.Combine(RepositoryRoot(), "src", "BuildingBlocks", "SkillBridge.BuildingBlocks.csproj");
        var packages = XDocument.Load(project).Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")!.Value);

        Assert.DoesNotContain(packages, package =>
            package == "Microsoft.AspNetCore.Authentication.JwtBearer" ||
            package.StartsWith("Npgsql", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildingBlocks_does_not_depend_on_API_or_module_business_types()
    {
        var root = RepositoryRoot();
        var project = Path.Combine(root, "src", "BuildingBlocks", "SkillBridge.BuildingBlocks.csproj");
        Assert.DoesNotContain(References(project), reference =>
            IsInside(reference, Path.Combine(root, "src", "Modules")) ||
            IsInside(reference, Path.Combine(root, "src", "Api")));
        Assert.DoesNotContain(typeof(Result).Assembly.GetReferencedAssemblies(), assembly =>
            assembly.Name == "SkillBridge.Api" || assembly.Name!.StartsWith("SkillBridge.Modules.", StringComparison.Ordinal));
    }

    private static IEnumerable<string> References(string project) => XDocument.Load(project)
        .Descendants("ProjectReference")
        .Select(reference => reference.Attribute("Include")!.Value.Replace('\\', Path.DirectorySeparatorChar))
        .Select(reference => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, reference)));

    private static bool IsInside(string path, string directory) =>
        path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "SkillBridge.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot find the repository root.");
    }
}
