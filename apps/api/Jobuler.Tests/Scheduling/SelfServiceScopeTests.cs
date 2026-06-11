using FluentAssertions;
using Jobuler.Api.Controllers;
using Jobuler.Application.Common;
using Jobuler.Application.Exports;
using Jobuler.Application.Exports.Commands;
using Jobuler.Application.Notifications;
using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Application.Scheduling.SelfService.Commands;
using Jobuler.Application.Scheduling.SelfService.Models;
using Jobuler.Application.Scheduling.SelfService.Queries;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Jobuler.Tests.Scheduling;

public class SelfServiceScopeTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetMyWaitlistEntriesQuery_ReturnsOnlyEntriesForRequestedGroup()
    {
        using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: Guid.NewGuid());
        var groupA = Group.Create(spaceId, null, "Group A");
        var groupB = Group.Create(spaceId, null, "Group B");
        var cycleA = CreateCycle(spaceId, groupA.Id);
        var cycleB = CreateCycle(spaceId, groupB.Id);
        var taskA = CreateTask(spaceId, groupA.Id, "A", ownerUserId);
        var taskB = CreateTask(spaceId, groupB.Id, "B", ownerUserId);
        var slotA = CreateSlot(spaceId, groupA.Id, taskA.Id, cycleA.Id, daysFromNow: 2);
        var slotB = CreateSlot(spaceId, groupB.Id, taskB.Id, cycleB.Id, daysFromNow: 3);

        db.People.Add(person);
        db.Groups.AddRange(groupA, groupB);
        db.SchedulingCycles.AddRange(cycleA, cycleB);
        db.GroupTasks.AddRange(taskA, taskB);
        db.ShiftSlots.AddRange(slotA, slotB);
        db.WaitlistEntries.AddRange(
            WaitlistEntry.Create(spaceId, slotA.Id, person.Id, position: 1),
            WaitlistEntry.Create(spaceId, slotB.Id, person.Id, position: 1));
        await db.SaveChangesAsync();

        var handler = new GetMyWaitlistEntriesQueryHandler(db);

        var result = await handler.Handle(
            new GetMyWaitlistEntriesQuery(spaceId, groupA.Id, person.Id),
            CancellationToken.None);

        result.Should().ContainSingle();
        result[0].ShiftSlotId.Should().Be(slotA.Id);
        result[0].TaskName.Should().Be("A");
    }

    [Fact]
    public async Task GetAdminWaitlistEntriesQuery_ReturnsOnlyActiveEntriesForRequestedGroup()
    {
        using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var routeMember = Person.Create(spaceId, "Route Member", displayName: "Route Display", linkedUserId: Guid.NewGuid());
        var otherMember = Person.Create(spaceId, "Other Member", linkedUserId: Guid.NewGuid());
        var removedMember = Person.Create(spaceId, "Removed Member", linkedUserId: Guid.NewGuid());
        var groupA = Group.Create(spaceId, null, "Group A");
        var groupB = Group.Create(spaceId, null, "Group B");
        var cycleA = CreateCycle(spaceId, groupA.Id);
        var cycleB = CreateCycle(spaceId, groupB.Id);
        var taskA = CreateTask(spaceId, groupA.Id, "A", ownerUserId);
        var taskB = CreateTask(spaceId, groupB.Id, "B", ownerUserId);
        var slotA = CreateSlot(spaceId, groupA.Id, taskA.Id, cycleA.Id, daysFromNow: 2);
        var slotB = CreateSlot(spaceId, groupB.Id, taskB.Id, cycleB.Id, daysFromNow: 3);
        var activeEntry = WaitlistEntry.Create(spaceId, slotA.Id, routeMember.Id, position: 1);
        var removedEntry = WaitlistEntry.Create(spaceId, slotA.Id, removedMember.Id, position: 2);
        removedEntry.Remove();

        db.People.AddRange(routeMember, otherMember, removedMember);
        db.Groups.AddRange(groupA, groupB);
        db.SchedulingCycles.AddRange(cycleA, cycleB);
        db.GroupTasks.AddRange(taskA, taskB);
        db.ShiftSlots.AddRange(slotA, slotB);
        db.WaitlistEntries.AddRange(
            activeEntry,
            removedEntry,
            WaitlistEntry.Create(spaceId, slotB.Id, otherMember.Id, position: 1));
        await db.SaveChangesAsync();

        var handler = new GetAdminWaitlistEntriesQueryHandler(db);

        var result = await handler.Handle(
            new GetAdminWaitlistEntriesQuery(spaceId, groupA.Id),
            CancellationToken.None);

        result.Should().ContainSingle();
        result[0].ShiftSlotId.Should().Be(slotA.Id);
        result[0].PersonId.Should().Be(routeMember.Id);
        result[0].PersonName.Should().Be("Route Display");
        result[0].TaskName.Should().Be("A");
    }

    [Fact]
    public async Task GetAvailableSlotsQuery_ReturnsEmpty_WhenLinkedPersonIsNotGroupMember()
    {
        using var db = CreateDb();
        var availability = Substitute.For<ISlotAvailabilityEngine>();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Outsider", linkedUserId: userId);
        db.People.Add(person);
        await db.SaveChangesAsync();

        var handler = new GetAvailableSlotsQueryHandler(db, availability);

        var result = await handler.Handle(
            new GetAvailableSlotsQuery(spaceId, groupId, Guid.NewGuid(), userId),
            CancellationToken.None);

        result.Slots.Should().BeEmpty();
        await availability.DidNotReceiveWithAnyArgs()
            .GetAvailableSlotsAsync(default, default, default, default);
    }

    [Fact]
    public async Task Submit_ReturnsNotFound_WhenSlotIsOutsideRouteGroup()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var routeGroup = Group.Create(spaceId, null, "Route Group");
        var otherGroup = Group.Create(spaceId, null, "Other Group");
        var userId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var otherCycle = CreateCycle(spaceId, otherGroup.Id);
        var otherTask = CreateTask(spaceId, otherGroup.Id, "Other Task", ownerUserId);
        var otherSlot = CreateSlot(spaceId, otherGroup.Id, otherTask.Id, otherCycle.Id, daysFromNow: 2);

        db.People.Add(person);
        db.Groups.AddRange(routeGroup, otherGroup);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, routeGroup.Id, person.Id));
        db.SchedulingCycles.Add(otherCycle);
        db.GroupTasks.Add(otherTask);
        db.ShiftSlots.Add(otherSlot);
        await db.SaveChangesAsync();

        var controller = new ShiftRequestsController(
            services.Mediator,
            services.Permissions,
            services.ShiftRequestService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.Submit(
            spaceId,
            routeGroup.Id,
            new SubmitShiftRequestRequest(otherSlot.Id),
            CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        await services.ShiftRequestService.DidNotReceiveWithAnyArgs()
            .ProcessRequestAsync(default, default, default);
    }

    [Fact]
    public async Task JoinWaitlist_ReturnsNotFound_WhenSlotIsOutsideRouteGroup()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var routeGroup = Group.Create(spaceId, null, "Route Group");
        var otherGroup = Group.Create(spaceId, null, "Other Group");
        var userId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var otherCycle = CreateCycle(spaceId, otherGroup.Id);
        var otherTask = CreateTask(spaceId, otherGroup.Id, "Other Task", ownerUserId);
        var otherSlot = CreateSlot(spaceId, otherGroup.Id, otherTask.Id, otherCycle.Id, daysFromNow: 2);

        db.People.Add(person);
        db.Groups.AddRange(routeGroup, otherGroup);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, routeGroup.Id, person.Id));
        db.SchedulingCycles.Add(otherCycle);
        db.GroupTasks.Add(otherTask);
        db.ShiftSlots.Add(otherSlot);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(userId, spaceId, Permissions.SpaceView, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new WaitlistController(
            services.Mediator,
            services.Permissions,
            services.WaitlistService,
            db);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.Join(
            spaceId,
            routeGroup.Id,
            new JoinWaitlistRequest(otherSlot.Id),
            CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        await services.WaitlistService.DidNotReceiveWithAnyArgs()
            .JoinWaitlistAsync(default, default, default);
    }

    [Fact]
    public async Task AcceptSwap_ReturnsNotFound_WhenSwapIsOutsideRouteGroup()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var routeGroup = Group.Create(spaceId, null, "Route Group");
        var otherGroup = Group.Create(spaceId, null, "Other Group");
        var userId = Guid.NewGuid();
        var currentPerson = Person.Create(spaceId, "Current Member", linkedUserId: userId);
        var otherPerson = Person.Create(spaceId, "Other Member", linkedUserId: Guid.NewGuid());
        var swap = SwapRequest.Create(
            spaceId,
            otherGroup.Id,
            otherPerson.Id,
            currentPerson.Id,
            Guid.NewGuid(),
            Guid.NewGuid());

        db.People.AddRange(currentPerson, otherPerson);
        db.Groups.AddRange(routeGroup, otherGroup);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, routeGroup.Id, currentPerson.Id));
        db.SwapRequests.Add(swap);
        await db.SaveChangesAsync();

        var controller = new ShiftSwapsController(services.SwapService, services.Permissions, db);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.AcceptSwap(
            spaceId,
            routeGroup.Id,
            swap.Id,
            CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        await services.SwapService.DidNotReceiveWithAnyArgs()
            .AcceptSwapAsync(default, default, default);
    }

    [Fact]
    public async Task GetAdminSwaps_ReturnsPendingSwapsForRouteGroupOnly()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var routeGroup = Group.Create(spaceId, null, "Route Group");
        var otherGroup = Group.Create(spaceId, null, "Other Group");
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var initiator = Person.Create(spaceId, "Alice", linkedUserId: Guid.NewGuid());
        var target = Person.Create(spaceId, "Bob", linkedUserId: Guid.NewGuid());
        var noisyInitiator = Person.Create(spaceId, "Noisy Alice", linkedUserId: Guid.NewGuid());
        var noisyTarget = Person.Create(spaceId, "Noisy Bob", linkedUserId: Guid.NewGuid());
        var routeCycle = CreateCycle(spaceId, routeGroup.Id);
        var otherCycle = CreateCycle(spaceId, otherGroup.Id);
        var routeTask = CreateTask(spaceId, routeGroup.Id, "Route Task", ownerUserId);
        var otherTask = CreateTask(spaceId, otherGroup.Id, "Other Task", ownerUserId);
        var offeredSlot = CreateSlot(spaceId, routeGroup.Id, routeTask.Id, routeCycle.Id, daysFromNow: 2);
        var requestedSlot = CreateSlot(spaceId, routeGroup.Id, routeTask.Id, routeCycle.Id, daysFromNow: 3);
        var noisyOfferedSlot = CreateSlot(spaceId, otherGroup.Id, otherTask.Id, otherCycle.Id, daysFromNow: 4);
        var noisyRequestedSlot = CreateSlot(spaceId, otherGroup.Id, otherTask.Id, otherCycle.Id, daysFromNow: 5);
        var offeredRequest = ShiftRequest.Create(spaceId, offeredSlot.Id, initiator.Id, routeGroup.Id, routeCycle.Id);
        offeredRequest.Approve();
        var requestedRequest = ShiftRequest.Create(spaceId, requestedSlot.Id, target.Id, routeGroup.Id, routeCycle.Id);
        requestedRequest.Approve();
        var noisyOfferedRequest = ShiftRequest.Create(spaceId, noisyOfferedSlot.Id, noisyInitiator.Id, otherGroup.Id, otherCycle.Id);
        noisyOfferedRequest.Approve();
        var noisyRequestedRequest = ShiftRequest.Create(spaceId, noisyRequestedSlot.Id, noisyTarget.Id, otherGroup.Id, otherCycle.Id);
        noisyRequestedRequest.Approve();
        var routeSwap = SwapRequest.Create(
            spaceId,
            routeGroup.Id,
            initiator.Id,
            target.Id,
            offeredRequest.Id,
            requestedRequest.Id);
        var noisySwap = SwapRequest.Create(
            spaceId,
            otherGroup.Id,
            noisyInitiator.Id,
            noisyTarget.Id,
            noisyOfferedRequest.Id,
            noisyRequestedRequest.Id);

        db.People.AddRange(initiator, target, noisyInitiator, noisyTarget);
        db.Groups.AddRange(routeGroup, otherGroup);
        db.SchedulingCycles.AddRange(routeCycle, otherCycle);
        db.GroupTasks.AddRange(routeTask, otherTask);
        db.ShiftSlots.AddRange(offeredSlot, requestedSlot, noisyOfferedSlot, noisyRequestedSlot);
        db.ShiftRequests.AddRange(offeredRequest, requestedRequest, noisyOfferedRequest, noisyRequestedRequest);
        db.SwapRequests.AddRange(routeSwap, noisySwap);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftSwapsController(services.SwapService, services.Permissions, db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.GetAdminSwaps(
            spaceId,
            routeGroup.Id,
            status: "Pending",
            limit: 50,
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeAssignableTo<IEnumerable<SwapRequestDto>>().Subject.ToList();
        response.Should().ContainSingle();
        response[0].Id.Should().Be(routeSwap.Id);
        response[0].InitiatorPersonName.Should().Be("Alice");
        response[0].TargetPersonName.Should().Be("Bob");
        response[0].InitiatorTaskName.Should().Be("Route Task");
        response[0].TargetTaskName.Should().Be("Route Task");
    }

    [Fact]
    public async Task SubmitShiftChange_ReturnsNotFound_WhenRequestedSlotIsOutsideRouteGroup()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var routeGroup = Group.Create(spaceId, null, "Route Group");
        var otherGroup = Group.Create(spaceId, null, "Other Group");
        var userId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var routeCycle = CreateCycle(spaceId, routeGroup.Id);
        var otherCycle = CreateCycle(spaceId, otherGroup.Id);
        var routeTask = CreateTask(spaceId, routeGroup.Id, "Route Task", ownerUserId);
        var otherTask = CreateTask(spaceId, otherGroup.Id, "Other Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, routeGroup.Id, routeTask.Id, routeCycle.Id, daysFromNow: 2);
        var otherSlot = CreateSlot(spaceId, otherGroup.Id, otherTask.Id, otherCycle.Id, daysFromNow: 3);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, routeGroup.Id, routeCycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();

        db.People.Add(person);
        db.Groups.AddRange(routeGroup, otherGroup);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, routeGroup.Id, person.Id));
        db.SchedulingCycles.AddRange(routeCycle, otherCycle);
        db.GroupTasks.AddRange(routeTask, otherTask);
        db.ShiftSlots.AddRange(originalSlot, otherSlot);
        db.ShiftRequests.Add(shiftRequest);
        await db.SaveChangesAsync();

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.Submit(
            spaceId,
            routeGroup.Id,
            new SubmitShiftChangeRequest(shiftRequest.Id, otherSlot.Id, "Need another shift"),
            CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        (await db.ShiftChangeRequests.CountAsync()).Should().Be(0);
        await services.NotificationService.DidNotReceiveWithAnyArgs()
            .NotifySpaceAdminsAsync(default, default!, default!, default!, default, default, default);
    }

    [Fact]
    public async Task SubmitShiftChange_NotifiesAdmins_WhenRequestIsCreated()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", displayName: "Display Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var requestedTask = CreateTask(spaceId, group.Id, "Requested Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var requestedSlot = CreateSlot(spaceId, group.Id, requestedTask.Id, cycle.Id, daysFromNow: 3);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.AddRange(task, requestedTask);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot);
        db.ShiftRequests.Add(shiftRequest);
        await db.SaveChangesAsync();

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.Submit(
            spaceId,
            group.Id,
            new SubmitShiftChangeRequest(shiftRequest.Id, requestedSlot.Id, "Need to move"),
            CancellationToken.None);

        result.Should().BeOfType<CreatedResult>();
        await services.NotificationService.Received(1).NotifySpaceAdminsAsync(
            spaceId,
            "self_service.change_requested",
            "Shift Change Requested",
            Arg.Is<string>(body => body.Contains("Display Member") && body.Contains("Task")),
            Arg.Is<string>(metadata => metadata.Contains("\"reason\":\"Need to move\"")),
            group.Id,
            Arg.Any<CancellationToken>());

        var createdChange = await db.ShiftChangeRequests.SingleAsync();
        await services.Audit.Received(1).LogAsync(
            spaceId,
            userId,
            "self_service.submit_shift_change",
            "shift_change_request",
            createdChange.Id,
            Arg.Is<string?>(json => json == null),
            Arg.Is<string?>(json => json != null
                && json.Contains(shiftRequest.Id.ToString())
                && json.Contains(requestedSlot.Id.ToString())
                && json.Contains("\"status\":\"pending\"")),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitShiftChange_AuditFailure_DoesNotNotifyAdmins()
    {
        using var db = CreateDb();
        var services = CreateControllerServices(audit: CreateFailingAuditLogger());
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", displayName: "Display Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var requestedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot);
        db.ShiftRequests.Add(shiftRequest);
        await db.SaveChangesAsync();

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.Submit(
            spaceId,
            group.Id,
            new SubmitShiftChangeRequest(shiftRequest.Id, requestedSlot.Id, "Need to move"),
            CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        await services.NotificationService.DidNotReceiveWithAnyArgs()
            .NotifySpaceAdminsAsync(default, default!, default!, default!, default!, default, default);
    }

    [Fact]
    public async Task SubmitShiftChange_Returns422_WhenRequestedSlotIsFull()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", displayName: "Display Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var requestedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        requestedSlot.IncrementFillCount();
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot);
        db.ShiftRequests.Add(shiftRequest);
        await db.SaveChangesAsync();

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.Submit(
            spaceId,
            group.Id,
            new SubmitShiftChangeRequest(shiftRequest.Id, requestedSlot.Id, "Need to move"),
            CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        (await db.ShiftChangeRequests.CountAsync()).Should().Be(0);
        await services.Audit.DidNotReceiveWithAnyArgs()
            .LogAsync(default, default, default!, default, default, default, default, default, default);
        await services.NotificationService.DidNotReceiveWithAnyArgs()
            .NotifySpaceAdminsAsync(default, default!, default!, default!, default!, default, default);
    }

    [Fact]
    public async Task SubmitShiftChange_Returns422_WhenRequestedSlotOverlapsAnotherApprovedShift()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", displayName: "Display Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var changeDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var requestedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, changeDate, new TimeOnly(12, 0), new TimeOnly(16, 0));
        var existingSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, changeDate, new TimeOnly(10, 0), new TimeOnly(14, 0));
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();
        var existingRequest = ShiftRequest.Create(spaceId, existingSlot.Id, person.Id, group.Id, cycle.Id);
        existingRequest.Approve();
        existingSlot.IncrementFillCount();

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot, existingSlot);
        db.ShiftRequests.AddRange(shiftRequest, existingRequest);
        await db.SaveChangesAsync();

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.Submit(
            spaceId,
            group.Id,
            new SubmitShiftChangeRequest(shiftRequest.Id, requestedSlot.Id, "Need to move"),
            CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        (await db.ShiftChangeRequests.CountAsync()).Should().Be(0);
        await services.Audit.DidNotReceiveWithAnyArgs()
            .LogAsync(default, default, default!, default, default, default, default, default, default);
        await services.NotificationService.DidNotReceiveWithAnyArgs()
            .NotifySpaceAdminsAsync(default, default!, default!, default!, default!, default, default);
    }

    [Fact]
    public async Task SubmitThenApproveShiftChange_MovesAssignmentAndNotifiesMember()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var memberUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", displayName: "Display Member", linkedUserId: memberUserId);
        var cycle = CreateCycle(spaceId, group.Id);
        var originalTask = CreateTask(spaceId, group.Id, "Original Task", ownerUserId);
        var requestedTask = CreateTask(spaceId, group.Id, "Requested Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, originalTask.Id, cycle.Id, daysFromNow: 2);
        var requestedSlot = CreateSlot(spaceId, group.Id, requestedTask.Id, cycle.Id, daysFromNow: 3);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.AddRange(originalTask, requestedTask);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot);
        db.ShiftRequests.Add(shiftRequest);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);

        controller.ControllerContext = CreateControllerContext(memberUserId);
        var submitResult = await controller.Submit(
            spaceId,
            group.Id,
            new SubmitShiftChangeRequest(shiftRequest.Id, requestedSlot.Id, "Need the next day"),
            CancellationToken.None);

        submitResult.Should().BeOfType<CreatedResult>();
        var createdChange = await db.ShiftChangeRequests.SingleAsync();
        createdChange.Status.Should().Be(ShiftChangeRequestStatus.Pending);

        controller.ControllerContext = CreateControllerContext(adminUserId);
        var approveResult = await controller.Approve(
            spaceId,
            group.Id,
            createdChange.Id,
            new ReviewShiftChangeRequest("Approved"),
            CancellationToken.None);

        approveResult.Should().BeOfType<NoContentResult>();

        var updatedShiftRequest = await db.ShiftRequests.SingleAsync(r => r.Id == shiftRequest.Id);
        var updatedOriginalSlot = await db.ShiftSlots.SingleAsync(s => s.Id == originalSlot.Id);
        var updatedRequestedSlot = await db.ShiftSlots.SingleAsync(s => s.Id == requestedSlot.Id);
        var updatedChange = await db.ShiftChangeRequests.SingleAsync(r => r.Id == createdChange.Id);

        updatedShiftRequest.ShiftSlotId.Should().Be(requestedSlot.Id);
        updatedOriginalSlot.CurrentFillCount.Should().Be(0);
        updatedRequestedSlot.CurrentFillCount.Should().Be(1);
        updatedChange.Status.Should().Be(ShiftChangeRequestStatus.Approved);
        updatedChange.AdminNote.Should().Be("Approved");

        await services.NotificationService.Received(1).NotifySpaceAdminsAsync(
            spaceId,
            "self_service.change_requested",
            "Shift Change Requested",
            Arg.Is<string>(body => body.Contains("Display Member") && body.Contains("Original Task")),
            Arg.Is<string>(metadata => metadata.Contains("\"reason\":\"Need the next day\"")),
            group.Id,
            Arg.Any<CancellationToken>());

        var memberNotification = await db.Notifications
            .SingleAsync(n => n.EventType == "self_service.change_approved");
        memberNotification.UserId.Should().Be(memberUserId);
        memberNotification.MetadataJson.Should().Contain(createdChange.Id.ToString());
        memberNotification.MetadataJson.Should().Contain(requestedSlot.Id.ToString());

        await services.PushSender.Received(1)
            .SendPushToUserAsync(memberUserId, spaceId, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        await services.Audit.Received(1).LogAsync(
            spaceId,
            memberUserId,
            "self_service.submit_shift_change",
            "shift_change_request",
            createdChange.Id,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await services.Audit.Received(1).LogAsync(
            spaceId,
            adminUserId,
            "self_service.approve_shift_change",
            "shift_change_request",
            createdChange.Id,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelShiftChange_CancelsPendingRequestAndNotifiesAdmins()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", displayName: "Display Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var requestedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        var changeRequest = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            originalSlot.Id,
            requestedSlot.Id,
            person.Id,
            "Need to move",
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot);
        db.ShiftRequests.Add(shiftRequest);
        db.ShiftChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync();

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.CancelMine(
            spaceId,
            group.Id,
            changeRequest.Id,
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        var updatedChange = await db.ShiftChangeRequests.SingleAsync(r => r.Id == changeRequest.Id);
        updatedChange.Status.Should().Be(ShiftChangeRequestStatus.Cancelled);

        await services.NotificationService.Received(1).NotifySpaceAdminsAsync(
            spaceId,
            "self_service.change_cancelled",
            "Shift Change Cancelled",
            Arg.Is<string>(body => body.Contains("Display Member") && body.Contains("Task")),
            Arg.Is<string>(metadata => metadata.Contains(changeRequest.Id.ToString())
                && metadata.Contains(shiftRequest.Id.ToString())
                && metadata.Contains(originalSlot.Id.ToString())
                && metadata.Contains(requestedSlot.Id.ToString())),
            group.Id,
            Arg.Any<CancellationToken>());

        await services.Audit.Received(1).LogAsync(
            spaceId,
            userId,
            "self_service.cancel_shift_change",
            "shift_change_request",
            changeRequest.Id,
            Arg.Is<string?>(json => json != null
                && json.Contains(changeRequest.Id.ToString())
                && json.Contains("\"status\":\"pending\"")),
            Arg.Is<string?>(json => json != null
                && json.Contains(changeRequest.Id.ToString())
                && json.Contains("\"status\":\"cancelled\"")),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelShiftChange_AuditFailure_DoesNotNotifyAdmins()
    {
        using var db = CreateDb();
        var services = CreateControllerServices(audit: CreateFailingAuditLogger());
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", displayName: "Display Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var requestedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        var changeRequest = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            originalSlot.Id,
            requestedSlot.Id,
            person.Id,
            "Need to move",
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot);
        db.ShiftRequests.Add(shiftRequest);
        db.ShiftChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync();

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.CancelMine(
            spaceId,
            group.Id,
            changeRequest.Id,
            CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        await services.NotificationService.DidNotReceiveWithAnyArgs()
            .NotifySpaceAdminsAsync(default, default!, default!, default!, default!, default, default);
    }

    [Fact]
    public async Task RejectShiftChange_RejectsPendingRequestAndNotifiesMember()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var memberUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", displayName: "Display Member", linkedUserId: memberUserId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var requestedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();
        var changeRequest = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            originalSlot.Id,
            requestedSlot.Id,
            person.Id,
            "Need to move",
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot);
        db.ShiftRequests.Add(shiftRequest);
        db.ShiftChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.Reject(
            spaceId,
            group.Id,
            changeRequest.Id,
            new ReviewShiftChangeRequest("Not enough coverage"),
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        var updatedShiftRequest = await db.ShiftRequests.SingleAsync(r => r.Id == shiftRequest.Id);
        var updatedOriginalSlot = await db.ShiftSlots.SingleAsync(s => s.Id == originalSlot.Id);
        var updatedRequestedSlot = await db.ShiftSlots.SingleAsync(s => s.Id == requestedSlot.Id);
        var updatedChange = await db.ShiftChangeRequests.SingleAsync(r => r.Id == changeRequest.Id);

        updatedShiftRequest.ShiftSlotId.Should().Be(originalSlot.Id);
        updatedOriginalSlot.CurrentFillCount.Should().Be(1);
        updatedRequestedSlot.CurrentFillCount.Should().Be(0);
        updatedChange.Status.Should().Be(ShiftChangeRequestStatus.Rejected);
        updatedChange.AdminNote.Should().Be("Not enough coverage");

        var memberNotification = await db.Notifications
            .SingleAsync(n => n.EventType == "self_service.change_rejected");
        memberNotification.UserId.Should().Be(memberUserId);
        memberNotification.MetadataJson.Should().Contain(changeRequest.Id.ToString());
        memberNotification.MetadataJson.Should().Contain("\"adminNote\":\"Not enough coverage\"");

        await services.PushSender.Received(1)
            .SendPushToUserAsync(memberUserId, spaceId, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        await services.Audit.Received(1).LogAsync(
            spaceId,
            adminUserId,
            "self_service.reject_shift_change",
            "shift_change_request",
            changeRequest.Id,
            Arg.Is<string?>(json => json != null
                && json.Contains(changeRequest.Id.ToString())
                && json.Contains("\"status\":\"pending\"")),
            Arg.Is<string?>(json => json != null
                && json.Contains(shiftRequest.Id.ToString())
                && json.Contains("\"status\":\"rejected\"")
                && json.Contains("\"admin_note\":\"Not enough coverage\"")),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectShiftChange_AuditFailure_DoesNotNotifyMemberOrPush()
    {
        using var db = CreateDb();
        var services = CreateControllerServices(audit: CreateFailingAuditLogger());
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var memberUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", displayName: "Display Member", linkedUserId: memberUserId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var requestedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();
        var changeRequest = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            originalSlot.Id,
            requestedSlot.Id,
            person.Id,
            "Need to move",
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot);
        db.ShiftRequests.Add(shiftRequest);
        db.ShiftChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.Reject(
            spaceId,
            group.Id,
            changeRequest.Id,
            new ReviewShiftChangeRequest("Not enough coverage"),
            CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        (await db.Notifications.CountAsync(n => n.EventType == "self_service.change_rejected")).Should().Be(0);
        await services.PushSender.DidNotReceiveWithAnyArgs()
            .SendPushToUserAsync(default, default, default!, default);
    }

    [Fact]
    public async Task SubmitShiftChange_Returns422_WhenOriginalShiftAlreadyStarted()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: -1);
        var requestedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot);
        db.ShiftRequests.Add(shiftRequest);
        await db.SaveChangesAsync();

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.Submit(
            spaceId,
            group.Id,
            new SubmitShiftChangeRequest(shiftRequest.Id, requestedSlot.Id, "Too late"),
            CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        (await db.ShiftChangeRequests.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SubmitShiftChange_Returns422_WhenRequestedShiftAlreadyStarted()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var requestedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: -1);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot);
        db.ShiftRequests.Add(shiftRequest);
        await db.SaveChangesAsync();

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.Submit(
            spaceId,
            group.Id,
            new SubmitShiftChangeRequest(shiftRequest.Id, requestedSlot.Id, "Too late"),
            CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        (await db.ShiftChangeRequests.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ApproveShiftChange_MovesApprovedShiftToRequestedSlot()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var requestedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();
        var changeRequest = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            originalSlot.Id,
            requestedSlot.Id,
            person.Id,
            "Can I move to the next day?",
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot);
        db.ShiftRequests.Add(shiftRequest);
        db.ShiftChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.Approve(
            spaceId,
            group.Id,
            changeRequest.Id,
            new ReviewShiftChangeRequest("Approved"),
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        var updatedRequest = await db.ShiftRequests.SingleAsync(r => r.Id == shiftRequest.Id);
        var updatedOriginalSlot = await db.ShiftSlots.SingleAsync(s => s.Id == originalSlot.Id);
        var updatedRequestedSlot = await db.ShiftSlots.SingleAsync(s => s.Id == requestedSlot.Id);
        var updatedChange = await db.ShiftChangeRequests.SingleAsync(r => r.Id == changeRequest.Id);

        updatedRequest.ShiftSlotId.Should().Be(requestedSlot.Id);
        updatedOriginalSlot.CurrentFillCount.Should().Be(0);
        updatedRequestedSlot.CurrentFillCount.Should().Be(1);
        updatedChange.Status.Should().Be(ShiftChangeRequestStatus.Approved);

        var notification = await db.Notifications
            .SingleAsync(n => n.EventType == "self_service.change_approved");
        notification.UserId.Should().Be(userId);
        notification.MetadataJson.Should().Contain(changeRequest.Id.ToString());
    }

    [Fact]
    public async Task ApproveShiftChange_Returns422_WhenRequestedSlotOverlapsAnotherApprovedShift()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var requestedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 4);
        var otherApprovedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 4);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();
        var otherRequest = ShiftRequest.Create(spaceId, otherApprovedSlot.Id, person.Id, group.Id, cycle.Id);
        otherRequest.Approve();
        otherApprovedSlot.IncrementFillCount();
        var changeRequest = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            originalSlot.Id,
            requestedSlot.Id,
            person.Id,
            "Can I move to the overlapping shift?",
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot, otherApprovedSlot);
        db.ShiftRequests.AddRange(shiftRequest, otherRequest);
        db.ShiftChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.Approve(
            spaceId,
            group.Id,
            changeRequest.Id,
            new ReviewShiftChangeRequest("Approved"),
            CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        var updatedRequest = await db.ShiftRequests.SingleAsync(r => r.Id == shiftRequest.Id);
        var updatedOriginalSlot = await db.ShiftSlots.SingleAsync(s => s.Id == originalSlot.Id);
        var updatedRequestedSlot = await db.ShiftSlots.SingleAsync(s => s.Id == requestedSlot.Id);
        var updatedChange = await db.ShiftChangeRequests.SingleAsync(r => r.Id == changeRequest.Id);

        updatedRequest.ShiftSlotId.Should().Be(originalSlot.Id);
        updatedOriginalSlot.CurrentFillCount.Should().Be(1);
        updatedRequestedSlot.CurrentFillCount.Should().Be(0);
        updatedChange.Status.Should().Be(ShiftChangeRequestStatus.Pending);
    }

    [Fact]
    public async Task ApproveShiftChange_Returns422_WhenRequestedSlotViolatesMinimumRest()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        group.SetMinRestBetweenShifts(8);
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4));
        var otherApprovedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, date, new TimeOnly(8, 0), new TimeOnly(12, 0));
        var requestedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, date, new TimeOnly(18, 0), new TimeOnly(22, 0));
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();
        var otherRequest = ShiftRequest.Create(spaceId, otherApprovedSlot.Id, person.Id, group.Id, cycle.Id);
        otherRequest.Approve();
        otherApprovedSlot.IncrementFillCount();
        var changeRequest = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            originalSlot.Id,
            requestedSlot.Id,
            person.Id,
            "Can I move to the evening shift?",
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot, otherApprovedSlot);
        db.ShiftRequests.AddRange(shiftRequest, otherRequest);
        db.ShiftChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.Approve(
            spaceId,
            group.Id,
            changeRequest.Id,
            new ReviewShiftChangeRequest("Approved"),
            CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        var updatedRequest = await db.ShiftRequests.SingleAsync(r => r.Id == shiftRequest.Id);
        var updatedOriginalSlot = await db.ShiftSlots.SingleAsync(s => s.Id == originalSlot.Id);
        var updatedRequestedSlot = await db.ShiftSlots.SingleAsync(s => s.Id == requestedSlot.Id);
        var updatedChange = await db.ShiftChangeRequests.SingleAsync(r => r.Id == changeRequest.Id);

        updatedRequest.ShiftSlotId.Should().Be(originalSlot.Id);
        updatedOriginalSlot.CurrentFillCount.Should().Be(1);
        updatedRequestedSlot.CurrentFillCount.Should().Be(0);
        updatedChange.Status.Should().Be(ShiftChangeRequestStatus.Pending);
    }

    [Fact]
    public async Task ApproveShiftChange_AuditFailure_DoesNotNotifyMemberOrPush()
    {
        using var db = CreateDb();
        var services = CreateControllerServices(audit: CreateFailingAuditLogger());
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var requestedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();
        var changeRequest = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            originalSlot.Id,
            requestedSlot.Id,
            person.Id,
            "Can I move to the next day?",
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot);
        db.ShiftRequests.Add(shiftRequest);
        db.ShiftChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.Approve(
            spaceId,
            group.Id,
            changeRequest.Id,
            new ReviewShiftChangeRequest("Approved"),
            CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        (await db.Notifications.CountAsync(n => n.EventType == "self_service.change_approved")).Should().Be(0);
        await services.PushSender.DidNotReceiveWithAnyArgs()
            .SendPushToUserAsync(default, default, default!, default);
    }

    [Fact]
    public async Task ApproveShiftChange_Returns422_WithoutMutatingAssignments_WhenRequestAlreadyHandled()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var requestedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();
        var changeRequest = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            originalSlot.Id,
            requestedSlot.Id,
            person.Id,
            "Can I move this?",
            DateTime.UtcNow);
        changeRequest.Reject(adminUserId, "Already reviewed");

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot);
        db.ShiftRequests.Add(shiftRequest);
        db.ShiftChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.Approve(
            spaceId,
            group.Id,
            changeRequest.Id,
            new ReviewShiftChangeRequest("Approved anyway"),
            CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        var updatedRequest = await db.ShiftRequests.SingleAsync(r => r.Id == shiftRequest.Id);
        var updatedOriginalSlot = await db.ShiftSlots.SingleAsync(s => s.Id == originalSlot.Id);
        var updatedRequestedSlot = await db.ShiftSlots.SingleAsync(s => s.Id == requestedSlot.Id);
        var updatedChange = await db.ShiftChangeRequests.SingleAsync(r => r.Id == changeRequest.Id);

        updatedRequest.ShiftSlotId.Should().Be(originalSlot.Id);
        updatedOriginalSlot.CurrentFillCount.Should().Be(1);
        updatedRequestedSlot.CurrentFillCount.Should().Be(0);
        updatedChange.Status.Should().Be(ShiftChangeRequestStatus.Rejected);
    }

    [Fact]
    public async Task ApproveShiftChange_Returns422_WhenOriginalShiftAlreadyStarted()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: -1);
        var requestedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();
        var changeRequest = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            originalSlot.Id,
            requestedSlot.Id,
            person.Id,
            "Can I move this?",
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot);
        db.ShiftRequests.Add(shiftRequest);
        db.ShiftChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.Approve(
            spaceId,
            group.Id,
            changeRequest.Id,
            new ReviewShiftChangeRequest("Approved"),
            CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        var updatedRequest = await db.ShiftRequests.SingleAsync(r => r.Id == shiftRequest.Id);
        var updatedOriginalSlot = await db.ShiftSlots.SingleAsync(s => s.Id == originalSlot.Id);
        var updatedRequestedSlot = await db.ShiftSlots.SingleAsync(s => s.Id == requestedSlot.Id);
        var updatedChange = await db.ShiftChangeRequests.SingleAsync(r => r.Id == changeRequest.Id);

        updatedRequest.ShiftSlotId.Should().Be(originalSlot.Id);
        updatedOriginalSlot.CurrentFillCount.Should().Be(1);
        updatedRequestedSlot.CurrentFillCount.Should().Be(0);
        updatedChange.Status.Should().Be(ShiftChangeRequestStatus.Pending);
    }

    [Fact]
    public async Task ApproveShiftChange_Returns422_WhenRequestedShiftAlreadyStarted()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var requestedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: -1);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();
        var changeRequest = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            originalSlot.Id,
            requestedSlot.Id,
            person.Id,
            "Can I move this?",
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot);
        db.ShiftRequests.Add(shiftRequest);
        db.ShiftChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.Approve(
            spaceId,
            group.Id,
            changeRequest.Id,
            new ReviewShiftChangeRequest("Approved"),
            CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        var updatedRequest = await db.ShiftRequests.SingleAsync(r => r.Id == shiftRequest.Id);
        var updatedOriginalSlot = await db.ShiftSlots.SingleAsync(s => s.Id == originalSlot.Id);
        var updatedRequestedSlot = await db.ShiftSlots.SingleAsync(s => s.Id == requestedSlot.Id);
        var updatedChange = await db.ShiftChangeRequests.SingleAsync(r => r.Id == changeRequest.Id);

        updatedRequest.ShiftSlotId.Should().Be(originalSlot.Id);
        updatedOriginalSlot.CurrentFillCount.Should().Be(1);
        updatedRequestedSlot.CurrentFillCount.Should().Be(0);
        updatedChange.Status.Should().Be(ShiftChangeRequestStatus.Pending);
    }

    [Fact]
    public async Task ApproveShiftChange_Returns422_WhenNoRequestedOrTargetSlot()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();
        var changeRequest = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            originalSlot.Id,
            requestedShiftSlotId: null,
            person.Id,
            "Any other day works",
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.Add(originalSlot);
        db.ShiftRequests.Add(shiftRequest);
        db.ShiftChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.Approve(
            spaceId,
            group.Id,
            changeRequest.Id,
            new ReviewShiftChangeRequest("Approved"),
            CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        var updatedRequest = await db.ShiftRequests.SingleAsync(r => r.Id == shiftRequest.Id);
        var updatedChange = await db.ShiftChangeRequests.SingleAsync(r => r.Id == changeRequest.Id);

        updatedRequest.ShiftSlotId.Should().Be(originalSlot.Id);
        updatedChange.Status.Should().Be(ShiftChangeRequestStatus.Pending);
        updatedChange.RequestedShiftSlotId.Should().BeNull();
    }

    [Fact]
    public async Task ApproveShiftChange_UsesAdminSelectedTargetSlot_WhenMemberSubmittedFlexibleRequest()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var requestedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();
        var changeRequest = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            originalSlot.Id,
            requestedShiftSlotId: null,
            person.Id,
            "Any other day works",
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot);
        db.ShiftRequests.Add(shiftRequest);
        db.ShiftChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.Approve(
            spaceId,
            group.Id,
            changeRequest.Id,
            new ReviewShiftChangeRequest("Approved", requestedSlot.Id),
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        var updatedRequest = await db.ShiftRequests.SingleAsync(r => r.Id == shiftRequest.Id);
        var updatedOriginalSlot = await db.ShiftSlots.SingleAsync(s => s.Id == originalSlot.Id);
        var updatedRequestedSlot = await db.ShiftSlots.SingleAsync(s => s.Id == requestedSlot.Id);
        var updatedChange = await db.ShiftChangeRequests.SingleAsync(r => r.Id == changeRequest.Id);

        updatedRequest.ShiftSlotId.Should().Be(requestedSlot.Id);
        updatedOriginalSlot.CurrentFillCount.Should().Be(0);
        updatedRequestedSlot.CurrentFillCount.Should().Be(1);
        updatedChange.Status.Should().Be(ShiftChangeRequestStatus.Approved);
        updatedChange.RequestedShiftSlotId.Should().Be(requestedSlot.Id);

        await services.Audit.Received(1).LogAsync(
            spaceId,
            adminUserId,
            "self_service.approve_shift_change",
            "shift_change_request",
            changeRequest.Id,
            Arg.Is<string?>(json => json != null
                && json.Contains(changeRequest.Id.ToString())
                && json.Contains("\"status\":\"pending\"")),
            Arg.Is<string?>(json => json != null
                && json.Contains(requestedSlot.Id.ToString())
                && json.Contains("\"status\":\"approved\"")
                && json.Contains("\"admin_note\":\"Approved\"")),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListShiftChangesForAdmin_ReturnsOriginalAndRequestedSlotDetails()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", displayName: "Display Member", linkedUserId: Guid.NewGuid());
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var requestedTask = CreateTask(spaceId, group.Id, "Requested Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var requestedSlot = CreateSlot(spaceId, group.Id, requestedTask.Id, cycle.Id, daysFromNow: 3);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();
        var changeRequest = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            originalSlot.Id,
            requestedSlot.Id,
            person.Id,
            "Can I move?",
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.AddRange(task, requestedTask);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot);
        db.ShiftRequests.Add(shiftRequest);
        db.ShiftChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.ListForAdmin(spaceId, group.Id, status: null, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var rows = ok.Value.Should().BeAssignableTo<IReadOnlyList<ShiftChangeRequestDto>>().Subject;
        rows.Should().ContainSingle();
        rows[0].PersonName.Should().Be("Display Member");
        rows[0].OriginalTaskName.Should().Be("Task");
        rows[0].RequestedTaskName.Should().Be("Requested Task");
    }

    [Fact]
    public async Task ListTargetSlotsForAdmin_ExcludesStartedSlots()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var startedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: -1);
        var futureSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);

        db.Groups.Add(group);
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(startedSlot, futureSlot);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.ListTargetSlotsForAdmin(
            spaceId,
            group.Id,
            "current",
            null,
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        json.Should().Contain(futureSlot.Id.ToString());
        json.Should().NotContain(startedSlot.Id.ToString());
    }

    [Fact]
    public async Task ListTargetSlotsForAdmin_FiltersUnsafeSlotsForChangeRequest()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var adminUserId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: memberUserId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var changeDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var unsafeSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, changeDate, new TimeOnly(12, 0), new TimeOnly(16, 0));
        var existingSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, changeDate, new TimeOnly(10, 0), new TimeOnly(14, 0));
        var safeSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, changeDate.AddDays(1), new TimeOnly(8, 0), new TimeOnly(16, 0));
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        originalSlot.IncrementFillCount();
        var existingRequest = ShiftRequest.Create(spaceId, existingSlot.Id, person.Id, group.Id, cycle.Id);
        existingRequest.Approve();
        existingSlot.IncrementFillCount();
        var changeRequest = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            originalSlot.Id,
            null,
            person.Id,
            "Flexible move",
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, unsafeSlot, existingSlot, safeSlot);
        db.ShiftRequests.AddRange(shiftRequest, existingRequest);
        db.ShiftChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftChangeRequestsController(
            services.Permissions,
            services.NotificationService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.ListTargetSlotsForAdmin(
            spaceId,
            group.Id,
            cycle.Id.ToString(),
            changeRequest.Id,
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        json.Should().Contain(safeSlot.Id.ToString());
        json.Should().NotContain(originalSlot.Id.ToString());
        json.Should().NotContain(existingSlot.Id.ToString());
        json.Should().NotContain(unsafeSlot.Id.ToString());
    }

    [Fact]
    public async Task GetAdminAssignments_ReturnsOnlyApprovedAssignmentsForRequestedGroupCycle()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var otherGroup = Group.Create(spaceId, null, "Other Group");
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var assigned = Person.Create(spaceId, "Assigned Member", displayName: "Assigned Display", linkedUserId: Guid.NewGuid());
        var cancelled = Person.Create(spaceId, "Cancelled Member", linkedUserId: Guid.NewGuid());
        var otherGroupMember = Person.Create(spaceId, "Other Group Member", linkedUserId: Guid.NewGuid());
        var cycle = CreateCycle(spaceId, group.Id);
        var otherGroupCycle = CreateCycle(spaceId, otherGroup.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var otherTask = CreateTask(spaceId, otherGroup.Id, "Other Task", ownerUserId);
        var slot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var cancelledSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var otherGroupSlot = CreateSlot(spaceId, otherGroup.Id, otherTask.Id, otherGroupCycle.Id, daysFromNow: 4);
        var approvedRequest = ShiftRequest.Create(spaceId, slot.Id, assigned.Id, group.Id, cycle.Id);
        approvedRequest.Approve();
        var attendance = ShiftAttendanceRecord.Create(
            spaceId,
            group.Id,
            cycle.Id,
            approvedRequest.Id,
            slot.Id,
            assigned.Id,
            ShiftAttendanceStatus.NoShow,
            adminUserId,
            "missed");
        var cancelledRequest = ShiftRequest.Create(spaceId, cancelledSlot.Id, cancelled.Id, group.Id, cycle.Id);
        cancelledRequest.Approve();
        cancelledRequest.Cancel("cancelled");
        var otherGroupRequest = ShiftRequest.Create(spaceId, otherGroupSlot.Id, otherGroupMember.Id, otherGroup.Id, otherGroupCycle.Id);
        otherGroupRequest.Approve();

        db.People.AddRange(assigned, cancelled, otherGroupMember);
        db.Groups.AddRange(group, otherGroup);
        db.GroupMemberships.AddRange(
            GroupMembership.Create(spaceId, group.Id, assigned.Id),
            GroupMembership.Create(spaceId, group.Id, cancelled.Id),
            GroupMembership.Create(spaceId, otherGroup.Id, otherGroupMember.Id));
        db.SchedulingCycles.AddRange(cycle, otherGroupCycle);
        db.GroupTasks.AddRange(task, otherTask);
        db.ShiftSlots.AddRange(slot, cancelledSlot, otherGroupSlot);
        db.ShiftRequests.AddRange(approvedRequest, cancelledRequest, otherGroupRequest);
        db.ShiftAttendanceRecords.Add(attendance);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.SchedulePublish, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftSlotsController(services.Mediator, services.Permissions, db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.GetAdminAssignments(
            spaceId,
            group.Id,
            cycle.Id.ToString(),
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var rows = ok.Value.Should().BeAssignableTo<IReadOnlyList<ShiftSlotAssignmentResponse>>().Subject;
        rows.Should().ContainSingle();
        rows[0].ShiftRequestId.Should().Be(approvedRequest.Id);
        rows[0].ShiftSlotId.Should().Be(slot.Id);
        rows[0].PersonId.Should().Be(assigned.Id);
        rows[0].PersonName.Should().Be("Assigned Display");
        rows[0].AttendanceStatus.Should().Be("NoShow");
        rows[0].AttendanceRecordedAt.Should().Be(attendance.RecordedAt);
    }

    [Fact]
    public async Task GetAdminSlots_ReturnsFullCycleSlotsWithoutMemberAvailabilityFiltering()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var otherGroup = Group.Create(spaceId, null, "Other Group");
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var cycle = CreateCycle(spaceId, group.Id);
        var otherCycle = CreateCycle(spaceId, otherGroup.Id);
        var task = CreateTask(spaceId, group.Id, "Coverage", ownerUserId);
        var otherTask = CreateTask(spaceId, otherGroup.Id, "Other Task", ownerUserId);
        var fullSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        fullSlot.IncrementFillCount();
        var openSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var otherGroupSlot = CreateSlot(spaceId, otherGroup.Id, otherTask.Id, otherCycle.Id, daysFromNow: 4);

        db.Groups.AddRange(group, otherGroup);
        db.SchedulingCycles.AddRange(cycle, otherCycle);
        db.GroupTasks.AddRange(task, otherTask);
        db.ShiftSlots.AddRange(fullSlot, openSlot, otherGroupSlot);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.SchedulePublish, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftSlotsController(services.Mediator, services.Permissions, db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.GetAdminSlots(
            spaceId,
            group.Id,
            cycle.Id.ToString(),
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeAssignableTo<AdminShiftSlotsResponse>().Subject;
        response.CurrentCycleId.Should().Be(cycle.Id);
        response.Slots.Select(s => s.ShiftSlotId).Should().Equal(fullSlot.Id, openSlot.Id);
        response.Slots[0].CurrentFillCount.Should().Be(1);
        response.Slots[0].Capacity.Should().Be(1);
        response.Slots[0].TaskName.Should().Be("Coverage");
        response.Slots.Should().NotContain(s => s.ShiftSlotId == otherGroupSlot.Id);
    }

    [Fact]
    public async Task GetAvailable_AllowsGroupMemberWithoutSpaceViewGrant()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var slot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.Add(slot);
        await db.SaveChangesAsync();

        services.Mediator
            .Send(
                Arg.Is<GetAvailableSlotsQuery>(q =>
                    q.SpaceId == spaceId
                    && q.GroupId == group.Id
                    && q.SchedulingCycleId == cycle.Id
                    && q.UserId == userId
                    && q.IncludeFullSlots),
                Arg.Any<CancellationToken>())
            .Returns(new SlotAvailabilityResult(
                new[]
                {
                    new AvailableSlotDto(
                        slot.Id,
                        slot.Date,
                        slot.StartTime,
                        slot.EndTime,
                        "Task",
                        slot.CurrentFillCount,
                        slot.Capacity)
                },
                IsReadOnly: false,
                Message: null));

        var controller = new ShiftSlotsController(services.Mediator, services.Permissions, db);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.GetAvailable(
            spaceId,
            group.Id,
            cycle.Id.ToString(),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        await services.Permissions.DidNotReceiveWithAnyArgs()
            .RequirePermissionAsync(default, default, default!, default);
    }

    [Fact]
    public async Task GetById_AllowsGroupMemberWithoutSpaceViewGrant()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var slot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.Add(slot);
        await db.SaveChangesAsync();

        services.Mediator
            .Send(
                Arg.Is<GetShiftSlotDetailQuery>(q =>
                    q.SpaceId == spaceId
                    && q.GroupId == group.Id
                    && q.ShiftSlotId == slot.Id
                    && q.UserId == userId),
                Arg.Any<CancellationToken>())
            .Returns(new ShiftSlotDetailDto(
                slot.Id,
                group.Id,
                task.Id,
                "Task",
                slot.ShiftTemplateId,
                cycle.Id,
                slot.Date,
                slot.StartTime,
                slot.EndTime,
                slot.Capacity,
                slot.CurrentFillCount,
                slot.Status.ToString(),
                IsReadOnly: false));

        var controller = new ShiftSlotsController(services.Mediator, services.Permissions, db);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.GetById(
            spaceId,
            group.Id,
            slot.Id,
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        await services.Permissions.DidNotReceiveWithAnyArgs()
            .RequirePermissionAsync(default, default, default!, default);
    }

    [Fact]
    public async Task ListAdminShiftRequests_ReturnsCancelledRequestsForRequestedGroupOnly()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var otherGroup = Group.Create(spaceId, null, "Other Group");
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var cancelledMember = Person.Create(spaceId, "Cancelled Member", displayName: "Cancelled Display", linkedUserId: Guid.NewGuid());
        var approvedMember = Person.Create(spaceId, "Approved Member", linkedUserId: Guid.NewGuid());
        var otherGroupMember = Person.Create(spaceId, "Other Group Member", linkedUserId: Guid.NewGuid());
        var cycle = CreateCycle(spaceId, group.Id);
        var otherCycle = CreateCycle(spaceId, otherGroup.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var otherTask = CreateTask(spaceId, otherGroup.Id, "Other Task", ownerUserId);
        var cancelledSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var approvedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var otherSlot = CreateSlot(spaceId, otherGroup.Id, otherTask.Id, otherCycle.Id, daysFromNow: 4);
        var cancelledRequest = ShiftRequest.Create(spaceId, cancelledSlot.Id, cancelledMember.Id, group.Id, cycle.Id);
        cancelledRequest.Approve();
        cancelledRequest.Cancel("Cannot attend");
        var approvedRequest = ShiftRequest.Create(spaceId, approvedSlot.Id, approvedMember.Id, group.Id, cycle.Id);
        approvedRequest.Approve();
        var otherGroupCancelledRequest = ShiftRequest.Create(spaceId, otherSlot.Id, otherGroupMember.Id, otherGroup.Id, otherCycle.Id);
        otherGroupCancelledRequest.Approve();
        otherGroupCancelledRequest.Cancel("Other group cancel");

        db.People.AddRange(cancelledMember, approvedMember, otherGroupMember);
        db.Groups.AddRange(group, otherGroup);
        db.GroupMemberships.AddRange(
            GroupMembership.Create(spaceId, group.Id, cancelledMember.Id),
            GroupMembership.Create(spaceId, group.Id, approvedMember.Id),
            GroupMembership.Create(spaceId, otherGroup.Id, otherGroupMember.Id));
        db.SchedulingCycles.AddRange(cycle, otherCycle);
        db.GroupTasks.AddRange(task, otherTask);
        db.ShiftSlots.AddRange(cancelledSlot, approvedSlot, otherSlot);
        db.ShiftRequests.AddRange(cancelledRequest, approvedRequest, otherGroupCancelledRequest);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftRequestsController(
            services.Mediator,
            services.Permissions,
            services.ShiftRequestService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.ListAdmin(
            spaceId,
            group.Id,
            "Cancelled",
            limit: 20,
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var rows = ok.Value.Should().BeAssignableTo<IReadOnlyList<AdminShiftRequestResponse>>().Subject;
        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(cancelledRequest.Id);
        rows[0].PersonId.Should().Be(cancelledMember.Id);
        rows[0].PersonName.Should().Be("Cancelled Display");
        rows[0].Status.Should().Be("Cancelled");
        rows[0].CancellationReason.Should().Be("Cannot attend");
        rows[0].TaskName.Should().Be("Task");

        await services.Permissions.Received(1)
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveAbsenceReport_AuditsReview_WhenPendingReportIsApproved()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var slot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var shiftRequest = ShiftRequest.Create(spaceId, slot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        var report = ShiftAbsenceReport.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            slot.Id,
            person.Id,
            "Sick",
            isLate: true,
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.Add(slot);
        db.ShiftRequests.Add(shiftRequest);
        db.ShiftAbsenceReports.Add(report);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftRequestsController(
            services.Mediator,
            services.Permissions,
            services.ShiftRequestService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.ApproveAbsenceReport(
            spaceId,
            group.Id,
            report.Id,
            new ReviewAbsenceReportRequest("Accepted"),
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        await services.Audit.Received(1).LogAsync(
            spaceId,
            adminUserId,
            "self_service.approve_absence_report",
            "shift_absence_report",
            report.Id,
            Arg.Is<string?>(json => json != null
                && json.Contains(report.Id.ToString())
                && json.Contains("\"status\":\"pending\"")
                && json.Contains("\"is_late\":true")),
            Arg.Is<string?>(json => json != null
                && json.Contains(shiftRequest.Id.ToString())
                && json.Contains("\"status\":\"approved\"")
                && json.Contains("\"admin_note\":\"Accepted\"")),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveAbsenceReport_AuditFailure_DoesNotNotifyMemberOrPush()
    {
        using var db = CreateDb();
        var services = CreateControllerServices(audit: CreateFailingAuditLogger());
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var slot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var shiftRequest = ShiftRequest.Create(spaceId, slot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        var report = ShiftAbsenceReport.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            slot.Id,
            person.Id,
            "Sick",
            isLate: true,
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.Add(slot);
        db.ShiftRequests.Add(shiftRequest);
        db.ShiftAbsenceReports.Add(report);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftRequestsController(
            services.Mediator,
            services.Permissions,
            services.ShiftRequestService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.ApproveAbsenceReport(
            spaceId,
            group.Id,
            report.Id,
            new ReviewAbsenceReportRequest("Accepted"),
            CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        (await db.Notifications.CountAsync(n => n.EventType == "self_service.absence_approved")).Should().Be(0);
        await services.PushSender.DidNotReceiveWithAnyArgs()
            .SendPushToUserAsync(default, default, default!, default);
    }

    [Fact]
    public async Task RejectAbsenceReport_AuditsReviewAndNotifiesMember_WhenPendingReportIsRejected()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var slot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var shiftRequest = ShiftRequest.Create(spaceId, slot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        var report = ShiftAbsenceReport.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            slot.Id,
            person.Id,
            "Sick",
            isLate: true,
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.Add(slot);
        db.ShiftRequests.Add(shiftRequest);
        db.ShiftAbsenceReports.Add(report);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftRequestsController(
            services.Mediator,
            services.Permissions,
            services.ShiftRequestService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.RejectAbsenceReport(
            spaceId,
            group.Id,
            report.Id,
            new ReviewAbsenceReportRequest("Need documentation"),
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        var updatedReport = await db.ShiftAbsenceReports.SingleAsync(r => r.Id == report.Id);
        updatedReport.Status.Should().Be(ShiftAbsenceReportStatus.Rejected);
        updatedReport.AdminNote.Should().Be("Need documentation");

        var memberNotification = await db.Notifications
            .SingleAsync(n => n.EventType == "self_service.absence_rejected");
        memberNotification.UserId.Should().Be(userId);
        memberNotification.MetadataJson.Should().Contain(report.Id.ToString());
        memberNotification.MetadataJson.Should().Contain("\"adminNote\":\"Need documentation\"");

        await services.PushSender.Received(1)
            .SendPushToUserAsync(userId, spaceId, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        await services.Audit.Received(1).LogAsync(
            spaceId,
            adminUserId,
            "self_service.reject_absence_report",
            "shift_absence_report",
            report.Id,
            Arg.Is<string?>(json => json != null
                && json.Contains(report.Id.ToString())
                && json.Contains("\"status\":\"pending\"")
                && json.Contains("\"is_late\":true")),
            Arg.Is<string?>(json => json != null
                && json.Contains(shiftRequest.Id.ToString())
                && json.Contains("\"status\":\"rejected\"")
                && json.Contains("\"admin_note\":\"Need documentation\"")),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectAbsenceReport_AuditFailure_DoesNotNotifyMemberOrPush()
    {
        using var db = CreateDb();
        var services = CreateControllerServices(audit: CreateFailingAuditLogger());
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var slot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var shiftRequest = ShiftRequest.Create(spaceId, slot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        var report = ShiftAbsenceReport.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            slot.Id,
            person.Id,
            "Sick",
            isLate: true,
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.Add(slot);
        db.ShiftRequests.Add(shiftRequest);
        db.ShiftAbsenceReports.Add(report);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftRequestsController(
            services.Mediator,
            services.Permissions,
            services.ShiftRequestService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.RejectAbsenceReport(
            spaceId,
            group.Id,
            report.Id,
            new ReviewAbsenceReportRequest("Need documentation"),
            CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        (await db.Notifications.CountAsync(n => n.EventType == "self_service.absence_rejected")).Should().Be(0);
        await services.PushSender.DidNotReceiveWithAnyArgs()
            .SendPushToUserAsync(default, default, default!, default);
    }

    [Fact]
    public async Task ApproveAbsenceReport_Returns422_WhenReportIsAlreadyReviewed()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var slot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var shiftRequest = ShiftRequest.Create(spaceId, slot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        var report = ShiftAbsenceReport.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            slot.Id,
            person.Id,
            "Sick",
            isLate: false,
            DateTime.UtcNow);
        report.Approve(adminUserId, "Already reviewed");

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.Add(slot);
        db.ShiftRequests.Add(shiftRequest);
        db.ShiftAbsenceReports.Add(report);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftRequestsController(
            services.Mediator,
            services.Permissions,
            services.ShiftRequestService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.ApproveAbsenceReport(
            spaceId,
            group.Id,
            report.Id,
            new ReviewAbsenceReportRequest("Reviewed again"),
            CancellationToken.None);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        var updatedReport = await db.ShiftAbsenceReports.SingleAsync(r => r.Id == report.Id);
        updatedReport.Status.Should().Be(ShiftAbsenceReportStatus.Approved);
        updatedReport.AdminNote.Should().Be("Already reviewed");
    }

    [Fact]
    public async Task ListMyAbsenceReports_ReturnsOnlyCurrentMemberCurrentGroupReports()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var otherGroup = Group.Create(spaceId, null, "Other Group");
        var userId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var otherPerson = Person.Create(spaceId, "Other Member", linkedUserId: Guid.NewGuid());
        var cycle = CreateCycle(spaceId, group.Id);
        var otherCycle = CreateCycle(spaceId, otherGroup.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var otherTask = CreateTask(spaceId, otherGroup.Id, "Other Task", ownerUserId);
        var slot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var otherMemberSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var otherGroupSlot = CreateSlot(spaceId, otherGroup.Id, otherTask.Id, otherCycle.Id, daysFromNow: 4);
        var request = ShiftRequest.Create(spaceId, slot.Id, person.Id, group.Id, cycle.Id);
        var otherMemberRequest = ShiftRequest.Create(spaceId, otherMemberSlot.Id, otherPerson.Id, group.Id, cycle.Id);
        var otherGroupRequest = ShiftRequest.Create(spaceId, otherGroupSlot.Id, person.Id, otherGroup.Id, otherCycle.Id);
        request.Approve();
        otherMemberRequest.Approve();
        otherGroupRequest.Approve();
        var myReport = ShiftAbsenceReport.Create(
            spaceId,
            group.Id,
            cycle.Id,
            request.Id,
            slot.Id,
            person.Id,
            "Sick",
            isLate: true,
            DateTime.UtcNow);
        var otherMemberReport = ShiftAbsenceReport.Create(
            spaceId,
            group.Id,
            cycle.Id,
            otherMemberRequest.Id,
            otherMemberSlot.Id,
            otherPerson.Id,
            "Other sick",
            isLate: true,
            DateTime.UtcNow);
        var otherGroupReport = ShiftAbsenceReport.Create(
            spaceId,
            otherGroup.Id,
            otherCycle.Id,
            otherGroupRequest.Id,
            otherGroupSlot.Id,
            person.Id,
            "Wrong group",
            isLate: true,
            DateTime.UtcNow);

        db.People.AddRange(person, otherPerson);
        db.Groups.AddRange(group, otherGroup);
        db.GroupMemberships.AddRange(
            GroupMembership.Create(spaceId, group.Id, person.Id),
            GroupMembership.Create(spaceId, group.Id, otherPerson.Id),
            GroupMembership.Create(spaceId, otherGroup.Id, person.Id));
        db.SchedulingCycles.AddRange(cycle, otherCycle);
        db.GroupTasks.AddRange(task, otherTask);
        db.ShiftSlots.AddRange(slot, otherMemberSlot, otherGroupSlot);
        db.ShiftRequests.AddRange(request, otherMemberRequest, otherGroupRequest);
        db.ShiftAbsenceReports.AddRange(myReport, otherMemberReport, otherGroupReport);
        db.SelfServiceConfigs.Add(SelfServiceConfig.Create(
            spaceId,
            group.Id,
            minShiftsPerCycle: 1,
            maxShiftsPerCycle: 3,
            requestWindowOpenOffsetHours: 168,
            requestWindowCloseOffsetHours: 24,
            cancellationCutoffHours: 24,
            maxLateCancellationsPerCycle: 4,
            lateCancellationWindowHours: 12,
            waitlistOfferMinutes: 60,
            cycleDurationDays: 7));
        await db.SaveChangesAsync();

        var controller = new ShiftRequestsController(
            services.Mediator,
            services.Permissions,
            services.ShiftRequestService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.ListMyAbsenceReports(
            spaceId,
            group.Id,
            "current",
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<MyAbsenceReportsResponse>().Subject;
        response.Reports.Should().ContainSingle();
        response.Reports[0].Id.Should().Be(myReport.Id);
        response.Reports[0].TaskName.Should().Be("Task");
        response.LateReportsUsed.Should().Be(1);
        response.MaxLateReports.Should().Be(4);
        response.SchedulingCycleId.Should().Be(cycle.Id);
    }

    [Fact]
    public async Task ListMine_CurrentShiftCount_CountsOnlyApprovedAssignments()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var approvedRequestId = Guid.NewGuid();
        var pendingRequestId = Guid.NewGuid();
        var approvedSlotId = Guid.NewGuid();
        var pendingSlotId = Guid.NewGuid();
        var cycleId = Guid.NewGuid();

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        await db.SaveChangesAsync();

        services.Mediator
            .Send(Arg.Any<GetMyShiftRequestsQuery>(), Arg.Any<CancellationToken>())
            .Returns([
                new ShiftRequestDto(
                    approvedRequestId,
                    approvedSlotId,
                    group.Id,
                    cycleId,
                    "Approved",
                    false,
                    DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                    new TimeOnly(8, 0),
                    new TimeOnly(16, 0),
                    "Approved Task",
                    null,
                    null,
                    null,
                    DateTime.UtcNow),
                new ShiftRequestDto(
                    pendingRequestId,
                    pendingSlotId,
                    group.Id,
                    cycleId,
                    "Pending",
                    false,
                    DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
                    new TimeOnly(8, 0),
                    new TimeOnly(16, 0),
                    "Pending Task",
                    null,
                    null,
                    null,
                    DateTime.UtcNow)
            ]);

        var controller = new ShiftRequestsController(
            services.Mediator,
            services.Permissions,
            services.ShiftRequestService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.ListMine(
            spaceId,
            group.Id,
            schedulingCycleId: null,
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<MyShiftRequestsResponse>().Subject;
        response.CurrentShiftCount.Should().Be(1);
        response.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetCycleStatus_IncludesPendingShiftChangeRequests()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var userId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var originalSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var requestedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var shiftRequest = ShiftRequest.Create(spaceId, originalSlot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve();
        var changeRequest = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            originalSlot.Id,
            requestedSlot.Id,
            person.Id,
            "Need to move",
            DateTime.UtcNow);

        db.People.Add(person);
        db.Groups.Add(group);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(originalSlot, requestedSlot);
        db.ShiftRequests.Add(shiftRequest);
        db.ShiftChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(userId, spaceId, Permissions.SpaceView, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new SelfServiceCyclesController(
            db,
            services.Permissions,
            Substitute.For<ISlotGenerationService>(),
            services.Mediator,
            Substitute.For<IPdfRenderer>());
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.GetStatus(spaceId, group.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SelfServiceCycleStatusResponse>().Subject;
        response.PendingShiftChangeRequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetCycleStatus_IgnoresRowsOutsideResolvedCycleScope()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var otherSpaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var otherGroup = Group.Create(otherSpaceId, null, "Other Group");
        var userId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var member = Person.Create(spaceId, "Member", linkedUserId: userId);
        var targetMember = Person.Create(spaceId, "Target Member", linkedUserId: Guid.NewGuid());
        var noisyMember = Person.Create(otherSpaceId, "Noisy Member", linkedUserId: Guid.NewGuid());
        var noisyTargetMember = Person.Create(otherSpaceId, "Noisy Target", linkedUserId: Guid.NewGuid());
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Task", ownerUserId);
        var otherTask = CreateTask(otherSpaceId, otherGroup.Id, "Other Task", ownerUserId);
        var approvedSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var pendingSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var targetSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 4);
        var noisySlot = CreateSlot(otherSpaceId, otherGroup.Id, otherTask.Id, cycle.Id, daysFromNow: 4);
        var noisyTargetSlot = CreateSlot(otherSpaceId, otherGroup.Id, otherTask.Id, cycle.Id, daysFromNow: 5);
        approvedSlot.IncrementFillCount();
        targetSlot.IncrementFillCount();
        noisySlot.IncrementFillCount();
        noisyTargetSlot.IncrementFillCount();

        var approvedRequest = ShiftRequest.Create(spaceId, approvedSlot.Id, member.Id, group.Id, cycle.Id);
        approvedRequest.Approve();
        var pendingRequest = ShiftRequest.Create(spaceId, pendingSlot.Id, member.Id, group.Id, cycle.Id);
        var targetRequest = ShiftRequest.Create(spaceId, targetSlot.Id, targetMember.Id, group.Id, cycle.Id);
        targetRequest.Approve();
        var noisyApprovedRequest = ShiftRequest.Create(otherSpaceId, noisySlot.Id, noisyMember.Id, otherGroup.Id, cycle.Id);
        noisyApprovedRequest.Approve();
        var noisyPendingRequest = ShiftRequest.Create(otherSpaceId, noisySlot.Id, noisyMember.Id, otherGroup.Id, cycle.Id);
        var noisyTargetRequest = ShiftRequest.Create(otherSpaceId, noisyTargetSlot.Id, noisyTargetMember.Id, otherGroup.Id, cycle.Id);
        noisyTargetRequest.Approve();

        var absenceReport = ShiftAbsenceReport.Create(
            spaceId,
            group.Id,
            cycle.Id,
            approvedRequest.Id,
            approvedSlot.Id,
            member.Id,
            "Sick",
            isLate: true,
            DateTime.UtcNow);
        var noisyAbsenceReport = ShiftAbsenceReport.Create(
            otherSpaceId,
            otherGroup.Id,
            cycle.Id,
            noisyApprovedRequest.Id,
            noisySlot.Id,
            noisyMember.Id,
            "Wrong scope",
            isLate: true,
            DateTime.UtcNow);
        var changeRequest = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            approvedRequest.Id,
            approvedSlot.Id,
            pendingSlot.Id,
            member.Id,
            "Need to move",
            DateTime.UtcNow);
        var noisyChangeRequest = ShiftChangeRequest.Create(
            otherSpaceId,
            otherGroup.Id,
            cycle.Id,
            noisyApprovedRequest.Id,
            noisySlot.Id,
            noisyTargetSlot.Id,
            noisyMember.Id,
            "Wrong scope",
            DateTime.UtcNow);
        var swapRequest = SwapRequest.Create(
            spaceId,
            group.Id,
            member.Id,
            targetMember.Id,
            approvedRequest.Id,
            targetRequest.Id);
        var noisySwapRequest = SwapRequest.Create(
            otherSpaceId,
            otherGroup.Id,
            noisyMember.Id,
            noisyTargetMember.Id,
            noisyApprovedRequest.Id,
            noisyTargetRequest.Id);

        db.People.AddRange(member, targetMember, noisyMember, noisyTargetMember);
        db.Groups.AddRange(group, otherGroup);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, member.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.AddRange(task, otherTask);
        db.ShiftSlots.AddRange(approvedSlot, pendingSlot, targetSlot, noisySlot, noisyTargetSlot);
        db.ShiftRequests.AddRange(approvedRequest, pendingRequest, targetRequest, noisyApprovedRequest, noisyPendingRequest, noisyTargetRequest);
        db.ShiftAbsenceReports.AddRange(absenceReport, noisyAbsenceReport);
        db.ShiftChangeRequests.AddRange(changeRequest, noisyChangeRequest);
        db.SwapRequests.AddRange(swapRequest, noisySwapRequest);
        db.WaitlistEntries.AddRange(
            WaitlistEntry.Create(spaceId, pendingSlot.Id, member.Id, position: 1),
            WaitlistEntry.Create(otherSpaceId, noisySlot.Id, noisyMember.Id, position: 1));
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(userId, spaceId, Permissions.SpaceView, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new SelfServiceCyclesController(
            db,
            services.Permissions,
            Substitute.For<ISlotGenerationService>(),
            services.Mediator,
            Substitute.For<IPdfRenderer>());
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.GetStatus(spaceId, group.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SelfServiceCycleStatusResponse>().Subject;
        response.SlotCount.Should().Be(3);
        response.TotalCapacity.Should().Be(3);
        response.FilledCount.Should().Be(2);
        response.ApprovedCount.Should().Be(2);
        response.PendingCount.Should().Be(1);
        response.WaitlistCount.Should().Be(1);
        response.PendingAbsenceReportCount.Should().Be(1);
        response.LatePendingAbsenceReportCount.Should().Be(1);
        response.PendingShiftChangeRequestCount.Should().Be(1);
        response.PendingSwapRequestCount.Should().Be(1);
        response.UnderfilledSlotCount.Should().Be(1);
        response.UnderfilledSlots.Should().ContainSingle(s => s.ShiftSlotId == pendingSlot.Id);
    }

    [Fact]
    public async Task GetCycleCloseout_SummarizesCurrentCycleOperatingMetrics()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Self-service group");
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var alice = Person.Create(spaceId, "Alice", linkedUserId: Guid.NewGuid());
        var bob = Person.Create(spaceId, "Bob", linkedUserId: Guid.NewGuid());
        var charlie = Person.Create(spaceId, "Charlie", linkedUserId: Guid.NewGuid());
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Guard", ownerUserId);
        var filledSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 2);
        var underfilledSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 3);
        var overfilledSlot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: 4);

        var approvedRequest = ShiftRequest.Create(spaceId, filledSlot.Id, alice.Id, group.Id, cycle.Id);
        approvedRequest.Approve();
        filledSlot.IncrementFillCount();

        var pendingRequest = ShiftRequest.Create(spaceId, underfilledSlot.Id, bob.Id, group.Id, cycle.Id);

        var rejectedRequest = ShiftRequest.Create(spaceId, underfilledSlot.Id, charlie.Id, group.Id, cycle.Id);
        rejectedRequest.Reject("Not eligible", adminUserId);

        var cancelledRequest = ShiftRequest.Create(spaceId, filledSlot.Id, bob.Id, group.Id, cycle.Id);
        cancelledRequest.Approve();
        cancelledRequest.Cancel("Cannot attend: sick");

        var adminOverrideRequest = ShiftRequest.Create(
            spaceId,
            overfilledSlot.Id,
            bob.Id,
            group.Id,
            cycle.Id,
            isAdminOverride: true,
            processedByUserId: adminUserId);
        adminOverrideRequest.Approve(adminUserId);
        overfilledSlot.IncrementFillCount();

        var swapTargetRequest = ShiftRequest.Create(spaceId, overfilledSlot.Id, charlie.Id, group.Id, cycle.Id);
        swapTargetRequest.Approve();
        overfilledSlot.IncrementFillCount();

        var pendingAbsence = ShiftAbsenceReport.Create(
            spaceId,
            group.Id,
            cycle.Id,
            approvedRequest.Id,
            filledSlot.Id,
            alice.Id,
            "Traffic",
            isLate: true,
            DateTime.UtcNow);
        var approvedAbsence = ShiftAbsenceReport.Create(
            spaceId,
            group.Id,
            cycle.Id,
            cancelledRequest.Id,
            filledSlot.Id,
            bob.Id,
            "Medical",
            isLate: false,
            DateTime.UtcNow);
        approvedAbsence.Approve(adminUserId);
        var rejectedAbsence = ShiftAbsenceReport.Create(
            spaceId,
            group.Id,
            cycle.Id,
            adminOverrideRequest.Id,
            overfilledSlot.Id,
            bob.Id,
            "Invalid",
            isLate: true,
            DateTime.UtcNow);
        rejectedAbsence.Reject(adminUserId);

        var pendingChange = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            approvedRequest.Id,
            filledSlot.Id,
            underfilledSlot.Id,
            alice.Id,
            "Need later",
            DateTime.UtcNow);
        var approvedChange = ShiftChangeRequest.Create(
            spaceId,
            group.Id,
            cycle.Id,
            adminOverrideRequest.Id,
            overfilledSlot.Id,
            filledSlot.Id,
            bob.Id,
            "Move me",
            DateTime.UtcNow);
        approvedChange.Approve(adminUserId);

        var pendingSwap = SwapRequest.Create(
            spaceId,
            group.Id,
            alice.Id,
            charlie.Id,
            approvedRequest.Id,
            swapTargetRequest.Id);
        var acceptedSwap = SwapRequest.Create(
            spaceId,
            group.Id,
            bob.Id,
            charlie.Id,
            adminOverrideRequest.Id,
            swapTargetRequest.Id);
        acceptedSwap.Accept();

        var waitingEntry = WaitlistEntry.Create(spaceId, underfilledSlot.Id, alice.Id, position: 1);
        var acceptedEntry = WaitlistEntry.Create(spaceId, underfilledSlot.Id, bob.Id, position: 2);
        acceptedEntry.Accept();
        var expiredEntry = WaitlistEntry.Create(spaceId, filledSlot.Id, charlie.Id, position: 3);
        expiredEntry.Expire();

        var pendingLeave = SpecialLeaveRequest.Create(
            spaceId,
            alice.Id,
            cycle.StartsAt.AddHours(2),
            cycle.StartsAt.AddHours(4),
            "Appointment",
            alice.LinkedUserId!.Value);

        db.People.AddRange(alice, bob, charlie);
        db.Groups.Add(group);
        db.GroupMemberships.AddRange(
            GroupMembership.Create(spaceId, group.Id, alice.Id),
            GroupMembership.Create(spaceId, group.Id, bob.Id),
            GroupMembership.Create(spaceId, group.Id, charlie.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.AddRange(filledSlot, underfilledSlot, overfilledSlot);
        db.ShiftRequests.AddRange(
            approvedRequest,
            pendingRequest,
            rejectedRequest,
            cancelledRequest,
            adminOverrideRequest,
            swapTargetRequest);
        db.ShiftAbsenceReports.AddRange(pendingAbsence, approvedAbsence, rejectedAbsence);
        db.ShiftChangeRequests.AddRange(pendingChange, approvedChange);
        db.SwapRequests.AddRange(pendingSwap, acceptedSwap);
        db.WaitlistEntries.AddRange(waitingEntry, acceptedEntry, expiredEntry);
        db.SpecialLeaveRequests.Add(pendingLeave);
        db.ShiftAttendanceRecords.AddRange(
            ShiftAttendanceRecord.Create(
                spaceId,
                group.Id,
                cycle.Id,
                approvedRequest.Id,
                approvedRequest.ShiftSlotId,
                approvedRequest.PersonId,
                ShiftAttendanceStatus.Present,
                adminUserId),
            ShiftAttendanceRecord.Create(
                spaceId,
                group.Id,
                cycle.Id,
                adminOverrideRequest.Id,
                adminOverrideRequest.ShiftSlotId,
                adminOverrideRequest.PersonId,
                ShiftAttendanceStatus.NoShow,
                adminUserId,
                "Did not arrive"));
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new SelfServiceCyclesController(
            db,
            services.Permissions,
            Substitute.For<ISlotGenerationService>(),
            services.Mediator,
            Substitute.For<IPdfRenderer>());
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.GetCloseout(spaceId, group.Id, cycle.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SelfServiceCycleCloseoutResponse>().Subject;
        response.CycleId.Should().Be(cycle.Id);
        response.SlotCount.Should().Be(3);
        response.TotalCapacity.Should().Be(3);
        response.FilledCount.Should().Be(3);
        response.UnderfilledSlotCount.Should().Be(1);
        response.OverfilledSlotCount.Should().Be(1);
        response.ApprovedAssignments.Should().Be(3);
        response.CancelledAssignments.Should().Be(1);
        response.RejectedRequests.Should().Be(1);
        response.PendingRequests.Should().Be(1);
        response.AdminOverrideAssignments.Should().Be(1);
        response.CannotAttendCancellations.Should().Be(1);
        response.LateAbsenceReports.Should().Be(2);
        response.ApprovedAbsenceReports.Should().Be(1);
        response.RejectedAbsenceReports.Should().Be(1);
        response.PendingAbsenceReports.Should().Be(1);
        response.PresentAttendanceRecords.Should().Be(1);
        response.NoShowAttendanceRecords.Should().Be(1);
        response.ExcusedAttendanceRecords.Should().Be(0);
        response.UnconfirmedAttendanceCount.Should().Be(1);
        response.ApprovedChangeRequests.Should().Be(1);
        response.PendingChangeRequests.Should().Be(1);
        response.AcceptedSwapRequests.Should().Be(1);
        response.PendingSwapRequests.Should().Be(1);
        response.ActiveWaitlistEntries.Should().Be(1);
        response.AcceptedWaitlistEntries.Should().Be(1);
        response.ExpiredWaitlistEntries.Should().Be(1);
        response.PendingSpecialLeaveRequests.Should().Be(1);
        response.IssueCount.Should().Be(7);
    }

    [Fact]
    public async Task ExportCloseoutCsv_ReturnsDownloadableCloseoutMetrics()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Self-service group");
        var adminUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Alice", linkedUserId: Guid.NewGuid());
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Guard", adminUserId);
        var slot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: -1);
        var request = ShiftRequest.Create(spaceId, slot.Id, person.Id, group.Id, cycle.Id);
        request.Approve(adminUserId);
        slot.IncrementFillCount();

        db.Groups.Add(group);
        db.People.Add(person);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.Add(slot);
        db.ShiftRequests.Add(request);
        db.ShiftAttendanceRecords.Add(ShiftAttendanceRecord.Create(
            spaceId,
            group.Id,
            cycle.Id,
            request.Id,
            slot.Id,
            person.Id,
            ShiftAttendanceStatus.NoShow,
            adminUserId));
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new SelfServiceCyclesController(
            db,
            services.Permissions,
            Substitute.For<ISlotGenerationService>(),
            services.Mediator,
            Substitute.For<IPdfRenderer>());
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.ExportCloseoutCsv(spaceId, group.Id, cycle.Id, CancellationToken.None);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("text/csv; charset=utf-8");
        file.FileDownloadName.Should().StartWith($"self-service-closeout-{cycle.Id:N}-");

        var csv = Encoding.UTF8.GetString(file.FileContents);
        csv.Should().Contain("metric,value");
        csv.Should().Contain($"cycle_id,{cycle.Id}");
        csv.Should().Contain("approved_assignments,1");
        csv.Should().Contain("no_show_attendance_records,1");
        csv.Should().Contain("unconfirmed_attendance_count,0");
    }

    [Fact]
    public async Task ExportCloseoutPdf_ReturnsRenderedCloseoutReport()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var space = Space.Create("Ops Space", adminUserId);
        typeof(Jobuler.Domain.Common.Entity).GetProperty(nameof(Space.Id))!.SetValue(space, spaceId);
        var group = Group.Create(spaceId, null, "Self-service group");
        var person = Person.Create(spaceId, "Alice", linkedUserId: Guid.NewGuid());
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Guard", adminUserId);
        var slot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: -1);
        var request = ShiftRequest.Create(spaceId, slot.Id, person.Id, group.Id, cycle.Id);
        request.Approve(adminUserId);
        slot.IncrementFillCount();

        db.Spaces.Add(space);
        db.Groups.Add(group);
        db.People.Add(person);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.Add(slot);
        db.ShiftRequests.Add(request);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        SelfServiceCloseoutPdfModel? renderedModel = null;
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var pdfRenderer = Substitute.For<IPdfRenderer>();
        pdfRenderer.Render(Arg.Do<SelfServiceCloseoutPdfModel>(m => renderedModel = m))
            .Returns(pdfBytes);

        var controller = new SelfServiceCyclesController(
            db,
            services.Permissions,
            Substitute.For<ISlotGenerationService>(),
            services.Mediator,
            pdfRenderer);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.ExportCloseoutPdf(spaceId, group.Id, cycle.Id, CancellationToken.None);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("application/pdf");
        file.FileDownloadName.Should().StartWith($"self-service-closeout-{cycle.Id:N}-");
        file.FileContents.Should().Equal(pdfBytes);
        renderedModel.Should().NotBeNull();
        renderedModel!.SpaceName.Should().Be("Ops Space");
        renderedModel.GroupName.Should().Be("Self-service group");
        renderedModel.CycleId.Should().Be(cycle.Id);
        renderedModel.ReportFingerprint.Should().MatchRegex("^[A-F0-9]{64}$");
        renderedModel.Metrics.Should().Contain(m => m.Label == "approved_assignments" && m.Value == "1");
    }

    [Fact]
    public async Task RecordAttendance_CreatesOrUpdatesAttendanceForApprovedPastShift()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Self-service group");
        var adminUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: Guid.NewGuid());
        var cycle = CreateCycle(spaceId, group.Id);
        var task = CreateTask(spaceId, group.Id, "Guard", adminUserId);
        var slot = CreateSlot(spaceId, group.Id, task.Id, cycle.Id, daysFromNow: -1);
        var shiftRequest = ShiftRequest.Create(spaceId, slot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve(adminUserId);
        slot.IncrementFillCount();

        db.Groups.Add(group);
        db.People.Add(person);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        db.SchedulingCycles.Add(cycle);
        db.GroupTasks.Add(task);
        db.ShiftSlots.Add(slot);
        db.ShiftRequests.Add(shiftRequest);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new ShiftRequestsController(
            services.Mediator,
            services.Permissions,
            services.ShiftRequestService,
            services.PushSender,
            services.Audit,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var createResult = await controller.RecordAttendance(
            spaceId,
            group.Id,
            shiftRequest.Id,
            new RecordShiftAttendanceRequest("NoShow", "Did not arrive"),
            CancellationToken.None);

        var createOk = createResult.Should().BeOfType<OkObjectResult>().Subject;
        var created = createOk.Value.Should().BeOfType<ShiftAttendanceResponse>().Subject;
        created.Status.Should().Be("NoShow");
        created.Note.Should().Be("Did not arrive");

        var updateResult = await controller.RecordAttendance(
            spaceId,
            group.Id,
            shiftRequest.Id,
            new RecordShiftAttendanceRequest("Excused", "Commander approved"),
            CancellationToken.None);

        var updateOk = updateResult.Should().BeOfType<OkObjectResult>().Subject;
        var updated = updateOk.Value.Should().BeOfType<ShiftAttendanceResponse>().Subject;
        updated.Id.Should().Be(created.Id);
        updated.Status.Should().Be("Excused");
        updated.Note.Should().Be("Commander approved");

        var records = await db.ShiftAttendanceRecords.ToListAsync();
        records.Should().ContainSingle();
        records[0].Status.Should().Be(ShiftAttendanceStatus.Excused);
    }

    [Fact]
    public async Task GenerateNext_CreatesCycleFromConfigAndRunsSlotGeneration()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        group.SetSchedulingMode(SchedulingMode.SelfService);
        var adminUserId = Guid.NewGuid();
        var config = SelfServiceConfig.Create(
            spaceId,
            group.Id,
            minShiftsPerCycle: 1,
            maxShiftsPerCycle: 4,
            requestWindowOpenOffsetHours: 168,
            requestWindowCloseOffsetHours: 24,
            cancellationCutoffHours: 24,
            maxLateCancellationsPerCycle: 2,
            lateCancellationWindowHours: 24,
            waitlistOfferMinutes: 60,
            cycleDurationDays: 10);

        db.Groups.Add(group);
        db.SelfServiceConfigs.Add(config);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var slotGeneration = Substitute.For<ISlotGenerationService>();
        var controller = new SelfServiceCyclesController(
            db,
            services.Permissions,
            slotGeneration,
            services.Mediator,
            Substitute.For<IPdfRenderer>());
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.GenerateNext(spaceId, group.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SelfServiceCycleStatusResponse>().Subject;

        var cycle = await db.SchedulingCycles.SingleAsync(c => c.Id == response.CycleId);
        cycle.SpaceId.Should().Be(spaceId);
        cycle.GroupId.Should().Be(group.Id);
        (cycle.EndsAt - cycle.StartsAt).Should().Be(TimeSpan.FromDays(10));
        cycle.RequestWindowOpensAt.Should().Be(cycle.StartsAt.AddHours(-168));
        cycle.RequestWindowClosesAt.Should().Be(cycle.StartsAt.AddHours(-24));

        await slotGeneration.Received(1)
            .GenerateSlotsForCycleAsync(group.Id, cycle.Id, Arg.Any<CancellationToken>());
        response.SlotCount.Should().Be(0);
    }

    [Fact]
    public async Task CloseWindow_WhenWindowIsOpen_RunsUnderScheduledCheck()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Self-service group");
        group.SetSchedulingMode(SchedulingMode.SelfService);
        var adminUserId = Guid.NewGuid();
        var cycle = CreateCycle(spaceId, group.Id);

        db.Groups.Add(group);
        db.SchedulingCycles.Add(cycle);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        services.Mediator
            .Send(Arg.Any<CheckUnderScheduledMembersCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CheckUnderScheduledMembersResult(true, []));

        var controller = new SelfServiceCyclesController(
            db,
            services.Permissions,
            Substitute.For<ISlotGenerationService>(),
            services.Mediator,
            Substitute.For<IPdfRenderer>());
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.CloseWindow(spaceId, group.Id, cycle.Id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        await services.Mediator.Received(1)
            .Send(
                Arg.Is<CheckUnderScheduledMembersCommand>(command =>
                    command.SpaceId == spaceId
                    && command.GroupId == group.Id
                    && command.SchedulingCycleId == cycle.Id),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CloseWindow_WhenWindowIsAlreadyClosed_DoesNotRunUnderScheduledCheckAgain()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Self-service group");
        group.SetSchedulingMode(SchedulingMode.SelfService);
        var adminUserId = Guid.NewGuid();
        var cycle = CreateCycle(spaceId, group.Id);
        cycle.UpdateRequestWindowClose(DateTime.UtcNow.AddHours(-1));

        db.Groups.Add(group);
        db.SchedulingCycles.Add(cycle);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new SelfServiceCyclesController(
            db,
            services.Permissions,
            Substitute.For<ISlotGenerationService>(),
            services.Mediator,
            Substitute.For<IPdfRenderer>());
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.CloseWindow(spaceId, group.Id, cycle.Id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        await services.Mediator.DidNotReceiveWithAnyArgs()
            .Send(default(CheckUnderScheduledMembersCommand)!, default);
    }

    [Fact]
    public async Task GetCycleStatus_IncludesPendingSpecialLeaveRequestsForCycleMembers()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var otherGroup = Group.Create(spaceId, null, "Other Group");
        var userId = Guid.NewGuid();
        var member = Person.Create(spaceId, "Member", linkedUserId: userId);
        var otherGroupMember = Person.Create(spaceId, "Other Member", linkedUserId: Guid.NewGuid());
        var cycle = CreateCycle(spaceId, group.Id);
        var inCycleLeave = SpecialLeaveRequest.Create(
            spaceId,
            member.Id,
            cycle.StartsAt.AddHours(4),
            cycle.StartsAt.AddHours(8),
            "Medical appointment",
            userId);
        var otherGroupLeave = SpecialLeaveRequest.Create(
            spaceId,
            otherGroupMember.Id,
            cycle.StartsAt.AddHours(4),
            cycle.StartsAt.AddHours(8),
            "Wrong group",
            Guid.NewGuid());
        var outsideCycleLeave = SpecialLeaveRequest.Create(
            spaceId,
            member.Id,
            cycle.EndsAt.AddDays(1),
            cycle.EndsAt.AddDays(2),
            "Outside cycle",
            userId);

        db.People.AddRange(member, otherGroupMember);
        db.Groups.AddRange(group, otherGroup);
        db.GroupMemberships.AddRange(
            GroupMembership.Create(spaceId, group.Id, member.Id),
            GroupMembership.Create(spaceId, otherGroup.Id, otherGroupMember.Id));
        db.SchedulingCycles.Add(cycle);
        db.SpecialLeaveRequests.AddRange(inCycleLeave, otherGroupLeave, outsideCycleLeave);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(userId, spaceId, Permissions.SpaceView, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new SelfServiceCyclesController(
            db,
            services.Permissions,
            Substitute.For<ISlotGenerationService>(),
            services.Mediator,
            Substitute.For<IPdfRenderer>());
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.GetStatus(spaceId, group.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SelfServiceCycleStatusResponse>().Subject;
        response.PendingSpecialLeaveRequestCount.Should().Be(1);
    }

    [Fact]
    public async Task MineSpecialLeave_RequiresSpaceViewPermission()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: userId);

        db.People.Add(person);
        await db.SaveChangesAsync();

        services.Permissions
            .RequirePermissionAsync(userId, spaceId, Permissions.SpaceView, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new SpecialLeaveRequestsController(
            services.Mediator,
            services.Permissions,
            db);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.Mine(spaceId, from: null, to: null, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        await services.Permissions.Received(1)
            .RequirePermissionAsync(userId, spaceId, Permissions.SpaceView, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListSpecialLeaveForAdmin_RequiresConstraintsManagePermission()
    {
        using var db = CreateDb();
        var services = CreateControllerServices();
        var spaceId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        services.Permissions
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = new SpecialLeaveRequestsController(
            services.Mediator,
            services.Permissions,
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.ListForAdmin(
            spaceId,
            status: null,
            from: null,
            to: null,
            groupId: null,
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        await services.Permissions.Received(1)
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.ConstraintsManage, Arg.Any<CancellationToken>());
        await services.Permissions.DidNotReceive()
            .RequirePermissionAsync(adminUserId, spaceId, Permissions.PeopleManage, Arg.Any<CancellationToken>());
    }

    private static SchedulingCycle CreateCycle(Guid spaceId, Guid groupId)
    {
        var utcNow = DateTime.UtcNow;
        return SchedulingCycle.Create(
            spaceId,
            groupId,
            startsAt: utcNow.AddDays(1),
            endsAt: utcNow.AddDays(8),
            requestWindowOpensAt: utcNow.AddDays(-2),
            requestWindowClosesAt: utcNow.AddHours(1));
    }

    private static GroupTask CreateTask(Guid spaceId, Guid groupId, string name, Guid createdByUserId)
    {
        var utcNow = DateTime.UtcNow;
        return GroupTask.Create(
            spaceId,
            groupId,
            name,
            utcNow,
            utcNow.AddDays(30),
            shiftDurationMinutes: 480,
            requiredHeadcount: 1,
            burdenLevel: TaskBurdenLevel.Normal,
            allowsDoubleShift: false,
            allowsOverlap: false,
            createdByUserId: createdByUserId);
    }

    private static ShiftSlot CreateSlot(
        Guid spaceId,
        Guid groupId,
        Guid taskId,
        Guid cycleId,
        int daysFromNow) =>
        ShiftSlot.Create(
            spaceId,
            groupId,
            taskId,
            shiftTemplateId: Guid.NewGuid(),
            schedulingCycleId: cycleId,
            date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysFromNow)),
            startTime: new TimeOnly(8, 0),
            endTime: new TimeOnly(16, 0),
            capacity: 1);

    private static ShiftSlot CreateSlot(
        Guid spaceId,
        Guid groupId,
        Guid taskId,
        Guid cycleId,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime) =>
        ShiftSlot.Create(
            spaceId,
            groupId,
            taskId,
            shiftTemplateId: Guid.NewGuid(),
            schedulingCycleId: cycleId,
            date: date,
            startTime: startTime,
            endTime: endTime,
            capacity: 1);

    private static IAuditLogger CreateAuditLogger()
    {
        var audit = Substitute.For<IAuditLogger>();
        audit.LogAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return audit;
    }

    private static IAuditLogger CreateFailingAuditLogger()
    {
        var audit = Substitute.For<IAuditLogger>();
        audit.LogAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Audit failed."));
        return audit;
    }


    private static ControllerContext CreateControllerContext(Guid userId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                "TestAuth"))
        };

        return new ControllerContext { HttpContext = httpContext };
    }

    private static ControllerServices CreateControllerServices(IAuditLogger? audit = null) =>
        new(
            Substitute.For<IMediator>(),
            Substitute.For<IPermissionService>(),
            Substitute.For<INotificationService>(),
            Substitute.For<IShiftRequestService>(),
            Substitute.For<IWaitlistService>(),
            Substitute.For<IShiftSwapService>(),
            Substitute.For<IPushNotificationSender>(),
            audit ?? CreateAuditLogger());

    private sealed record ControllerServices(
        IMediator Mediator,
        IPermissionService Permissions,
        INotificationService NotificationService,
        IShiftRequestService ShiftRequestService,
        IWaitlistService WaitlistService,
        IShiftSwapService SwapService,
        IPushNotificationSender PushSender,
        IAuditLogger Audit);
}
