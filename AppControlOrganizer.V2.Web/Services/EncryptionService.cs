//-----------------------------------------------------------------------
// <copyright file="EncryptionService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implementation of data encryption using ASP.NET Core Data Protection. Encrypts sensitive card data for storage
/// and decrypts on retrieval.
/// </summary>
public class EncryptionService(IConfiguration configuration, ILogger<EncryptionService>? logger = null) : IEncryptionService
{
  // Versioned prefix so we can change algorithms later without breaking stored data.
  private const string PrefixV2 = "enc:v2:";

  // 32-byte key required for AES-256-GCM. Provide as Base64 in ENV: EnvironmentVariables__ENCRYPTION_KEY
  // (or in appsettings under EnvironmentVariables:ENCRYPTION_KEY).
  private readonly byte[] key = GetKey(configuration);

  /// <summary>
  /// Encrypts a plaintext string.
  /// </summary>
  public string? Encrypt(string? plaintext)
  {
    if (string.IsNullOrWhiteSpace(plaintext))
    {
      return plaintext;
    }

    // Already encrypted with v2
    if (plaintext.StartsWith(PrefixV2, StringComparison.Ordinal))
    {
      return plaintext;
    }

    // Nonce (12 bytes) + Tag (16 bytes) + Ciphertext
    byte[] nonce = RandomNumberGenerator.GetBytes(12);
    byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);
    byte[] cipherBytes = new byte[plainBytes.Length];
    byte[] tag = new byte[16];

    using AesGcm aes = new AesGcm(key, tagSizeInBytes: 16);
    aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

    byte[] payload = new byte[nonce.Length + tag.Length + cipherBytes.Length];
    Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
    Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
    Buffer.BlockCopy(cipherBytes, 0, payload, nonce.Length + tag.Length, cipherBytes.Length);

    return PrefixV2 + Base64UrlEncode(payload);
  }

  /// <summary>
  /// Decrypts a ciphertext string.
  /// </summary>
  public string? Decrypt(string? ciphertext)
  {
    if (string.IsNullOrWhiteSpace(ciphertext))
    {
      return ciphertext;
    }

    // Only v2 is decryptable without the old DataProtection key ring.
    if (!ciphertext.StartsWith(PrefixV2, StringComparison.Ordinal))
    {
      return ciphertext;
    }

    try
    {
      byte[] payload = Base64UrlDecode(ciphertext[PrefixV2.Length..]);
      if (payload.Length < 12 + 16)
      {
        return ciphertext;
      }

      byte[] nonce = payload.AsSpan(0, 12).ToArray();
      byte[] tag = payload.AsSpan(12, 16).ToArray();
      byte[] cipher = payload.AsSpan(28).ToArray();

      byte[] plain = new byte[cipher.Length];
      using AesGcm aes = new AesGcm(key, tagSizeInBytes: 16);
      aes.Decrypt(nonce, cipher, tag, plain);

      return Encoding.UTF8.GetString(plain);
    }
    catch (Exception ex)
    {
      // A v2 value that cannot be authenticated means a wrong key or corrupted/tampered data. Returning the
      // ciphertext would show garbage and let numeric readers fall back to zero, so fail loudly instead.
      logger?.LogError(ex, "Encrypted value could not be decrypted with the configured key.");
      throw new CryptographicException("Encrypted value could not be decrypted with the configured key.", ex);
    }
  }

  /// <summary>
  /// Encrypts a plaintext string, returning the original value on failure.
  /// </summary>
  public string? EncryptSafe(string? plaintext)
  {
    try
    {
      return Encrypt(plaintext);
    }
    catch
    {
      return plaintext;
    }
  }

  /// <summary>
  /// Decrypts a ciphertext string, returning the original value on failure.
  /// </summary>
  public string? DecryptSafe(string? ciphertext)
  {
    try
    {
      return Decrypt(ciphertext);
    }
    catch
    {
      return ciphertext;
    }
  }

  private static byte[] GetKey(IConfiguration configuration)
  {
    string? keyB64 =
      configuration["EnvironmentVariables:ENCRYPTION_KEY"] ??
      configuration["ENCRYPTION_KEY"];

    if (string.IsNullOrWhiteSpace(keyB64))
    {
      throw new InvalidOperationException(
        "Missing encryption key. Set EnvironmentVariables__ENCRYPTION_KEY (Base64, 32 bytes) in your environment variables.");
    }

    byte[] key;
    try
    {
      key = Convert.FromBase64String(keyB64.Trim());
    }
    catch (FormatException ex)
    {
      throw new InvalidOperationException("ENCRYPTION_KEY must be Base64.", ex);
    }

    if (key.Length != 32)
    {
      throw new InvalidOperationException($"ENCRYPTION_KEY must decode to 32 bytes (got {key.Length}).");
    }

    return key;
  }

  private static string Base64UrlEncode(byte[] bytes)
  {
    return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
  }

  private static byte[] Base64UrlDecode(string text)
  {
    text = text.Replace('-', '+').Replace('_', '/');
    switch (text.Length % 4)
    {
      case 2:
        text += "==";
        break;
      case 3:
        text += "=";
        break;
    }
    return Convert.FromBase64String(text);
  }
}

/// <summary>
/// Interface for encryption services.
/// </summary>
public interface IEncryptionService
{
  /// <summary>
  /// Encrypts a plaintext string.
  /// </summary>
  string? Encrypt(string? plaintext);

  /// <summary>
  /// Decrypts a ciphertext string.
  /// </summary>
  string? Decrypt(string? ciphertext);

  /// <summary>
  /// Encrypts a plaintext string, returning the original value if encryption fails.
  /// Eliminates the need for individual try-catch blocks at call sites.
  /// </summary>
  string? EncryptSafe(string? plaintext);

  /// <summary>
  /// Decrypts a ciphertext string, returning the original value if decryption fails.
  /// Eliminates the need for individual try-catch blocks at call sites.
  /// </summary>
  string? DecryptSafe(string? ciphertext);
}
