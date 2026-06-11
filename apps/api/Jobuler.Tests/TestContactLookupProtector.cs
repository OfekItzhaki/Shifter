using Jobuler.Application.Common;
using Jobuler.Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace Jobuler.Tests;

public static class TestContactLookupProtector
{
    public static IContactLookupProtector Create()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:FieldHashKey"] = "test-contact-lookup-key"
            })
            .Build();

        return new ContactLookupProtector(configuration);
    }
}
