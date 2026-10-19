//-----------------------------------------------------------------------
// <copyright file="TransactionImportHasher.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Helpers;

using System.Security.Cryptography;
using System.Text;
/// <summary>
/// Deterministic hash generator for import idempotency.
/// </summary>
public static class TransactionImportHasher
{
  /// <summary>
  /// Converts a GUID string to a deterministic int hash.
  /// </summary>
  public static int HashGuid(string guidString)
  {
    if (string.IsNullOrWhiteSpace(guidString))
    {
            return 0;
    }

    byte[] inputBytes = Encoding.UTF8.GetBytes(guidString.Trim().ToUpperInvariant());
    byte[] hashBytes = SHA256.HashData(inputBytes);

    // Take first 4 bytes and convert to int (always positive via mask)
    int result = BitConverter.ToInt32(hashBytes, 0) & 0x7FFFFFFF;
    return result;
  }

  /// <summary>
  /// Normalizes description for comparison and hashing.
  /// </summary>
  public static string NormalizeDescription(string description)
  {
    if (string.IsNullOrWhiteSpace(description))
    {
            return string.Empty;
    }

    // Trim, collapse multiple spaces, uppercase for comparison
    string normalized = description.Trim();
    while (normalized.Contains("  ", StringComparison.Ordinal))
    {
            normalized = normalized.Replace("  ", " ");
    }

    // Limit to 500 chars
    if (normalized.Length > 500)
    {
      normalized = normalized.Substring(0, 500);
    }

    return normalized;
  }
}
