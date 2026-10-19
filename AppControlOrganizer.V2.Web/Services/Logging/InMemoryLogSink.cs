//-----------------------------------------------------------------------
// <copyright file="InMemoryLogSink.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Logging;

using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

/// <summary>
/// Serilog sink that keeps a bounded, in-process buffer of rendered log lines so System Status
/// can show observability data in every environment, including hosts without a writable disk
/// for rolling log files.
/// </summary>
public sealed class InMemoryLogSink : ILogEventSink, IInMemoryLogBuffer
{
  private const int Capacity = 300;
  private readonly ConcurrentQueue<string> lines = new();
  private readonly MessageTemplateTextFormatter formatter = new(
    "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

  /// <inheritdoc />
  public void Emit(LogEvent logEvent)
  {
    using StringWriter writer = new StringWriter();
    this.formatter.Format(logEvent, writer);
    this.lines.Enqueue(writer.ToString().TrimEnd('\r', '\n'));

    while (this.lines.Count > Capacity && this.lines.TryDequeue(out _))
    {
    }
  }

  /// <inheritdoc />
  public IReadOnlyList<string> GetRecentLines()
  {
    return this.lines.ToArray();
  }
}
