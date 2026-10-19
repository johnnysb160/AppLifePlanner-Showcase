//-----------------------------------------------------------------------
// <copyright file="ConfigurationsValues.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Helpers;
/// <summary>
/// Provides access to configuration values required for connecting to database services, including Supabase,
/// Neon Postgres, and the encryption key.
/// </summary>
/// <remarks>All properties are initialized from environment variables and are immutable after construction. This
/// class is intended to centralize database-related configuration for application components.</remarks>
/// <param name="configuration">The configuration source used to retrieve environment variable values for connection settings.</param>
public class ConfigurationsValues(IConfiguration configuration)
{
  /// <summary>
  /// Gets the connection string for the primary Supabase database.
  /// </summary>
  public string SupabaseConnectionString { get; init; } = ReadConnectionString(
    configuration,
    "SUPABASE_CONNECTION");

  /// <summary>
  /// Gets the connection string for the secondary Neon database.
  /// </summary>
  public string NeonConnectionString { get; init; } = ReadConnectionString(
    configuration,
    "NEON_CONNECTION");

  /// <summary>
  /// Gets the encryption key.
  /// </summary>
  public string EncryptionKey { get; init; } = configuration["EnvironmentVariables:ENCRYPTION_KEY"] ?? string.Empty;

  /// <summary>
  /// Gets the command timeout duration, in seconds, applied to each individual database command.
  /// Configurable via "Database:CommandTimeoutSeconds". Default is 30 seconds.
  /// </summary>
  public string ConnectionTimeout { get; init; } = configuration["Database:CommandTimeoutSeconds"] ?? "30";

  /// <summary>
  /// Number of retry attempts for transient database errors. Default is 3.
  /// </summary>
  public string RetryAttempts { get; init; } = configuration["Retry:Attempts"] ?? "3";

  /// <summary>
  /// Base delay in seconds used for exponential backoff. Default is 1 second.
  /// </summary>
  public string RetryBaseDelaySeconds { get; init; } = configuration["Retry:BaseDelaySeconds"] ?? "1";

  private static string ReadConnectionString(IConfiguration configuration, params string[] names)
  {
    foreach (string name in names)
    {
      string? value = configuration[$"EnvironmentVariables:{name}"] ?? configuration[name];
      if (!string.IsNullOrWhiteSpace(value))
      {
        return value;
      }
    }

    return string.Empty;
  }
}
