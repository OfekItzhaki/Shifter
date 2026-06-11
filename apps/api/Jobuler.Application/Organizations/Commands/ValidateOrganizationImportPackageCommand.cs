using System.Text.Json;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Organizations.Commands;

public record ValidateOrganizationImportPackageCommand(string PackageJson)
    : IRequest<OrganizationImportValidationResult>;

public record OrganizationImportValidationResult(
    bool IsImportSafe,
    Guid? OrganizationId,
    string? OrganizationName,
    int SchemaVersion,
    OrganizationImportValidationCounts Counts,
    List<string> Conflicts,
    List<string> Warnings,
    List<string> Errors);

public record OrganizationImportValidationCounts(
    int Spaces,
    int Groups,
    int People,
    int SpaceMemberships,
    int GroupMemberships,
    int GroupTasks,
    int TaskTypes,
    int TaskSlots,
    int Constraints,
    int ScheduleRuns,
    int ScheduleVersions,
    int Assignments,
    int SpaceSelfServiceDefaults,
    int SpaceSpecialDays,
    int SelfServiceConfigs,
    int SchedulingCycles,
    int ShiftTemplates,
    int ShiftSlots,
    int ShiftRequests,
    int ShiftAttendanceRecords,
    int ShiftAbsenceReports,
    int ShiftChangeRequests,
    int WaitlistEntries,
    int SwapRequests,
    int SpecialLeaveRequests);

public class ValidateOrganizationImportPackageCommandHandler
    : IRequestHandler<ValidateOrganizationImportPackageCommand, OrganizationImportValidationResult>
{
    private readonly AppDbContext _db;

    public ValidateOrganizationImportPackageCommandHandler(AppDbContext db) => _db = db;

    public async Task<OrganizationImportValidationResult> Handle(
        ValidateOrganizationImportPackageCommand request,
        CancellationToken ct)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var conflicts = new List<string>();
        Guid? organizationId = null;
        string? organizationName = null;
        var schemaVersion = 0;
        var counts = new OrganizationImportValidationCounts(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(request.PackageJson);
        }
        catch (JsonException ex)
        {
            return new OrganizationImportValidationResult(
                false,
                null,
                null,
                0,
                counts,
                conflicts,
                warnings,
                [$"Invalid JSON package: {ex.Message}"]);
        }

        using (document)
        {
            var root = document.RootElement;
            if (!TryGetObject(root, "manifest", out var manifest))
                errors.Add("Missing manifest object.");
            if (!TryGetObject(root, "data", out var data))
                errors.Add("Missing data object.");

            schemaVersion = TryGetInt(root, "schemaVersion") ?? 0;
            if (schemaVersion != 1)
                errors.Add($"Unsupported schemaVersion '{schemaVersion}'.");

            if (manifest.ValueKind == JsonValueKind.Object)
            {
                organizationId = TryGetGuid(manifest, "organizationId");
                organizationName = TryGetString(manifest, "organizationName");

                if (!organizationId.HasValue)
                    errors.Add("Manifest organizationId is missing or invalid.");
                if (string.IsNullOrWhiteSpace(organizationName))
                    warnings.Add("Manifest organization name is empty.");
            }

            if (data.ValueKind == JsonValueKind.Object)
            {
                var spaces = GetArray(data, "spaces");
                var groups = GetArray(data, "groups");
                var people = GetArray(data, "people");
                var spaceMemberships = GetArray(data, "spaceMemberships");
                var groupMemberships = GetArray(data, "groupMemberships");
                var groupTasks = GetArray(data, "groupTasks");
                var taskTypes = GetArray(data, "taskTypes");
                var taskSlots = GetArray(data, "taskSlots");
                var constraints = GetArray(data, "constraints");
                var scheduleRuns = GetArray(data, "scheduleRuns");
                var scheduleVersions = GetArray(data, "scheduleVersions");
                var assignments = GetArray(data, "assignments");
                var spaceSelfServiceDefaults = GetArray(data, "spaceSelfServiceDefaults");
                var spaceSpecialDays = GetArray(data, "spaceSpecialDays");
                var selfServiceConfigs = GetArray(data, "selfServiceConfigs");
                var schedulingCycles = GetArray(data, "schedulingCycles");
                var shiftTemplates = GetArray(data, "shiftTemplates");
                var shiftSlots = GetArray(data, "shiftSlots");
                var shiftRequests = GetArray(data, "shiftRequests");
                var shiftAttendanceRecords = GetArray(data, "shiftAttendanceRecords");
                var shiftAbsenceReports = GetArray(data, "shiftAbsenceReports");
                var shiftChangeRequests = GetArray(data, "shiftChangeRequests");
                var waitlistEntries = GetArray(data, "waitlistEntries");
                var swapRequests = GetArray(data, "swapRequests");
                var specialLeaveRequests = GetArray(data, "specialLeaveRequests");

                counts = new OrganizationImportValidationCounts(
                    spaces.Count,
                    groups.Count,
                    people.Count,
                    spaceMemberships.Count,
                    groupMemberships.Count,
                    groupTasks.Count,
                    taskTypes.Count,
                    taskSlots.Count,
                    constraints.Count,
                    scheduleRuns.Count,
                    scheduleVersions.Count,
                    assignments.Count,
                    spaceSelfServiceDefaults.Count,
                    spaceSpecialDays.Count,
                    selfServiceConfigs.Count,
                    schedulingCycles.Count,
                    shiftTemplates.Count,
                    shiftSlots.Count,
                    shiftRequests.Count,
                    shiftAttendanceRecords.Count,
                    shiftAbsenceReports.Count,
                    shiftChangeRequests.Count,
                    waitlistEntries.Count,
                    swapRequests.Count,
                    specialLeaveRequests.Count);

                ValidateManifestCounts(manifest, counts, errors);

                var organizationObjectId = TryGetObject(data, "organization", out var organization)
                    ? TryGetGuid(organization, "id")
                    : null;
                if (organizationId.HasValue && organizationObjectId.HasValue && organizationObjectId != organizationId)
                    errors.Add("Manifest organizationId does not match data.organization.id.");

                await AddConflictAsync(
                    organizationId.HasValue
                        ? await _db.Organizations.AnyAsync(o => o.Id == organizationId.Value, ct)
                        : false,
                    "Organization id already exists in target deployment.",
                    conflicts);

                await AddEntityConflictsAsync(_db.Spaces, ExtractIds(spaces), "space", conflicts, ct);
                await AddEntityConflictsAsync(_db.Groups, ExtractIds(groups), "group", conflicts, ct);
                await AddEntityConflictsAsync(_db.People, ExtractIds(people), "person", conflicts, ct);
                await AddEntityConflictsAsync(_db.ScheduleVersions, ExtractIds(scheduleVersions), "schedule version", conflicts, ct);
                await AddEntityConflictsAsync(_db.Assignments, ExtractIds(assignments), "assignment", conflicts, ct);
                await AddEntityConflictsAsync(_db.SpaceSpecialDays, ExtractIds(spaceSpecialDays), "space special day", conflicts, ct);
                await AddEntityConflictsAsync(_db.SchedulingCycles, ExtractIds(schedulingCycles), "self-service cycle", conflicts, ct);
                await AddEntityConflictsAsync(_db.ShiftTemplates, ExtractIds(shiftTemplates), "shift template", conflicts, ct);
                await AddEntityConflictsAsync(_db.ShiftSlots, ExtractIds(shiftSlots), "shift slot", conflicts, ct);
                await AddEntityConflictsAsync(_db.ShiftRequests, ExtractIds(shiftRequests), "shift request", conflicts, ct);
                await AddEntityConflictsAsync(_db.ShiftAttendanceRecords, ExtractIds(shiftAttendanceRecords), "shift attendance record", conflicts, ct);
                await AddEntityConflictsAsync(_db.ShiftAbsenceReports, ExtractIds(shiftAbsenceReports), "shift absence report", conflicts, ct);
                await AddEntityConflictsAsync(_db.ShiftChangeRequests, ExtractIds(shiftChangeRequests), "shift change request", conflicts, ct);
                await AddEntityConflictsAsync(_db.WaitlistEntries, ExtractIds(waitlistEntries), "waitlist entry", conflicts, ct);
                await AddEntityConflictsAsync(_db.SwapRequests, ExtractIds(swapRequests), "swap request", conflicts, ct);
                await AddEntityConflictsAsync(_db.SpecialLeaveRequests, ExtractIds(specialLeaveRequests), "special leave request", conflicts, ct);

                var userIds = ExtractIds(GetArray(data, "users"));
                var existingUserCount = await _db.Users.CountAsync(u => userIds.Contains(u.Id), ct);
                if (existingUserCount > 0)
                    warnings.Add($"{existingUserCount} user id(s) already exist; import must re-bind or confirm matching identities.");
            }
        }

        var isImportSafe = errors.Count == 0 && conflicts.Count == 0;
        return new OrganizationImportValidationResult(
            isImportSafe,
            organizationId,
            organizationName,
            schemaVersion,
            counts,
            conflicts,
            warnings,
            errors);
    }

    private static async Task AddConflictAsync(bool hasConflict, string message, List<string> conflicts)
    {
        await Task.CompletedTask;
        if (hasConflict)
            conflicts.Add(message);
    }

    private static async Task AddEntityConflictsAsync<TEntity>(
        DbSet<TEntity> set,
        IReadOnlyCollection<Guid> ids,
        string label,
        List<string> conflicts,
        CancellationToken ct)
        where TEntity : Jobuler.Domain.Common.Entity
    {
        if (ids.Count == 0)
            return;

        var count = await set.CountAsync(e => ids.Contains(e.Id), ct);
        if (count > 0)
            conflicts.Add($"{count} {label} id(s) already exist in target deployment.");
    }

    private static void ValidateManifestCounts(
        JsonElement manifest,
        OrganizationImportValidationCounts actual,
        List<string> errors)
    {
        if (!TryGetObject(manifest, "counts", out var expected))
            return;

        Compare("spaces", actual.Spaces);
        Compare("groups", actual.Groups);
        Compare("people", actual.People);
        Compare("spaceMemberships", actual.SpaceMemberships);
        Compare("groupMemberships", actual.GroupMemberships);
        Compare("groupTasks", actual.GroupTasks);
        Compare("taskTypes", actual.TaskTypes);
        Compare("taskSlots", actual.TaskSlots);
        Compare("constraints", actual.Constraints);
        Compare("scheduleRuns", actual.ScheduleRuns);
        Compare("scheduleVersions", actual.ScheduleVersions);
        Compare("assignments", actual.Assignments);
        Compare("spaceSelfServiceDefaults", actual.SpaceSelfServiceDefaults);
        Compare("spaceSpecialDays", actual.SpaceSpecialDays);
        Compare("selfServiceConfigs", actual.SelfServiceConfigs);
        Compare("schedulingCycles", actual.SchedulingCycles);
        Compare("shiftTemplates", actual.ShiftTemplates);
        Compare("shiftSlots", actual.ShiftSlots);
        Compare("shiftRequests", actual.ShiftRequests);
        Compare("shiftAttendanceRecords", actual.ShiftAttendanceRecords);
        Compare("shiftAbsenceReports", actual.ShiftAbsenceReports);
        Compare("shiftChangeRequests", actual.ShiftChangeRequests);
        Compare("waitlistEntries", actual.WaitlistEntries);
        Compare("swapRequests", actual.SwapRequests);
        Compare("specialLeaveRequests", actual.SpecialLeaveRequests);

        void Compare(string propertyName, int actualValue)
        {
            var expectedValue = TryGetInt(expected, propertyName);
            if (expectedValue.HasValue && expectedValue.Value != actualValue)
                errors.Add($"Manifest count mismatch for {propertyName}: expected {expectedValue.Value}, package has {actualValue}.");
        }
    }

    private static List<JsonElement> GetArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            return [];

        return value.EnumerateArray().ToList();
    }

    private static List<Guid> ExtractIds(IEnumerable<JsonElement> rows) =>
        rows.Select(row => TryGetGuid(row, "id"))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

    private static bool TryGetObject(JsonElement parent, string propertyName, out JsonElement value)
    {
        if (parent.TryGetProperty(propertyName, out value) && value.ValueKind == JsonValueKind.Object)
            return true;

        value = default;
        return false;
    }

    private static Guid? TryGetGuid(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var parsed))
            return parsed;

        return null;
    }

    private static int? TryGetInt(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : null;
    }

    private static string? TryGetString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
            return null;

        return value.GetString();
    }
}
