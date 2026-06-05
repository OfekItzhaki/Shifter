using FluentAssertions;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Domain.Spaces;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Integration;

public class YokneamCompanySchedulingFlowTests
{
    private static AppDbContext CreateDb()
    {
        AppDbContext.ConfigurationAssembly = typeof(SolverPayloadNormalizer).Assembly;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ReplaceService<IModelCacheKeyFactory, ConfigurationAwareModelCacheKeyFactory>()
            .Options;
        return new AppDbContext(options);
    }

    private static SolverPayloadNormalizer CreateNormalizer(AppDbContext db)
    {
        var cumulativeTracker = Substitute.For<ICumulativeTracker>();
        cumulativeTracker.GetForSolverPayloadAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<CumulativeTrackingDto>()));

        return new SolverPayloadNormalizer(
            db,
            Substitute.For<ILogger<SolverPayloadNormalizer>>(),
            cumulativeTracker);
    }

    [Fact]
    public async Task YokneamCompanyFlow_BuildsShifterPayloadWithRolesQualificationsAndAbsences()
    {
        var db = CreateDb();
        var seed = await SeedYokneamCompanyAsync(db);
        var normalizer = CreateNormalizer(db);

        var payload = await normalizer.BuildAsync(
            seed.SpaceId,
            runId: Guid.NewGuid(),
            triggerMode: "standard",
            baselineVersionId: null,
            groupId: seed.GroupId,
            startTime: seed.PlanningStart,
            ct: CancellationToken.None);

        payload.SpaceId.Should().Be(seed.SpaceId.ToString());
        payload.Locale.Should().Be("he");
        payload.People.Should().HaveCount(seed.ActivePersonIds.Count - seed.InitialAbsentPersonIds.Count);
        payload.People.Select(p => p.PersonId)
            .Should().NotIntersectWith(seed.InitialAbsentPersonIds.Select(id => id.ToString()),
                "soldiers abroad, sick, or at an event must not be eligible for the first run");

        var commander = payload.People.Single(p => p.PersonId == seed.PersonIds["אופק יצחקי"].ToString());
        commander.RoleIds.Should().Contain(seed.RoleIds["מ\"מ"].ToString());
        commander.QualificationIds.Should().Contain(["מפקד סיור", "מפקד כיתת כוננות", "מפקד יזומה"]);

        var driver = payload.People.Single(p => p.PersonId == seed.PersonIds["יוסי לוי"].ToString());
        driver.QualificationIds.Should().Contain("נהג סיור");

        var taskNames = payload.TaskSlots.Select(s => s.TaskTypeName).Distinct().ToList();
        taskNames.Should().Contain(["סיור יוקנעם", "חמ\"ל", "כיתת כוננות", "רחפן", "יזומה"]);

        payload.TaskSlots.Should().OnlyContain(s => s.QualificationRequirements != null,
            "the Python Shifter service expects qualification_requirements to always be a list");

        payload.TaskSlots.Where(s => s.TaskTypeName == "סיור יוקנעם")
            .Should().OnlyContain(s => s.QualificationRequirements!.Any(q => q.QualificationName == "נהג סיור" && q.Mandatory));

        payload.TaskSlots.Where(s => s.TaskTypeName == "חמ\"ל")
            .Should().OnlyContain(s =>
                s.QualificationRequirements!.Any(q => q.QualificationName == "סמב\"צ חמל" && q.Mandatory)
                && s.QualificationRequirements!.Any(q => q.QualificationName == "מפקד חמל" && q.Mandatory));

        payload.PresenceWindows.Should().Contain(p =>
            p.PersonId == seed.PersonIds["שלום"].ToString()
            && p.State == "at_home"
            && p.StartsAt == seed.PlanningStart.AddHours(12).ToString("o"));
    }

    [Fact]
    public async Task YokneamCompanyFlow_EvolvingAbsenceRemovesSoldierFromNextShifterRun()
    {
        var db = CreateDb();
        var seed = await SeedYokneamCompanyAsync(db);
        var normalizer = CreateNormalizer(db);

        var firstPayload = await normalizer.BuildAsync(
            seed.SpaceId,
            runId: Guid.NewGuid(),
            triggerMode: "standard",
            baselineVersionId: null,
            groupId: seed.GroupId,
            startTime: seed.PlanningStart,
            ct: CancellationToken.None);

        firstPayload.People.Select(p => p.PersonId)
            .Should().Contain(seed.PersonIds["יוסי לוי"].ToString());

        db.PresenceWindows.Add(PresenceWindow.CreateManual(
            seed.SpaceId,
            seed.PersonIds["יוסי לוי"],
            PresenceState.AtHome,
            seed.PlanningStart.AddDays(1),
            seed.PlanningStart.AddDays(3),
            "אירוע משפחתי חדש"));
        await db.SaveChangesAsync();

        var followUpPayload = await normalizer.BuildAsync(
            seed.SpaceId,
            runId: Guid.NewGuid(),
            triggerMode: "regeneration",
            baselineVersionId: null,
            groupId: seed.GroupId,
            startTime: seed.PlanningStart,
            ct: CancellationToken.None);

        followUpPayload.People.Select(p => p.PersonId)
            .Should().NotContain(seed.PersonIds["יוסי לוי"].ToString(),
                "new absences must be reflected before Shifter creates the next draft");

        followUpPayload.PresenceWindows.Should().Contain(p =>
            p.PersonId == seed.PersonIds["יוסי לוי"].ToString()
            && p.State == "at_home"
            && p.StartsAt == seed.PlanningStart.AddDays(1).ToString("o"));
    }

    private static async Task<YokneamSeed> SeedYokneamCompanyAsync(AppDbContext db)
    {
        var ownerId = Guid.NewGuid();
        var planningStart = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc);

        var space = Space.Create("פלוגת יוקנעם", ownerId, locale: "he");
        db.Spaces.Add(space);

        var group = Group.Create(space.Id, null, "פלוגת יוקנעם", "תרחיש בדיקה מלא", ownerId);
        group.UpdateSettings(7, planningStart);
        group.SetMinRestBetweenShifts(8);
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var roleNames = new[] { "מ\"מ", "סמל", "מ\"כ", "חייל" };
        var roles = roleNames.ToDictionary(
            name => name,
            name => SpaceRole.CreateForGroup(space.Id, group.Id, name, ownerId));
        db.SpaceRoles.AddRange(roles.Values);

        var qualifications = new[]
        {
            "מפקד סיור",
            "מפקד כיתת כוננות",
            "נהג סיור",
            "מפעיל רחפן",
            "סמב\"צ חמל",
            "מפקד חמל",
            "מפקד יזומה"
        };
        db.GroupQualifications.AddRange(qualifications.Select(q => GroupQualification.Create(space.Id, group.Id, q, ownerId)));

        var people = new Dictionary<string, Person>();
        foreach (var name in new[]
        {
            "אופק יצחקי",
            "יוסי לוי",
            "יעקוב",
            "שלום",
            "נתן",
            "רון אברהם",
            "דניאל כהן",
            "מאיר",
            "גוזה שטרכטר",
            "איציק",
            "מנחם",
            "נועם"
        })
        {
            var person = Person.Create(space.Id, name);
            people[name] = person;
            db.People.Add(person);
            db.GroupMemberships.Add(GroupMembership.Create(space.Id, group.Id, person.Id));
        }

        await db.SaveChangesAsync();

        AssignRole(db, space.Id, group.Id, people["אופק יצחקי"].Id, roles["מ\"מ"].Id);
        AssignRole(db, space.Id, group.Id, people["שלום"].Id, roles["סמל"].Id);
        AssignRole(db, space.Id, group.Id, people["נתן"].Id, roles["מ\"כ"].Id);
        AssignRole(db, space.Id, group.Id, people["רון אברהם"].Id, roles["מ\"כ"].Id);

        AddQualifications(db, space.Id, people["אופק יצחקי"].Id,
            "מפקד סיור", "מפקד כיתת כוננות", "מפקד יזומה");
        AddQualifications(db, space.Id, people["יוסי לוי"].Id,
            "נהג סיור", "מפעיל רחפן");
        AddQualifications(db, space.Id, people["יעקוב"].Id,
            "נהג סיור", "סמב\"צ חמל");
        AddQualifications(db, space.Id, people["שלום"].Id,
            "מפקד חמל", "מפקד סיור");
        AddQualifications(db, space.Id, people["נתן"].Id,
            "מפקד כיתת כוננות", "מפקד יזומה");
        AddQualifications(db, space.Id, people["רון אברהם"].Id,
            "מפקד חמל", "סמב\"צ חמל");
        AddQualifications(db, space.Id, people["דניאל כהן"].Id,
            "נהג סיור");
        AddQualifications(db, space.Id, people["מאיר"].Id,
            "מפעיל רחפן");

        var initiallyAbsent = new[]
        {
            people["גוזה שטרכטר"].Id,
            people["איציק"].Id,
            people["שלום"].Id
        };

        db.PresenceWindows.AddRange(
            PresenceWindow.CreateManual(
                space.Id,
                people["גוזה שטרכטר"].Id,
                PresenceState.AtHome,
                planningStart,
                planningStart.AddDays(5),
                "חו\"ל"),
            PresenceWindow.CreateManual(
                space.Id,
                people["איציק"].Id,
                PresenceState.AtHome,
                planningStart.AddDays(2),
                planningStart.AddDays(4),
                "ימי מחלה"),
            PresenceWindow.CreateManual(
                space.Id,
                people["שלום"].Id,
                PresenceState.AtHome,
                planningStart.AddHours(12),
                planningStart.AddDays(1),
                "אירוע פלוגתי"));

        db.GroupTasks.AddRange(
            GroupTask.Create(
                space.Id,
                group.Id,
                "סיור יוקנעם",
                planningStart,
                planningStart.AddDays(7),
                shiftDurationMinutes: 480,
                requiredHeadcount: 2,
                burdenLevel: TaskBurdenLevel.Hard,
                allowsDoubleShift: false,
                allowsOverlap: false,
                createdByUserId: ownerId,
                qualificationRequirements:
                [
                    new QualificationRequirement("נהג סיור", 1, true),
                    new QualificationRequirement("מפקד סיור", 1, true)
                ]),
            GroupTask.Create(
                space.Id,
                group.Id,
                "חמ\"ל",
                planningStart,
                planningStart.AddDays(7),
                shiftDurationMinutes: 480,
                requiredHeadcount: 2,
                burdenLevel: TaskBurdenLevel.Normal,
                allowsDoubleShift: false,
                allowsOverlap: false,
                createdByUserId: ownerId,
                qualificationRequirements:
                [
                    new QualificationRequirement("סמב\"צ חמל", 1, true),
                    new QualificationRequirement("מפקד חמל", 1, true)
                ]),
            GroupTask.Create(
                space.Id,
                group.Id,
                "כיתת כוננות",
                planningStart,
                planningStart.AddDays(7),
                shiftDurationMinutes: 720,
                requiredHeadcount: 3,
                burdenLevel: TaskBurdenLevel.Hard,
                allowsDoubleShift: false,
                allowsOverlap: false,
                createdByUserId: ownerId,
                qualificationRequirements:
                [
                    new QualificationRequirement("מפקד כיתת כוננות", 1, true)
                ]),
            GroupTask.Create(
                space.Id,
                group.Id,
                "רחפן",
                planningStart.AddHours(8),
                planningStart.AddDays(7).AddHours(8),
                shiftDurationMinutes: 720,
                requiredHeadcount: 1,
                burdenLevel: TaskBurdenLevel.Normal,
                allowsDoubleShift: false,
                allowsOverlap: false,
                createdByUserId: ownerId,
                qualificationRequirements:
                [
                    new QualificationRequirement("מפעיל רחפן", 1, true)
                ]),
            GroupTask.Create(
                space.Id,
                group.Id,
                "יזומה",
                planningStart.AddHours(18),
                planningStart.AddDays(7).AddHours(18),
                shiftDurationMinutes: 720,
                requiredHeadcount: 2,
                burdenLevel: TaskBurdenLevel.Hard,
                allowsDoubleShift: false,
                allowsOverlap: false,
                createdByUserId: ownerId,
                qualificationRequirements:
                [
                    new QualificationRequirement("מפקד יזומה", 1, true)
                ]));

        await db.SaveChangesAsync();

        return new YokneamSeed(
            space.Id,
            group.Id,
            planningStart,
            people.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Id),
            roles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Id),
            people.Values.Select(p => p.Id).ToHashSet(),
            initiallyAbsent.ToHashSet());
    }

    private static void AssignRole(AppDbContext db, Guid spaceId, Guid groupId, Guid personId, Guid roleId) =>
        db.PersonRoleAssignments.Add(PersonRoleAssignment.Create(spaceId, personId, roleId, groupId));

    private static void AddQualifications(AppDbContext db, Guid spaceId, Guid personId, params string[] qualifications) =>
        db.PersonQualifications.AddRange(qualifications.Select(q => PersonQualification.Create(spaceId, personId, q)));

    private sealed record YokneamSeed(
        Guid SpaceId,
        Guid GroupId,
        DateTime PlanningStart,
        Dictionary<string, Guid> PersonIds,
        Dictionary<string, Guid> RoleIds,
        HashSet<Guid> ActivePersonIds,
        HashSet<Guid> InitialAbsentPersonIds);

    private sealed class ConfigurationAwareModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) =>
            (context.GetType(), AppDbContext.ConfigurationAssembly?.FullName, designTime);
    }
}
