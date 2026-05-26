// Feature: self-service-scheduling
// Unit tests for ShiftTemplate command validators (Create, Update, Delete)

using FluentValidation.TestHelper;
using Jobuler.Application.Scheduling.SelfService.Commands;
using Xunit;

namespace Jobuler.Tests.Validation;

public class ShiftTemplateCommandValidatorTests
{
    private readonly CreateShiftTemplateCommandValidator _createValidator = new();
    private readonly UpdateShiftTemplateCommandValidator _updateValidator = new();
    private readonly DeleteShiftTemplateCommandValidator _deleteValidator = new();

    // ── Create Command Validation ─────────────────────────────────────────────

    private static CreateShiftTemplateCommand ValidCreateCommand() => new(
        SpaceId: Guid.NewGuid(),
        GroupId: Guid.NewGuid(),
        GroupTaskId: Guid.NewGuid(),
        RequestingUserId: Guid.NewGuid(),
        DayOfWeek: DayOfWeek.Monday,
        StartTime: new TimeOnly(8, 0),
        EndTime: new TimeOnly(16, 0),
        RequiredHeadcount: 3);

    [Fact]
    public void Create_ValidCommand_PassesValidation()
    {
        var result = _createValidator.TestValidate(ValidCreateCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Create_StartTime_NotBefore_EndTime_Fails()
    {
        var cmd = ValidCreateCommand() with
        {
            StartTime = new TimeOnly(16, 0),
            EndTime = new TimeOnly(8, 0)
        };
        var result = _createValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.StartTime);
    }

    [Fact]
    public void Create_StartTime_EqualTo_EndTime_Fails()
    {
        var cmd = ValidCreateCommand() with
        {
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(10, 0)
        };
        var result = _createValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.StartTime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(-1)]
    public void Create_RequiredHeadcount_OutOfRange_Fails(int headcount)
    {
        var cmd = ValidCreateCommand() with { RequiredHeadcount = headcount };
        var result = _createValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.RequiredHeadcount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(999)]
    [InlineData(500)]
    public void Create_RequiredHeadcount_InRange_Passes(int headcount)
    {
        var cmd = ValidCreateCommand() with { RequiredHeadcount = headcount };
        var result = _createValidator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.RequiredHeadcount);
    }

    [Fact]
    public void Create_Empty_SpaceId_Fails()
    {
        var cmd = ValidCreateCommand() with { SpaceId = Guid.Empty };
        var result = _createValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.SpaceId);
    }

    [Fact]
    public void Create_Empty_GroupId_Fails()
    {
        var cmd = ValidCreateCommand() with { GroupId = Guid.Empty };
        var result = _createValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.GroupId);
    }

    [Fact]
    public void Create_Empty_GroupTaskId_Fails()
    {
        var cmd = ValidCreateCommand() with { GroupTaskId = Guid.Empty };
        var result = _createValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.GroupTaskId);
    }

    [Fact]
    public void Create_Empty_RequestingUserId_Fails()
    {
        var cmd = ValidCreateCommand() with { RequestingUserId = Guid.Empty };
        var result = _createValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.RequestingUserId);
    }

    [Theory]
    [InlineData(DayOfWeek.Sunday)]
    [InlineData(DayOfWeek.Saturday)]
    [InlineData(DayOfWeek.Wednesday)]
    public void Create_ValidDayOfWeek_Passes(DayOfWeek day)
    {
        var cmd = ValidCreateCommand() with { DayOfWeek = day };
        var result = _createValidator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.DayOfWeek);
    }

    // ── Update Command Validation ─────────────────────────────────────────────

    private static UpdateShiftTemplateCommand ValidUpdateCommand() => new(
        SpaceId: Guid.NewGuid(),
        GroupId: Guid.NewGuid(),
        TemplateId: Guid.NewGuid(),
        RequestingUserId: Guid.NewGuid(),
        DayOfWeek: DayOfWeek.Tuesday,
        StartTime: new TimeOnly(9, 0),
        EndTime: new TimeOnly(17, 0),
        RequiredHeadcount: 5);

    [Fact]
    public void Update_ValidCommand_PassesValidation()
    {
        var result = _updateValidator.TestValidate(ValidUpdateCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Update_StartTime_NotBefore_EndTime_Fails()
    {
        var cmd = ValidUpdateCommand() with
        {
            StartTime = new TimeOnly(18, 0),
            EndTime = new TimeOnly(9, 0)
        };
        var result = _updateValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.StartTime);
    }

    [Fact]
    public void Update_StartTime_EqualTo_EndTime_Fails()
    {
        var cmd = ValidUpdateCommand() with
        {
            StartTime = new TimeOnly(12, 0),
            EndTime = new TimeOnly(12, 0)
        };
        var result = _updateValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.StartTime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(-5)]
    public void Update_RequiredHeadcount_OutOfRange_Fails(int headcount)
    {
        var cmd = ValidUpdateCommand() with { RequiredHeadcount = headcount };
        var result = _updateValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.RequiredHeadcount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(999)]
    public void Update_RequiredHeadcount_BoundaryValues_Pass(int headcount)
    {
        var cmd = ValidUpdateCommand() with { RequiredHeadcount = headcount };
        var result = _updateValidator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.RequiredHeadcount);
    }

    [Fact]
    public void Update_Empty_TemplateId_Fails()
    {
        var cmd = ValidUpdateCommand() with { TemplateId = Guid.Empty };
        var result = _updateValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.TemplateId);
    }

    [Fact]
    public void Update_Empty_GroupTaskId_Fails()
    {
        var cmd = ValidUpdateCommand() with { GroupTaskId = Guid.Empty };
        var result = _updateValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.GroupTaskId);
    }

    [Fact]
    public void Update_Null_GroupTaskId_Passes()
    {
        var cmd = ValidUpdateCommand() with { GroupTaskId = null };
        var result = _updateValidator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.GroupTaskId);
    }

    // ── Delete Command Validation ─────────────────────────────────────────────

    private static DeleteShiftTemplateCommand ValidDeleteCommand() => new(
        SpaceId: Guid.NewGuid(),
        GroupId: Guid.NewGuid(),
        TemplateId: Guid.NewGuid(),
        RequestingUserId: Guid.NewGuid());

    [Fact]
    public void Delete_ValidCommand_PassesValidation()
    {
        var result = _deleteValidator.TestValidate(ValidDeleteCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Delete_Empty_SpaceId_Fails()
    {
        var cmd = ValidDeleteCommand() with { SpaceId = Guid.Empty };
        var result = _deleteValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.SpaceId);
    }

    [Fact]
    public void Delete_Empty_GroupId_Fails()
    {
        var cmd = ValidDeleteCommand() with { GroupId = Guid.Empty };
        var result = _deleteValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.GroupId);
    }

    [Fact]
    public void Delete_Empty_TemplateId_Fails()
    {
        var cmd = ValidDeleteCommand() with { TemplateId = Guid.Empty };
        var result = _deleteValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.TemplateId);
    }

    [Fact]
    public void Delete_Empty_RequestingUserId_Fails()
    {
        var cmd = ValidDeleteCommand() with { RequestingUserId = Guid.Empty };
        var result = _deleteValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.RequestingUserId);
    }
}
