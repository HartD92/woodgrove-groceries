using System.Security.Cryptography;

namespace Woodgrove.Migration.BulkImport;

public sealed class BulkImportPasswordGenerator : IBulkImportPasswordGenerator
{
    private const string AllowedChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$?_-.";

    public string Generate()
    {
        Span<char> password = stackalloc char[24];
        password[0] = GetRandomChar("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        password[1] = GetRandomChar("abcdefghijklmnopqrstuvwxyz");
        password[2] = GetRandomChar("0123456789");
        password[3] = GetRandomChar("!@$?_-.");

        for (var index = 4; index < password.Length; index++)
        {
            password[index] = GetRandomChar(AllowedChars);
        }

        Shuffle(password);
        return new string(password);
    }

    private static char GetRandomChar(string source) => source[RandomNumberGenerator.GetInt32(source.Length)];

    private static void Shuffle(Span<char> value)
    {
        for (var index = value.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (value[index], value[swapIndex]) = (value[swapIndex], value[index]);
        }
    }
}
