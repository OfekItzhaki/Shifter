namespace Jobuler.Application.Common;

public interface IContactLookupProtector
{
    string NormalizeEmail(string email);
    string NormalizePhone(string phone);
    string HashEmail(string email);
    string HashPhone(string phone);
}
