using FluentAssertions;
using Jobuler.Domain.People;
using Xunit;

namespace Jobuler.Tests.Domain;

public class SpecialLeaveRequestTests
{
    [Fact]
    public void Create_WithValidInput_CreatesPendingRequest()
    {
        var start = DateTime.UtcNow.AddDays(1);

        var request = SpecialLeaveRequest.Create(
            Guid.NewGuid(), Guid.NewGuid(), start, start.AddHours(4), " Family event ", Guid.NewGuid());

        request.Status.Should().Be(SpecialLeaveRequestStatus.Pending);
        request.Reason.Should().Be("Family event");
    }

    [Fact]
    public void Approve_FromPending_SetsApprovalFields()
    {
        var start = DateTime.UtcNow.AddDays(1);
        var request = SpecialLeaveRequest.Create(
            Guid.NewGuid(), Guid.NewGuid(), start, start.AddHours(4), "Event", Guid.NewGuid());
        var adminId = Guid.NewGuid();
        var presenceId = Guid.NewGuid();

        request.Approve(adminId, presenceId, "Approved");

        request.Status.Should().Be(SpecialLeaveRequestStatus.Approved);
        request.ProcessedByUserId.Should().Be(adminId);
        request.PresenceWindowId.Should().Be(presenceId);
    }
}
