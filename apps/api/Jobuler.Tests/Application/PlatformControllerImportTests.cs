using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Jobuler.Api.Controllers;
using Jobuler.Application.Organizations.Commands;
using Jobuler.Domain.Common;
using Jobuler.Domain.Identity;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

public class PlatformControllerImportTests
{
    [Fact]
    public async Task ImportOrganization_ForNonPlatformAdmin_ReturnsForbidWithoutDispatching()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.Users.Add(CreateUser(userId, isPlatformAdmin: false));
        await db.SaveChangesAsync();
        var mediator = Substitute.For<IMediator>();
        var controller = CreateController(mediator, db, userId);

        var result = await controller.ImportOrganization(
            CreateImportRequest("{\"schemaVersion\":1}", confirmImport: true),
            CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        await mediator.DidNotReceiveWithAnyArgs().Send(
            Arg.Any<ImportOrganizationPackageCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportOrganization_ForPlatformAdmin_DispatchesPackageImportCommand()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        const string packageJson = "{\"schemaVersion\":1,\"data\":{\"organization\":{\"name\":\"Acme\"}}}";
        db.Users.Add(CreateUser(userId, isPlatformAdmin: true));
        await db.SaveChangesAsync();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ImportOrganizationPackageCommand>(), Arg.Any<CancellationToken>())
            .Returns(new OrganizationImportResult(
                organizationId,
                "Acme",
                EmptyCounts(),
                []));
        var controller = CreateController(mediator, db, userId);

        var result = await controller.ImportOrganization(
            CreateImportRequest(packageJson, confirmImport: true),
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<OrganizationImportResult>()
            .Which.OrganizationId.Should().Be(organizationId);
        await mediator.Received(1).Send(
            Arg.Is<ImportOrganizationPackageCommand>(command =>
                command.PackageJson == packageJson &&
                command.ConfirmImport),
            Arg.Any<CancellationToken>());
    }

    private static PlatformController CreateController(IMediator mediator, AppDbContext db, Guid userId)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                "TestAuth"))
        };

        return new PlatformController(mediator, db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = context
            }
        };
    }

    private static ImportOrganizationRequest CreateImportRequest(string packageJson, bool confirmImport)
    {
        using var document = JsonDocument.Parse(packageJson);
        return new ImportOrganizationRequest(document.RootElement.Clone(), confirmImport);
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static User CreateUser(Guid id, bool isPlatformAdmin)
    {
        var user = User.Create($"user-{id:N}@example.com", "Test User", "hash", "en");
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(user, id);
        typeof(User).GetProperty(nameof(User.IsPlatformAdmin))!.SetValue(user, isPlatformAdmin);
        return user;
    }

    private static OrganizationImportValidationCounts EmptyCounts() =>
        new(
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
