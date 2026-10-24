using System.Security.Cryptography;
using Harmonee.Api.Contracts.Common;

namespace Harmonee.Families.Domain;

internal static class InviteCodes
{
    // No 0/O, 1/I/L: codes are read aloud and typed by hand.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Generate() =>
        new(RandomNumberGenerator.GetItems<char>(Alphabet, FieldLimits.InviteCodeLength));

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();
}
