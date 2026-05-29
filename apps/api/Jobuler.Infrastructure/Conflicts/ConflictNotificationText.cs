namespace Jobuler.Infrastructure.Conflicts;

public static class ConflictNotificationText
{
    public static (string Title, string Body) Get(string locale) => locale switch
    {
        "he" => ("Schedule Conflict", "You have overlapping assignments — notify your manager"),
        "ru" => ("Конфликт смен", "У вас пересечение смен — сообщите менеджеру"),
        _ => ("Schedule Conflict", "You have overlapping assignments — notify your manager")
    };
}
