using FluentValidation.TestHelper;
using Jobuler.Application.Notifications;
using Jobuler.Application.Notifications.Validators;
using Xunit;

namespace Jobuler.Tests.Validation;

public class CreatePushSubscriptionCommandValidatorTests
{
    private readonly CreatePushSubscriptionCommandValidator _validator = new();

    private static CreatePushSubscriptionCommand ValidCommand() =>
        new(
            SpaceId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Endpoint: "https://fcm.googleapis.com/fcm/send/abc123",
            P256dh: "BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8p8REfWRk",
            Auth: "tBHItJI5svbpC7-BnWQ3eA");

    [Fact]
    public void Valid_Command_PassesValidation()
    {
        var result = _validator.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://not-https.example.com/push")]
    [InlineData("ftp://wrong-scheme.example.com")]
    [InlineData("not-a-url")]
    [InlineData("https://")]
    public void Invalid_Endpoint_FailsValidation(string endpoint)
    {
        var cmd = ValidCommand() with { Endpoint = endpoint };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Endpoint);
    }

    [Fact]
    public void Null_Endpoint_FailsValidation()
    {
        var cmd = ValidCommand() with { Endpoint = null! };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Endpoint);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid+chars==")]
    [InlineData("has spaces in it")]
    public void Invalid_P256dh_FailsValidation(string p256dh)
    {
        var cmd = ValidCommand() with { P256dh = p256dh };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.P256dh);
    }

    [Fact]
    public void Null_P256dh_FailsValidation()
    {
        var cmd = ValidCommand() with { P256dh = null! };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.P256dh);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid+chars==")]
    [InlineData("has spaces")]
    public void Invalid_Auth_FailsValidation(string auth)
    {
        var cmd = ValidCommand() with { Auth = auth };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Auth);
    }

    [Fact]
    public void Null_Auth_FailsValidation()
    {
        var cmd = ValidCommand() with { Auth = null! };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Auth);
    }

    [Fact]
    public void Valid_Base64Url_With_Hyphens_And_Underscores_Passes()
    {
        var cmd = ValidCommand() with
        {
            P256dh = "abc-def_ghi",
            Auth = "xyz-123_456"
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
