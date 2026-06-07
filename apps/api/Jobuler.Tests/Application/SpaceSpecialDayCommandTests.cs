using FluentAssertions;
using Jobuler.Application.Spaces.SpecialDays;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Jobuler.Tests.Application;

public class SpaceSpecialDayCommandTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Create_ThenList_ReturnsSpecialDay()
    {
        await using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var date = new DateOnly(2026, 9, 22);

        var create = new CreateSpaceSpecialDayCommandHandler(db);
        var id = await create.Handle(new CreateSpaceSpecialDayCommand(
            spaceId,
            date,
            "Rosh Hashanah",
            SpaceSpecialDayKind.Holiday,
            2.5m,
            RequiresCoverage: true), CancellationToken.None);

        var list = new ListSpaceSpecialDaysQueryHandler(db);
        var result = await list.Handle(new ListSpaceSpecialDaysQuery(spaceId), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(id);
        result[0].Date.Should().Be(date);
        result[0].Name.Should().Be("Rosh Hashanah");
        result[0].Kind.Should().Be(SpaceSpecialDayKind.Holiday);
        result[0].HomeLeaveWeightMultiplier.Should().Be(2.5m);
        result[0].RequiresCoverage.Should().BeTrue();
    }

    [Fact]
    public async Task Create_DuplicateNameOnSameDate_IsRejected()
    {
        await using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var date = new DateOnly(2026, 9, 22);
        var handler = new CreateSpaceSpecialDayCommandHandler(db);

        await handler.Handle(new CreateSpaceSpecialDayCommand(
            spaceId,
            date,
            "Holiday",
            SpaceSpecialDayKind.Holiday,
            1.5m,
            RequiresCoverage: true), CancellationToken.None);

        var act = () => handler.Handle(new CreateSpaceSpecialDayCommand(
            spaceId,
            date,
            "Holiday",
            SpaceSpecialDayKind.Custom,
            1.5m,
            RequiresCoverage: true), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A special day with this name already exists on this date.");
    }
}
