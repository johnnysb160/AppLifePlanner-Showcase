//-----------------------------------------------------------------------
// <copyright file="RateLimitPolicies.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Security;

/// <summary>
/// Named rate limiting policies applied to the application's endpoints.
/// </summary>
public static class RateLimitPolicies
{
  /// <summary>
  /// Policy for authentication endpoints (login, token, logout).
  /// Limit: 10 requests per minute per IP.
  /// </summary>
  public const string Login = "login";

  /// <summary>
  /// Policy for data API endpoints (accounts, transactions, allocations, credit cards).
  /// Limit: 200 requests per minute per IP.
  /// </summary>
  public const string Api = "api";
}
