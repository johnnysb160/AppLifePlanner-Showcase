//-----------------------------------------------------------------------
// <copyright file="WidgetDiagnosticsLogger.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.WindowsWidget;

using System.IO;

/// <summary>
/// Writes lightweight local diagnostics for the Windows widget.
/// </summary>
public static class WidgetDiagnosticsLogger
{
  private const long MaximumLogBytes = 512 * 1024;
  private static readonly object SyncRoot = new object();

  /// <summary>Writes an informational diagnostic entry.</summary>
  /// <param name="message">Message to write.</param>
  public static void Info(string message)
  {
    Write("INFO", message, null);
  }

  /// <summary>Writes an error diagnostic entry.</summary>
  /// <param name="message">Message to write.</param>
  /// <param name="exception">Exception to include.</param>
  public static void Error(string message, Exception exception)
  {
    Write("ERROR", message, exception);
  }

  private static void Write(string level, string message, Exception? exception)
  {
    try
    {
      lock (SyncRoot)
      {
        string directory = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
          "AppLifePlanner",
          "WindowsWidget");
        Directory.CreateDirectory(directory);
        string logPath = Path.Combine(directory, "widget.log");
        RotateIfNeeded(logPath);

        string exceptionText = exception == null
          ? string.Empty
          : $" | {exception.GetType().Name}: {exception.Message}";
        File.AppendAllText(
          logPath,
          $"{DateTimeOffset.Now:O} [{level}] {message}{exceptionText}{Environment.NewLine}");
      }
    }
    catch
    {
      // Diagnostics must never affect the widget runtime.
    }
  }

  private static void RotateIfNeeded(string logPath)
  {
    FileInfo file = new FileInfo(logPath);
    if (!file.Exists || file.Length <= MaximumLogBytes)
    {
      return;
    }

    string backupPath = Path.ChangeExtension(logPath, ".old.log");
    if (File.Exists(backupPath))
    {
      File.Delete(backupPath);
    }

    File.Move(logPath, backupPath);
  }
}
