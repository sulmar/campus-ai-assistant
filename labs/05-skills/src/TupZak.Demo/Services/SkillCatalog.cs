namespace TupZak.Demo.Services;

public sealed record SkillInfo(string Name, string Description, IReadOnlyList<string> Files);

/// <summary>
/// Podgląd Skills z katalogu .claude/skills (tego samego, z którego korzysta Claude Code) — tylko do odczytu,
/// żeby na zajęciach łatwo pokazać, co dokładnie dostaje agent: SKILL.md, referencje i skrypty.
/// </summary>
public sealed class SkillCatalog(string directory)
{
    public string Directory { get; } = Path.GetFullPath(directory);

    public IReadOnlyList<SkillInfo> All()
    {
        if (!System.IO.Directory.Exists(Directory)) return [];

        return System.IO.Directory.GetDirectories(Directory)
            .Where(d => File.Exists(Path.Combine(d, "SKILL.md")))
            .Order()
            .Select(d =>
            {
                var frontmatter = Frontmatter(File.ReadAllLines(Path.Combine(d, "SKILL.md")));
                var files = System.IO.Directory.GetFiles(d, "*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(d, f).Replace('\\', '/'))
                    .OrderBy(f => f == "SKILL.md" ? "" : f)
                    .ToList();
                return new SkillInfo(frontmatter.GetValueOrDefault("name", Path.GetFileName(d)), frontmatter.GetValueOrDefault("description", ""), files);
            })
            .ToList();
    }

    /// <summary>Treść pliku Skilla albo null, gdy plik nie istnieje lub ścieżka wychodzi poza katalog Skilla.</summary>
    public string? ReadFile(string skill, string path)
    {
        string root = Path.GetFullPath(Path.Combine(Directory, skill)) + Path.DirectorySeparatorChar;
        string file = Path.GetFullPath(Path.Combine(root, path));
        return file.StartsWith(root, StringComparison.Ordinal) && root.StartsWith(Directory + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(file)
            ? File.ReadAllText(file)
            : null;
    }

    private static Dictionary<string, string> Frontmatter(string[] lines)
    {
        var values = new Dictionary<string, string>();
        if (lines.Length == 0 || lines[0].Trim() != "---") return values;
        foreach (string line in lines.Skip(1).TakeWhile(l => l.Trim() != "---"))
        {
            int colon = line.IndexOf(':');
            if (colon > 0) values[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }
        return values;
    }
}
