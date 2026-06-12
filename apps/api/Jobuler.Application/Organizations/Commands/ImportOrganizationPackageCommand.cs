using System.Reflection;
using System.Text.Json;
using Jobuler.Domain.Constraints;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Identity;
using Jobuler.Domain.Logs;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.Organizations;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Organizations.Commands;

public record ImportOrganizationPackageCommand(string PackageJson, bool ConfirmImport)
    : IRequest<OrganizationImportResult>;

public record OrganizationImportResult(
    Guid OrganizationId,
    string? OrganizationName,
    OrganizationImportValidationCounts Counts,
    List<string> Warnings);

public class ImportOrganizationPackageCommandHandler
    : IRequestHandler<ImportOrganizationPackageCommand, OrganizationImportResult>
{
    private const string ImportedUserPasswordHashPrefix = "imported-user-reset-required:";

    private readonly AppDbContext _db;

    public ImportOrganizationPackageCommandHandler(AppDbContext db) => _db = db;

    public async Task<OrganizationImportResult> Handle(
        ImportOrganizationPackageCommand request,
        CancellationToken ct)
    {
        if (!request.ConfirmImport)
            throw new InvalidOperationException("confirmImport is required before importing an organization package.");

        var validation = await new ValidateOrganizationImportPackageCommandHandler(_db)
            .Handle(new ValidateOrganizationImportPackageCommand(request.PackageJson), ct);

        if (!validation.IsImportSafe)
        {
            var reasons = validation.Errors.Concat(validation.Conflicts).ToList();
            throw new InvalidOperationException(
                $"Organization package is not safe to import: {string.Join(" ", reasons)}");
        }

        using var document = JsonDocument.Parse(request.PackageJson);
        var data = document.RootElement.GetProperty("data");

        var transaction = _db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory"
            ? null
            : await _db.Database.BeginTransactionAsync(ct);

        try
        {
            await AddUsersAsync(data, ct);

            AddOne<Organization>(data, "organization");
            AddRange<Space>(data, "spaces");
            AddRange<SpaceMembership>(data, "spaceMemberships");
            AddRange<Group>(data, "groups");
            AddRange<Person>(data, "people");
            AddRange<GroupMembership>(data, "groupMemberships");
            AddRange<GroupTask>(data, "groupTasks");
            AddRange<TaskType>(data, "taskTypes");
            AddRange<TaskSlot>(data, "taskSlots");
            AddRange<ConstraintRule>(data, "constraints");
            AddRange<ScheduleRun>(data, "scheduleRuns");
            AddRange<ScheduleVersion>(data, "scheduleVersions");
            AddRange<Assignment>(data, "assignments");
            AddRange<SpaceSelfServiceDefaults>(data, "spaceSelfServiceDefaults");
            AddRange<SpaceSpecialDay>(data, "spaceSpecialDays");
            AddRange<SelfServiceConfig>(data, "selfServiceConfigs");
            AddRange<SchedulingCycle>(data, "schedulingCycles");
            AddRange<ShiftTemplate>(data, "shiftTemplates");
            AddRange<ShiftSlot>(data, "shiftSlots");
            AddRange<ShiftRequest>(data, "shiftRequests");
            AddRange<ShiftAttendanceRecord>(data, "shiftAttendanceRecords");
            AddRange<ShiftAbsenceReport>(data, "shiftAbsenceReports");
            AddRange<ShiftChangeRequest>(data, "shiftChangeRequests");
            AddRange<WaitlistEntry>(data, "waitlistEntries");
            AddRange<SwapRequest>(data, "swapRequests");
            AddRange<SpecialLeaveRequest>(data, "specialLeaveRequests");
            AddRange<Notification>(data, "notifications");
            AddRange<AuditLog>(data, "auditLogs");

            await _db.SaveChangesAsync(ct);

            if (transaction is not null)
                await transaction.CommitAsync(ct);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(ct);
            throw;
        }

        return new OrganizationImportResult(
            validation.OrganizationId!.Value,
            validation.OrganizationName,
            validation.Counts,
            validation.Warnings);
    }

    private async Task AddUsersAsync(JsonElement data, CancellationToken ct)
    {
        foreach (var userElement in GetArray(data, "users"))
        {
            var userId = GetRequiredGuid(userElement, "id");
            if (await _db.Users.AnyAsync(u => u.Id == userId, ct))
                continue;

            var user = Materialize<User>(userElement);
            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                SetProperty(user, nameof(User.PasswordHash),
                    BCrypt.Net.BCrypt.HashPassword($"{ImportedUserPasswordHashPrefix}{Guid.NewGuid():N}", workFactor: 12));
            }

            _db.Users.Add(user);
        }
    }

    private void AddOne<TEntity>(JsonElement data, string propertyName)
        where TEntity : class
    {
        if (!data.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"Package data is missing required object '{propertyName}'.");

        _db.Set<TEntity>().Add(Materialize<TEntity>(element));
    }

    private void AddRange<TEntity>(JsonElement data, string propertyName)
        where TEntity : class
    {
        var rows = GetArray(data, propertyName)
            .Select(Materialize<TEntity>)
            .ToList();

        if (rows.Count > 0)
            _db.Set<TEntity>().AddRange(rows);
    }

    private static TEntity Materialize<TEntity>(JsonElement element)
        where TEntity : class
    {
        var entity = (TEntity)(Activator.CreateInstance(typeof(TEntity), nonPublic: true)
            ?? throw new InvalidOperationException($"Could not construct {typeof(TEntity).Name}."));

        foreach (var property in typeof(TEntity).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var jsonName = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            if (!element.TryGetProperty(jsonName, out var value) || value.ValueKind == JsonValueKind.Undefined)
                continue;

            SetProperty(entity, property.Name, ConvertJsonValue(value, property.PropertyType));
        }

        return entity;
    }

    private static object? ConvertJsonValue(JsonElement value, Type targetType)
    {
        var nullableType = Nullable.GetUnderlyingType(targetType);
        if (value.ValueKind == JsonValueKind.Null)
            return nullableType is not null || !targetType.IsValueType ? null : Activator.CreateInstance(targetType);

        var effectiveType = nullableType ?? targetType;

        if (effectiveType == typeof(Guid))
            return value.GetGuid();
        if (effectiveType == typeof(string))
            return value.GetString();
        if (effectiveType == typeof(DateTime))
            return value.GetDateTime();
        if (effectiveType == typeof(DateOnly))
            return DateOnly.Parse(value.GetString()!);
        if (effectiveType == typeof(TimeOnly))
            return TimeOnly.Parse(value.GetString()!);
        if (effectiveType == typeof(bool))
            return value.GetBoolean();
        if (effectiveType == typeof(int))
            return value.GetInt32();
        if (effectiveType == typeof(decimal))
            return value.GetDecimal();
        if (effectiveType == typeof(double))
            return value.GetDouble();
        if (effectiveType == typeof(long))
            return value.GetInt64();
        if (effectiveType.IsEnum)
        {
            return value.ValueKind == JsonValueKind.String
                ? Enum.Parse(effectiveType, value.GetString()!, ignoreCase: true)
                : Enum.ToObject(effectiveType, value.GetInt32());
        }

        return JsonSerializer.Deserialize(value.GetRawText(), effectiveType);
    }

    private static void SetProperty(object instance, string propertyName, object? value)
    {
        var type = instance.GetType();
        while (type is not null)
        {
            var property = type.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (property is not null)
            {
                property.SetValue(instance, value);
                return;
            }

            type = type.BaseType;
        }

        throw new InvalidOperationException($"Could not set property '{propertyName}' on {instance.GetType().Name}.");
    }

    private static List<JsonElement> GetArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            return [];

        return value.EnumerateArray().ToList();
    }

    private static Guid GetRequiredGuid(JsonElement row, string propertyName)
    {
        if (!row.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"Package row is missing required '{propertyName}'.");

        return value.GetGuid();
    }
}
