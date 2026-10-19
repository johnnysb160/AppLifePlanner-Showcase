//-----------------------------------------------------------------------
// <copyright file="InMemoryLogSinkTests.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Observability;

using AppControlOrganizer.V2.Web.Services.Logging;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

/// <summary>
/// Tests the bounded in-process log buffer used by System Status observability.
/// </summary>
public sealed class InMemoryLogSinkTests
{
  /// <summary>
  /// Emitted events must be readable back as rendered lines, oldest first.
  /// </summary>
  [Fact]
  public void EmitAppendsRenderedLineToBuffer()
  {
    InMemoryLogSink sink = new InMemoryLogSink();

    sink.Emit(CreateEvent("first message"));
    sink.Emit(CreateEvent("second message"));

    IReadOnlyList<string> lines = sink.GetRecentLines();

    Assert.Equal(2, lines.Count);
    Assert.Contains("first message", lines[0], StringComparison.Ordinal);
    Assert.Contains("second message", lines[1], StringComparison.Ordinal);
  }

  /// <summary>
  /// The buffer must stay bounded so memory usage cannot grow without limit.
  /// </summary>
  [Fact]
  public void EmitDropsOldestLineOnceCapacityIsExceeded()
  {
    InMemoryLogSink sink = new InMemoryLogSink();

    for (int index = 0; index < 305; index++)
    {
      sink.Emit(CreateEvent($"message {index}"));
    }

    IReadOnlyList<string> lines = sink.GetRecentLines();

    Assert.Equal(300, lines.Count);
    Assert.Contains("message 5", lines[0], StringComparison.Ordinal);
    Assert.Contains("message 304", lines[^1], StringComparison.Ordinal);
  }

  private static LogEvent CreateEvent(string message)
  {
    MessageTemplateParser parser = new MessageTemplateParser();
    return new LogEvent(
      DateTimeOffset.UtcNow,
      LogEventLevel.Information,
      null,
      parser.Parse(message),
      []);
  }
}
