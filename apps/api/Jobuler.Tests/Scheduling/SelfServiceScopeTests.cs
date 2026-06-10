using FluentAssertions;
using Jobuler.Api.Controllers;
using Jobuler.Application.Common;
using Jobuler.Application.Notifications;
using Jobuler.Application.Scheduling.SelfService;
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
using NSubstitute;
using System.Security.Claims;
using System.Text.Json;
using Xunit;

namespace Jobuler.Tests.Scheduling;

public class SelfServiceScopeTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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

        var controller = new ShiftSwapsController(services.SwapService, db);
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
            db);
        controller.ControllerContext = CreateControllerContext(adminUserId);

        var result = await controller.ListTargetSlotsForAdmin(
            spaceId,
            group.Id,
            "current",
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        json.Should().Contain(futureSlot.Id.ToString());
        json.Should().NotContain(startedSlot.Id.ToString());
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
            services.Mediator);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.GetStatus(spaceId, group.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SelfServiceCycleStatusResponse>().Subject;
        response.PendingShiftChangeRequestCount.Should().Be(1);
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
            services.Mediator);
        controller.ControllerContext = CreateControllerContext(userId);

        var result = await controller.GetStatus(spaceId, group.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SelfServiceCycleStatusResponse>().Subject;
        response.PendingSpecialLeaveRequestCount.Should().Be(1);
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

    private static ControllerServices CreateControllerServices() =>
        new(
            Substitute.For<IMediator>(),
            Substitute.For<IPermissionService>(),
            Substitute.For<INotificationService>(),
            Substitute.For<IShiftRequestService>(),
            Substitute.For<IWaitlistService>(),
            Substitute.For<IShiftSwapService>(),
            Substitute.For<IPushNotificationSender>());

    private sealed record ControllerServices(
        IMediator Mediator,
        IPermissionService Permissions,
        INotificationService NotificationService,
        IShiftRequestService ShiftRequestService,
        IWaitlistService WaitlistService,
        IShiftSwapService SwapService,
        IPushNotificationSender PushSender);
}
