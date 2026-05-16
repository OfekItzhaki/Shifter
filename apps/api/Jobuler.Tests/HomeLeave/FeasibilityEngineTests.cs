using FluentAssertions;
using Jobuler.Application.HomeLeave;
using Xunit;

namespace Jobuler.Tests.HomeLeave;

public class FeasibilityEngineTests
{
    private readonly FeasibilityEngine _engine = new();

    // ── Feasible scenarios ──

    [Fact]
    public void Evaluate_WhenEnoughPeopleCanGoHome_ReturnsFeasible()
    {
        // 10 members, minPeopleAtBase=5 → leaveCapacity = 10 - 5 = 5 >= 1
        var result = _engine.Evaluate(memberCount: 10, minPeopleAtBase: 5, baseDays: 5, homeDays: 2);

        result.IsFeasible.Should().BeTrue();
        result.MaxFeasibleHomeDays.Should().NotBeNull();
        result.MaxFeasibleHomeDays.Should().BeGreaterThanOrEqualTo(2);
        result.Reason.Should().BeNull();
    }

    [Fact]
    public void Evaluate_WhenExactlyOnePeopleCanGoHome_ReturnsFeasible()
    {
        // 10 members, minPeopleAtBase=9 → leaveCapacity = 1 (exactly 1 person can go)
        var result = _engine.Evaluate(memberCount: 10, minPeopleAtBase: 9, baseDays: 7, homeDays: 2);

        result.IsFeasible.Should().BeTrue();
        result.MaxFeasibleHomeDays.Should().Be(2); // exactly at limit, no surplus
        result.Reason.Should().BeNull();
    }

    [Fact]
    public void Evaluate_WhenLargeSurplus_MaxFeasibleHomeDaysEqualsBaseDays()
    {
        // 20 members, minPeopleAtBase=3 → leaveCapacity = 17, surplus = 17 - 3 = 14
        var result = _engine.Evaluate(memberCount: 20, minPeopleAtBase: 3, baseDays: 7, homeDays: 2);

        result.IsFeasible.Should().BeTrue();
        result.MaxFeasibleHomeDays.Should().Be(7); // max(homeDays, baseDays)
        result.Reason.Should().BeNull();
    }

    // ── Not feasible scenarios ──

    [Fact]
    public void Evaluate_WhenMinPeopleAtBaseEqualsMembers_ReturnsNotFeasible()
    {
        // 5 members, minPeopleAtBase=5 → leaveCapacity = 0 < 1
        var result = _engine.Evaluate(memberCount: 5, minPeopleAtBase: 5, baseDays: 5, homeDays: 2);

        result.IsFeasible.Should().BeFalse();
        result.MaxFeasibleHomeDays.Should().BeNull();
        result.Reason.Should().NotBeNullOrEmpty();
        result.Reason.Should().Contain("אין מספיק אנשים");
    }

    [Fact]
    public void Evaluate_WhenMinPeopleAtBaseExceedsMembers_ReturnsNotFeasible()
    {
        // 5 members, minPeopleAtBase=6 → leaveCapacity = -1 < 1
        var result = _engine.Evaluate(memberCount: 5, minPeopleAtBase: 6, baseDays: 5, homeDays: 2);

        result.IsFeasible.Should().BeFalse();
        result.MaxFeasibleHomeDays.Should().BeNull();
        result.Reason.Should().NotBeNullOrEmpty();
    }

    // ── Validation / edge cases ──

    [Fact]
    public void Evaluate_WhenMemberCountIsZero_ThrowsInvalidOperation()
    {
        var act = () => _engine.Evaluate(memberCount: 0, minPeopleAtBase: 1, baseDays: 5, homeDays: 2);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Member count*");
    }

    [Fact]
    public void Evaluate_WhenMinPeopleAtBaseLessThanOne_ThrowsInvalidOperation()
    {
        var act = () => _engine.Evaluate(memberCount: 10, minPeopleAtBase: 0, baseDays: 5, homeDays: 2);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Minimum people at base*");
    }

    [Fact]
    public void Evaluate_WhenBaseDaysLessThanOne_ThrowsInvalidOperation()
    {
        var act = () => _engine.Evaluate(memberCount: 10, minPeopleAtBase: 2, baseDays: 0, homeDays: 2);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Base days*");
    }

    [Fact]
    public void Evaluate_WhenHomeDaysLessThanOne_ThrowsInvalidOperation()
    {
        var act = () => _engine.Evaluate(memberCount: 10, minPeopleAtBase: 2, baseDays: 5, homeDays: 0);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Home days*");
    }

    // ── Typical real-world scenarios ──
    // minPeopleAtBase is the coverage requirement; leaveCapacity = memberCount - minPeopleAtBase

    [Theory]
    [InlineData(10, 5, 5, 2, true)]   // leaveCapacity=5, available=5 >= 5 coverage
    [InlineData(8, 7, 7, 2, true)]    // leaveCapacity=1, available=7 >= 7 coverage (exact)
    [InlineData(6, 5, 5, 2, true)]    // leaveCapacity=1, available=5 >= 5 coverage
    [InlineData(15, 10, 5, 2, true)]  // leaveCapacity=5, available=10 >= 10 coverage
    [InlineData(15, 13, 5, 2, true)]  // leaveCapacity=2, available=13 >= 13 coverage
    [InlineData(4, 3, 3, 1, true)]    // leaveCapacity=1, available=3 >= 3 coverage (exact)
    [InlineData(4, 4, 3, 1, false)]   // leaveCapacity=0 < 1 → not feasible
    [InlineData(5, 5, 5, 2, false)]   // leaveCapacity=0 < 1 → not feasible
    public void Evaluate_VariousConfigurations_MatchesExpectedFeasibility(
        int memberCount, int minPeopleAtBase, int baseDays, int homeDays, bool expectedFeasible)
    {
        var result = _engine.Evaluate(memberCount, minPeopleAtBase, baseDays, homeDays);
        result.IsFeasible.Should().Be(expectedFeasible);
    }
}
