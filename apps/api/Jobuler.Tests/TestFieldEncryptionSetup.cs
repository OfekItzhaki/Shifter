using System.Runtime.CompilerServices;
using Jobuler.Infrastructure.Security;

namespace Jobuler.Tests;

public static class TestFieldEncryptionSetup
{
    [ModuleInitializer]
    public static void ConfigureFieldEncryption()
    {
        FieldEncryption.Configure("jobuler-tests-field-encryption-key");
    }
}
