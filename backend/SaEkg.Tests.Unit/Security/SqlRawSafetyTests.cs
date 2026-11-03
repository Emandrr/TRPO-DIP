using FluentAssertions;
using Xunit;

namespace SaEkg.Tests.Unit.Security;

public class SqlRawSafetyTests
{
    [Fact]
    public void Source_has_no_interpolated_FromSqlRaw()
    {
        var root = FindBackendRoot();
        var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !p.Contains($"{Path.DirectorySeparatorChar}SaEkg.Tests."))
            .ToList();

        files.Should().NotBeEmpty();
        var offenders = new List<string>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            if (text.Contains("FromSqlRaw($") || text.Contains("ExecuteSqlRaw($") || text.Contains("ExecuteSqlInterpolated($"))
            {
                offenders.Add(file);
            }
        }

        offenders.Should().BeEmpty("SQL must use parameters, not string interpolation");
    }

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SaEkg.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Backend root not found");
    }
}
