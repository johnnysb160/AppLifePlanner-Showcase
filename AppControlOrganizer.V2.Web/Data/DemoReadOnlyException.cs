//-----------------------------------------------------------------------
// <copyright file="DemoReadOnlyException.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Data;

/// <summary>
/// Thrown when a write is attempted against the demonstration tenant used by anonymous sessions.
/// The demonstration data is read-only by design: visitors must create a profile to persist changes.
/// </summary>
public class DemoReadOnlyException : InvalidOperationException
{
  /// <summary>Resource key used to localize the message shown to the visitor.</summary>
  public const string MessageKey = "Demo mode is read-only. Create a profile to save your own data.";

  /// <summary>
  /// Initializes a new instance of the <see cref="DemoReadOnlyException"/> class.
  /// </summary>
  public DemoReadOnlyException() : base(MessageKey)
  {
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="DemoReadOnlyException"/> class.
  /// </summary>
  /// <param name="message">Message describing the rejected write.</param>
  public DemoReadOnlyException(string message) : base(message)
  {
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="DemoReadOnlyException"/> class.
  /// </summary>
  /// <param name="message">Message describing the rejected write.</param>
  /// <param name="innerException">Inner exception.</param>
  public DemoReadOnlyException(string message, Exception innerException) : base(message, innerException)
  {
  }
}
