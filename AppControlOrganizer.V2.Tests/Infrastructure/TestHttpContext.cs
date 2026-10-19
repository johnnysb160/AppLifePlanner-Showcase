//-----------------------------------------------------------------------
// <copyright file="TestHttpContext.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Infrastructure;

using System.Security.Claims;
using AppControlOrganizer.V2.Web.Data;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Builds HTTP context accessors that exercise the application's tenant filters.
/// </summary>
internal static class TestHttpContext
{
  /// <summary>
  /// Creates an accessor with no active HTTP context.
  /// </summary>
  /// <returns>A background-style accessor.</returns>
  public static IHttpContextAccessor Background()
  {
    return new HttpContextAccessor();
  }

  /// <summary>
  /// Creates an accessor for an anonymous demo visitor.
  /// </summary>
  /// <returns>An anonymous accessor.</returns>
  public static IHttpContextAccessor Demo()
  {
    return new HttpContextAccessor
    {
      HttpContext = new DefaultHttpContext(),
    };
  }

  /// <summary>
  /// Creates an accessor for an authenticated user.
  /// </summary>
  /// <param name="userId">The user identifier.</param>
  /// <returns>An authenticated accessor.</returns>
  public static IHttpContextAccessor User(string userId)
  {
    ClaimsIdentity identity = new ClaimsIdentity(
      new[]
      {
        new Claim(ClaimTypes.NameIdentifier, userId),
        new Claim(ClaimTypes.Name, userId),
      },
      "Test");

    return new HttpContextAccessor
    {
      HttpContext = new DefaultHttpContext
      {
        User = new ClaimsPrincipal(identity),
      },
    };
  }

  /// <summary>
  /// Gets the demo owner identifier used by anonymous requests.
  /// </summary>
  public static string DemoUserId
  {
    get
    {
      return LifePlannerContext.DemoUserId;
    }
  }
}
