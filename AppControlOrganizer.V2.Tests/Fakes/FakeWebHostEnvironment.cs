//-----------------------------------------------------------------------
// <copyright file="FakeWebHostEnvironment.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Fakes;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

/// <summary>
/// Test double for the hosting environment, used to exercise environment-gated behavior.
/// </summary>
internal sealed class FakeWebHostEnvironment : IWebHostEnvironment
{
  /// <summary>
  /// Initializes a new instance of the <see cref="FakeWebHostEnvironment"/> class.
  /// </summary>
  /// <param name="environmentName">The environment name, such as Production or Development.</param>
  public FakeWebHostEnvironment(string environmentName)
  {
    this.EnvironmentName = environmentName;
  }

  /// <inheritdoc />
  public string ApplicationName { get; set; } = "AppLifePlanner.Tests";

  /// <inheritdoc />
  public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

  /// <inheritdoc />
  public string ContentRootPath { get; set; } = string.Empty;

  /// <inheritdoc />
  public string EnvironmentName { get; set; }

  /// <inheritdoc />
  public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

  /// <inheritdoc />
  public string WebRootPath { get; set; } = string.Empty;
}
