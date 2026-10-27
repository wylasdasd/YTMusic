using System.Security.Cryptography;
using System.Text;

namespace CommonHelp;

/// <summary>
/// 功能职责：AES-GCM 字符串加解密。密钥由上下文（如站点 Origin）+ 前缀派生，密文带版本前缀。适合浏览器存储等半可信环境；不能替代真正的密钥管理。
/// 方法群组：加密、尝试解密。
/// </summary>
public static class AesGcmCryptoHelp
{
    /// <summary>密文版本前缀；非该前缀的输入 <see cref="TryDecrypt"/> 返回 false。</summary>
    public const string EncryptedPrefix = "v1:";

    private const int NonceSize = 12;
    private const int TagSize = 16;

    /// <summary>
    /// 功能作用：加密明文，输出 <c>v1:Base64(nonce‖cipher‖tag)</c>。
    /// </summary>
    /// <param name="plaintext">明文。</param>
    /// <param name="context">密钥绑定上下文（如页面 Origin）；解密时必须一致。</param>
    /// <param name="keyMaterialPrefix">密钥派生前缀，隔离不同用途的密钥空间。</param>
    /// <returns>带版本前缀的密文。</returns>
    public static string Encrypt(string plaintext, string context, string keyMaterialPrefix = "")
    {
        var key = DeriveKey(keyMaterialPrefix, context);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var payload = new byte[NonceSize + cipherBytes.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(cipherBytes, 0, payload, NonceSize, cipherBytes.Length);
        Buffer.BlockCopy(tag, 0, payload, NonceSize + cipherBytes.Length, TagSize);

        return EncryptedPrefix + Convert.ToBase64String(payload);
    }

    /// <summary>
    /// 功能作用：尝试解密 <see cref="Encrypt"/> 产生的密文；前缀不符、载荷损坏或上下文不匹配时返回 false。
    /// </summary>
    /// <param name="stored">含版本前缀的密文。</param>
    /// <param name="context">加密时使用的上下文。</param>
    /// <param name="plaintext">解密成功时的明文。</param>
    /// <param name="keyMaterialPrefix">加密时使用的密钥派生前缀。</param>
    /// <returns>解密成功为 true。</returns>
    public static bool TryDecrypt(string stored, string context, out string? plaintext, string keyMaterialPrefix = "")
    {
        plaintext = null;
        if (string.IsNullOrEmpty(stored) || !stored.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
            return false;

        try
        {
            var payload = Convert.FromBase64String(stored[EncryptedPrefix.Length..]);
            if (payload.Length <= NonceSize + TagSize)
                return false;

            var nonce = payload.AsSpan(0, NonceSize);
            var tag = payload.AsSpan(payload.Length - TagSize, TagSize);
            var cipherBytes = payload.AsSpan(NonceSize, payload.Length - NonceSize - TagSize);

            var plainBytes = new byte[cipherBytes.Length];
            using var aes = new AesGcm(DeriveKey(keyMaterialPrefix, context), TagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

            plaintext = Encoding.UTF8.GetString(plainBytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] DeriveKey(string keyMaterialPrefix, string context) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterialPrefix + context));
}
