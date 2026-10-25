using System.Security.Cryptography;
using System.Text;

namespace VanillaNotes.Core.Security;

/// <summary>
/// The recovery code of a profile: the second secret that opens its key when the password is
/// forgotten. It is shown once and stored nowhere, so it is made to be copied by hand: letters
/// and digits only, none that is mistaken for another, in groups of four.
/// </summary>
public static class RecoveryCode
{
    /// <summary>The characters a code is made of: digits and capitals, less I, L, O, and U, which are read as other characters.</summary>
    private const string _alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>How many characters a code has, not counting the hyphens: 120 random bits.</summary>
    private const int _length = 24;

    /// <summary>How many characters each group of a code shown has.</summary>
    private const int _groupLength = 4;

    /// <summary>
    /// Makes a new code, in the form it is shown to the person.
    /// </summary>
    public static string Create()
    {
        char[] characters = RandomNumberGenerator.GetItems<char>(_alphabet, _length);
        return string.Join('-', characters.Chunk(_groupLength).Select(group => new string(group)));
    }

    /// <summary>
    /// Reads a code back as a person typed it, where capitals, spaces, and hyphens make no
    /// difference. What it gives is the secret that locks the key.
    /// </summary>
    /// <returns>The code in the one form that locks and opens the key, or nothing when what was typed cannot be a code.</returns>
    public static string? Read(string typed)
    {
        var code = new StringBuilder(_length);
        foreach (char character in typed)
        {
            if (char.IsWhiteSpace(character) || character == '-')
            {
                continue;
            }

            char upper = char.ToUpperInvariant(character);
            if (!_alphabet.Contains(upper, StringComparison.Ordinal))
            {
                return null;
            }

            code.Append(upper);
        }

        return code.Length == _length ? code.ToString() : null;
    }
}
