using FluentAssertions;
using Xunit;

namespace Jobuler.Tests.Application;

public class SelfServiceReviewRlsMigrationTests
{
    [Fact]
    public void Migrations_EnableRls_ForAllSelfServiceReviewTables()
    {
        var migrations = ReadMigrationFiles();

        migrations.Should().Contain("ALTER TABLE shift_absence_reports ENABLE ROW LEVEL SECURITY");
        migrations.Should().Contain("ALTER TABLE shift_change_requests ENABLE ROW LEVEL SECURITY");
        migrations.Should().Contain("ALTER TABLE special_leave_requests ENABLE ROW LEVEL SECURITY");
    }

    private static string ReadMigrationFiles()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var migrationsDirectory = Path.Combine(
                current.FullName,
                "Jobuler.Application",
                "Persistence",
                "Migrations");

            if (Directory.Exists(migrationsDirectory))
            {
                return string.Join(
                    Environment.NewLine,
                    Directory.GetFiles(migrationsDirectory, "*.cs")
                        .Where(path => !path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
                        .Select(File.ReadAllText));
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Jobuler.Application/Persistence/Migrations.");
    }
}
