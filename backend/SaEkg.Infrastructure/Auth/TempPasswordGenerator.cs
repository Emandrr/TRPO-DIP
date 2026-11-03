using System.Security.Cryptography;
using SaEkg.Domain.Constants;

namespace SaEkg.Infrastructure.Auth;

public static class TempPasswordGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%&*";

    public static string Generate()
    {
        Span<char> chars = stackalloc char[AuthConstants.TempPasswordLength];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        chars[0] = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz"[RandomNumberGenerator.GetInt32(46)];
        chars[1] = "23456789"[RandomNumberGenerator.GetInt32(8)];
        chars[2] = "!@#$%&*"[RandomNumberGenerator.GetInt32(7)];
        return new string(chars);
    }
}
