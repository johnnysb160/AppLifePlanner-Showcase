//-----------------------------------------------------------------------
// <copyright file="WidgetSettingsStore.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.WindowsWidget;

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// Persists widget settings under the current Windows user profile.
/// </summary>
public sealed class WidgetSettingsStore
{
  private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
  {
    WriteIndented = true,
  };

  private readonly string filePath;

  /// <summary>
  /// Initializes a new instance of the <see cref="WidgetSettingsStore"/> class.
  /// </summary>
  public WidgetSettingsStore()
  {
    string root = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "AppLifePlanner",
      "WindowsWidget");
    Directory.CreateDirectory(root);
    this.filePath = Path.Combine(root, "settings.json");
  }

  /// <summary>Loads saved settings or returns defaults.</summary>
  public WidgetSettings Load()
  {
    if (!File.Exists(this.filePath))
    {
      return new WidgetSettings();
    }

    string json = File.ReadAllText(this.filePath);
    return JsonSerializer.Deserialize<WidgetSettings>(json) ?? new WidgetSettings();
  }

  /// <summary>Saves settings for the current Windows user.</summary>
  public void Save(WidgetSettings settings)
  {
    string json = JsonSerializer.Serialize(settings, JsonOptions);
    File.WriteAllText(this.filePath, json);
  }

  /// <summary>Protects a secret using Windows DPAPI for the current user.</summary>
  public static string Protect(string value)
  {
    if (string.IsNullOrEmpty(value))
    {
      return string.Empty;
    }

    byte[] data = Encoding.UTF8.GetBytes(value);
    byte[] protectedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
    return Convert.ToBase64String(protectedData);
  }

  /// <summary>Unprotects a secret using Windows DPAPI for the current user.</summary>
  public static string Unprotect(string value)
  {
    if (string.IsNullOrEmpty(value))
    {
      return string.Empty;
    }

    byte[] protectedData = Convert.FromBase64String(value);
    byte[] data = ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
    return Encoding.UTF8.GetString(data);
  }
}

/// <summary>
/// User-specific widget settings.
/// </summary>
public sealed class WidgetSettings
{
  /// <summary>Default AppLifePlanner hosted URL.</summary>
  public const string DefaultBaseUrl = "https://applifeplanner.onrender.com";

  /// <summary>Gets or sets the AppLifePlanner web base URL.</summary>
  public string BaseUrl { get; set; } = DefaultBaseUrl;

  /// <summary>Gets or sets the DPAPI-protected widget token.</summary>
  public string ProtectedWidgetToken { get; set; } = string.Empty;

  /// <summary>Gets or sets the DPAPI-protected GitHub token used by Velopack for private releases.</summary>
  public string ProtectedGitHubToken { get; set; } = string.Empty;

  /// <summary>Gets or sets the last automatic update check time.</summary>
  public DateTime? LastUpdateCheckUtc { get; set; }

  /// <summary>Gets or sets a value indicating whether activity-change sounds are muted.</summary>
  public bool IsActivitySoundMuted { get; set; }

  /// <summary>Gets or sets the activity-change alert sound kind.</summary>
  public string ActivitySoundKind { get; set; } = "windows";

  /// <summary>Gets or sets the activity-change alert volume percentage.</summary>
  public int ActivitySoundVolumePercent { get; set; } = 70;

  /// <summary>Gets or sets custom card positions and sizes for the local widget.</summary>
  public Dictionary<string, WidgetCardLayout> CardLayouts { get; set; } = new Dictionary<string, WidgetCardLayout>();

  /// <summary>Gets or sets a value indicating whether card coordinates are saved as absolute canvas positions.</summary>
  public bool CardLayoutsUseAbsoluteCoordinates { get; set; }

  /// <summary>Gets or sets the schema version used to persist custom card layouts.</summary>
  public int CardLayoutSchemaVersion { get; set; }

  /// <summary>Gets or sets the local widget window layout.</summary>
  public WidgetWindowLayout? WindowLayout { get; set; }
}

/// <summary>
/// User-specific layout for a movable widget card.
/// </summary>
public sealed class WidgetCardLayout
{
  /// <summary>Gets or sets the horizontal visual offset.</summary>
  public double X { get; set; }

  /// <summary>Gets or sets the vertical visual offset.</summary>
  public double Y { get; set; }

  /// <summary>Gets or sets the manual card width.</summary>
  public double? Width { get; set; }

  /// <summary>Gets or sets the manual card height.</summary>
  public double? Height { get; set; }

  /// <summary>Gets or sets the expanded card height before collapsing.</summary>
  public double? ExpandedHeight { get; set; }

  /// <summary>Gets or sets the expanded card width before collapsing.</summary>
  public double? ExpandedWidth { get; set; }

  /// <summary>Gets or sets a value indicating whether the card is collapsed.</summary>
  public bool IsCollapsed { get; set; }

  /// <summary>Gets or sets the visual stacking order.</summary>
  public int ZIndex { get; set; }
}

/// <summary>
/// User-specific layout for the widget window.
/// </summary>
public sealed class WidgetWindowLayout
{
  /// <summary>Gets or sets the window left coordinate.</summary>
  public double Left { get; set; }

  /// <summary>Gets or sets the window top coordinate.</summary>
  public double Top { get; set; }

  /// <summary>Gets or sets the window width.</summary>
  public double Width { get; set; }

  /// <summary>Gets or sets the window height.</summary>
  public double Height { get; set; }
}
