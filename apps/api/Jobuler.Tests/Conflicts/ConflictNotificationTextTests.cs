using FluentAssertions;
using Jobuler.Infrastructure.Conflicts;
using Xunit;

namespace Jobuler.Tests.Conflicts;

public class ConflictNotificationTextTests
{
    [Fact]
    public void Get_Hebrew_ReturnsHebrewTitleAndBody()
    {
        var (title, body) = ConflictNotificationText.Get("he");

        title.Should().Be("התנגשות שיבוצים");
        body.Should().Be("יש לך חפיפה בין שיבוצים — עדכן את המנהל");
    }

    [Fact]
    public void Get_English_ReturnsEnglishTitleAndBody()
    {
        var (title, body) = ConflictNotificationText.Get("en");

        title.Should().Be("Schedule Conflict");
        body.Should().Be("You have overlapping assignments — notify your manager");
    }

    [Fact]
    public void Get_Russian_ReturnsRussianTitleAndBody()
    {
        var (title, body) = ConflictNotificationText.Get("ru");

        title.Should().Be("Конфликт смен");
        body.Should().Be("У вас пересечение смен — сообщите менеджеру");
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("")]
    [InlineData("unknown")]
    public void Get_UnknownLocale_DefaultsToEnglish(string locale)
    {
        var (title, body) = ConflictNotificationText.Get(locale);

        title.Should().Be("Schedule Conflict");
        body.Should().Be("You have overlapping assignments — notify your manager");
    }
}
