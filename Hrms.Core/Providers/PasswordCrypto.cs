using System.Security.Cryptography;
using System.Text;

namespace Hrms.Api.Messaging;

public class PasswordCrypto
{
    private static readonly byte[] AesKey = Convert.FromBase64String("LId6/aR7VyGPJ22ln81lly9AS9ONZzobVdZCwa8rW+I=");
    private static readonly byte[] AesIV = Convert.FromBase64String("KNqFSumL92HQoY4yAgdQrw==");
    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentNullException(nameof(plainText));

        using var aes = Aes.Create();
        aes.Key = AesKey;
        aes.IV = AesIV;

        using var encryptor = aes.CreateEncryptor();
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        return Convert.ToBase64String(cipherBytes);
    }

    public static string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            throw new ArgumentNullException(nameof(encryptedText));

        byte[] cipherBytes = Convert.FromBase64String(encryptedText);

        using var aes = Aes.Create();
        aes.Key = AesKey;
        aes.IV = AesIV;

        using var decryptor = aes.CreateDecryptor();
        byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }
}