using TupZak.Demo.Services;

namespace TupZak.Demo.Tests;

public class SkillCatalogTests
{
    private readonly SkillCatalog _catalog = new(Path.Combine(TestData.DataDirectory, "..", ".claude", "skills"));

    [Fact]
    public void Lists_delivery_route_skill_with_its_files()
    {
        var skill = Assert.Single(_catalog.All(), s => s.Name == "delivery-route");

        Assert.StartsWith("Wyznaczanie trasy dostawy", skill.Description);
        Assert.Equal("SKILL.md", skill.Files[0]);
        Assert.Contains("scripts/optimize_route.py", skill.Files);
        Assert.Contains("references/answer-format.md", skill.Files);
    }

    [Fact]
    public void Reads_skill_files()
    {
        Assert.Contains("name: delivery-route", _catalog.ReadFile("delivery-route", "SKILL.md"));
        Assert.Contains("permutations", _catalog.ReadFile("delivery-route", "scripts/optimize_route.py"));
    }

    [Theory]
    [InlineData("delivery-route", "../../README.md")]
    [InlineData("..", "README.md")]
    [InlineData("delivery-route", "/etc/hosts")]
    [InlineData("delivery-route", "missing.md")]
    public void Does_not_read_outside_skill_directory(string skill, string path)
    {
        Assert.Null(_catalog.ReadFile(skill, path));
    }
}
