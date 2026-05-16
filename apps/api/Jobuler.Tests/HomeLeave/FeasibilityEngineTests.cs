using FluentAssertions;
using Jobuler.Application.HomeLeave;
using Xunit;

namespace Jobuler.Tests.HomeLeave;

public class FeasibilityEngineTests
{
    private readonly FeasibilityEngine _engine = new();

    // ── Feasible scenarios ──

    [Fact]
    public void Evaluate_WhenAvailableExceedsCoverage_ReturnsFeasible()
    {
        // 10 members, 2 leave capacity, coverage needs 5 → available = 10 - 2 = 8 >= 5
        var result = _engine.Evaluate(memberCount: 10, leaveCapacity: 2, baseDays: 5, homeDays: 2, coverageRequirement: 5);

        result.IsFeasible.Should().BeTrue();
        result.MaxFeasibleHomeDays.Should().NotBeNull();
        result.MaxFeasibleHomeDays.Should().BeGreaterThanOrEqualTo(2);
        result.Reason.Should().BeNull();
    }

    [Fact]
    public void Evaluate_WhenAvailableExactlyEqualsCoverage_ReturnsFeasible()
    {
        // 10 members, 5 leave capacity, coverage needs 5 → available = 10 - 5 = 5 >= 5
        var result = _engine.Evaluate(memberCount: 10, leaveCapacity: 5, baseDays: 7, homeDays: 2, coverageRequirement: 5);

        result.IsFeasible.Should().BeTrue();
        result.MaxFeasibleHomeDays.Should().Be(2); // exactly at limit, no surplus
        result.Reason.Should().BeNull();
    }

    [Fact]
    public void Evaluate_WhenLargeSurplus_MaxFeasibleHomeDaysEqualsBaseDays()
    {
        // 20 members, 2 leave capacity, coverage needs 3 → available = 18, surplus = 15
        var result = _engine.Evaluate(memberCount: 20, leaveCapacity: 2, baseDays: 7, homeDays: 2, coverageRequirement: 3);

        result.IsFeasible.Should().BeTrue();
        result.MaxFeasibleHomeDays.Should().Be(7); // max(homeDays, baseDays)
        result.Reason.Should().BeNull();
    }

    // ── Not feasible scenarios ──

    [Fact]
    public void Evaluate_WhenAvailableLessThanCoverage_ReturnsNotFeasible()
    {
        // 5 members, 3 leave capacity, coverage needs 4 → available = 5 - 3 = 2 < 4
        var result = _engine.Evaluate(memberCount: 5, leaveCapacity: 3, baseDays: 5, homeDays: 2, coverageRequirement: 4);

        result.IsFeasible.Should().BeFalse();
        result.MaxFeasibleHomeDays.Should().BeNull();
        result.Reason.Should().NotBeNullOrEmpty();
        result.Reason.Should().Contain("אין מספיק אנשים");
    }

    [Fact]
    public void Evaluate_WhenAllMembersOnLeave_ReturnsNotFeasible()
    {
        // 5 members, 5 leave capacity, coverage needs 1 → available = 0 < 1
        var result = _engine.Evaluate(memberCount: 5, leaveCapacity: 5, baseDays: 5, homeDays: 2, coverageRequirement: 1);

        result.IsFeasible.Should().BeFalse();
        result.MaxFeasibleHomeDays.Should().BeNull();
        result.Reason.Should().NotBeNullOrEmpty();
    }

    // ── Validation / edge cases ──

    [Fact]
    public void Evaluate_WhenMemberCountIsZero_ThrowsInvalidOperation()
    {
        var act = () => _engine.Evaluate(memberCount: 0, leaveCapacity: 0, baseDays: 5, homeDays: 2, coverageRequirement: 1);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Member count*");
    }

    [Fact]
    public void Evaluate_WhenLeaveCapacityNegative_ThrowsInvalidOperation()
    {
        var act = () => _engine.Evaluate(memberCount: 10, leaveCapacity: -1, baseDays: 5, homeDays: 2, coverageRequirement: 1);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Leave capacity*");
    }

    [Fact]
    public void Evaluate_WhenBaseDaysLessThanOne_ThrowsInvalidOperation()
    {
        var act = () => _engine.Evaluate(memberCount: 10, leaveCapacity: 2, baseDays: 0, homeDays: 2, coverageRequirement: 1);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Base days*");
    }

    [Fact]
    public void Evaluate_WhenHomeDaysLessThanOne_ThrowsInvalidOperation()
    {
        var act = () => _engine.Evaluate(memberCount: 10, leaveCapacity: 2, baseDays: 5, homeDays: 0, coverageRequirement: 1);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Home days*");
    }

    [Fact]
    public void Evaluate_WhenCoverageRequirementLessThanOne_ThrowsInvalidOperation()
    {
        var act = () => _engine.Evaluate(memberCount: 10, leaveCapacity: 2, baseDays: 5, homeDays: 2, coverageRequirement: 0);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Coverage requirement*");
    }

    // ── Typical real-world scenarios ──

    [Theory]
    [InlineData(10, 2, 5, 2, 5, true)]   // 8 available >= 5 coverage
    [InlineData(8, 1, 7, 2, 7, true)]    // 7 available >= 7 coverage (exact)
    [InlineData(6, 2, 5, 2, 5, false)]   // 4 available < 5 coverage
    [InlineData(15, 3, 5, 2, 10, true)]  // 12 available >= 10 coverage
    [InlineData(15, 3, 5, 2, 13, false)] // 12 available < 13 coverage
    [InlineData(4, 1, 3, 1, 3, true)]    // 3 available >= 3 coverage (exact)
    [InlineData(4, 1, 3, 1, 4, false)]   // 3 available < 4 coverage
    public void Evaluate_VariousConfigurations_MatchesExpectedFeasibility(
        int memberCount, int leaveCapacity, int baseDays, int homeDays, int coverageRequirement, bool expectedFeasible)
    {
        var result = _engine.Evaluate(memberCount, leaveCapacity, baseDays, homeDays, coverageRequirement);
        result.IsFeasible.Should().Be(expectedFeasible);
    }
}
