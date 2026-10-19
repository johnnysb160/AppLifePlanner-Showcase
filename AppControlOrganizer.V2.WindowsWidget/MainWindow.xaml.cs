//-----------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.WindowsWidget;

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AppControlOrganizer.V2.Shared.DTOs;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;
using Forms = System.Windows.Forms;

/// <summary>
/// Main always-on-top widget window.
/// </summary>
public partial class MainWindow : Window
{
  private const double CompactLayoutMaxHeight = 470;
  private const double TinyLayoutMaxHeight = 390;
  private const double WideLayoutMinWidth = 1120;
  private const int ExtendedWindowStyleIndex = -20;
  private const int AppWindowStyle = 0x00040000;
  private const int ToolWindowStyle = 0x00000080;
  private const int DwmWindowCornerPreferenceAttribute = 33;
  private const int DwmSystemBackdropTypeAttribute = 38;
  private const int DwmRoundedCornerPreference = 2;
  private const int DwmAcrylicBackdropType = 3;
  private const int WindowIconSmall = 0;
  private const int WindowIconBig = 1;
  private const int WindowSetIconMessage = 0x0080;
  private const double CardDragThreshold = 4;
  private const double CardResizeZoneSize = 42;
  private const double CollapsedCardHeight = 38;
  private const string InsightCardKeyPrefix = "insight:";
  private const string AppUserModelId = "AppLifePlanner.WindowsWidget";
  private const string ActivitySoundWindows = "windows";
  private const string ActivitySoundSoftChime = "soft-chime";
  private const string ActivitySoundDoublePulse = "double-pulse";
  private const string ActivitySoundFocusBell = "focus-bell";
  private const string ActivitySoundBrightPing = "bright-ping";
  private const string ActivitySoundReadActivityLegacy = "read-activity";
  private const string ActivitySoundReadActivityPortuguese = "read-activity-pt";
  private const string ActivitySoundReadActivityEnglish = "read-activity-en";
  private const string ActivitySoundReadActivityVoicePrefix = "read-activity-voice|";
  private const string ActivitySoundReadActivityWindowsVoicePrefix = "read-activity-windows-voice|";
  private static readonly string[] PortugueseVoiceLanguages = { "416", "816" };
  private static readonly string[] EnglishVoiceLanguages = { "409", "809", "C09", "1009" };
  private static readonly string[] PortugueseVoiceCultureNames = { "pt-BR", "pt-PT" };
  private static readonly string[] EnglishVoiceCultureNames = { "en-US", "en-GB", "en-AU", "en-CA", "en-IN" };
  private static readonly Dictionary<string, string> EnglishActivityTranslations = new Dictionary<string, string>(
    StringComparer.OrdinalIgnoreCase)
  {
    { "Academia", "Gym" },
    { "Acordar", "Wake up" },
    { "Almoço", "Lunch" },
    { "Almoco", "Lunch" },
    { "Bicicleta", "Bike ride" },
    { "Café", "Coffee" },
    { "Cafe", "Coffee" },
    { "Caixa", "Cash" },
    { "Curso", "Course" },
    { "Descanso", "Rest" },
    { "Desenvolvimento", "Development" },
    { "Espaço", "Space" },
    { "Espaco", "Space" },
    { "Estudo", "Study" },
    { "Foco", "Focus" },
    { "Livre", "free" },
    { "Pessoal", "Personal" },
    { "Planejamento", "Planning" },
    { "Reserva", "Reserve" },
    { "Rotina", "Routine" },
    { "Saúde", "Health" },
    { "Saude", "Health" },
    { "Trabalho", "Work" },
  };

  private readonly WidgetClient client = new WidgetClient();
  private readonly Dictionary<string, MovableWidgetCard> movableCards = new Dictionary<string, MovableWidgetCard>(
    StringComparer.Ordinal);
  private readonly HashSet<string> liveInsightCardKeys = new HashSet<string>(StringComparer.Ordinal);
  private readonly WidgetSettingsStore settingsStore = new WidgetSettingsStore();
  private readonly CancellationTokenSource lifetimeCancellation = new CancellationTokenSource();
  private readonly SemaphoreSlim refreshGate = new SemaphoreSlim(1, 1);
  private readonly SemaphoreSlim updateGate = new SemaphoreSlim(1, 1);
  private readonly MediaPlayer activitySoundPlayer = new MediaPlayer();
  private readonly Dictionary<string, string> activitySoundFiles = new Dictionary<string, string>(StringComparer.Ordinal);
  private Forms.NotifyIcon? notifyIcon;
  private System.Drawing.Icon? taskbarLargeIcon;
  private System.Drawing.Icon? taskbarSmallIcon;
  private System.Drawing.Icon? trayIcon;
  private TodayWidgetSnapshotDto? currentSnapshot;
  private WidgetSettings settings = new WidgetSettings();
  private string widgetToken = string.Empty;
  private string currentActivitySignature = string.Empty;
  private int currentProgressPercent;
  private int currentTopCardZIndex = 10;
  private int refreshButtonBusyDepth;
  private int savedCardLayoutApplyCount;
  private bool pendingProgressLayoutRefresh;
  private bool requiresWidgetPairing = true;
  private bool cardsHostedInCanvas;
  private bool hasUnsavedLayoutChanges;
  private bool layoutChangeTrackingEnabled;
  private bool restoredWindowLayout;
  private bool suppressLayoutChangeTracking;
  private MovableWidgetCard? activeMoveCard;
  private MovableWidgetCard? activeResizeCard;
  private Point activeMoveStartPoint;
  private Point activeResizeStartPoint;
  private double activeMoveStartX;
  private double activeMoveStartY;
  private double activeResizeStartHeight;
  private double activeResizeStartLeft;
  private double activeResizeStartTop;
  private double activeResizeStartWidth;
  private double activeResizeTotalHorizontalChange;
  private double activeResizeTotalVerticalChange;
  private bool activeMoveExceededThreshold;
  private bool pendingActivitySoundVolumePreview;
  private bool isApplyingSoundSettings;
  private const int CardLayoutSchemaVersion = 4;

  /// <summary>
  /// Initializes a new instance of the <see cref="MainWindow"/> class.
  /// </summary>
  public MainWindow()
  {
    ConfigureAppIdentity();
    this.InitializeComponent();
    this.ConfigureMovableCards();
    this.ApplyWindowIcon();
    this.settings = this.settingsStore.Load();
    this.NormalizeSoundSettings();
    this.ConfigureSoundControls();
    this.SanitizeLoadedCardLayouts();
    this.ApplySavedWindowLayout();
    this.BaseUrlTextBox.Text = this.settings.BaseUrl;
    this.widgetToken = WidgetSettingsStore.Unprotect(this.settings.ProtectedWidgetToken);
    this.UpdateSoundToggleButton();
    this.ConfigureTrayIcon();
    this.ConfigureRefreshTimer();
    this.ConfigureUpdateTimer();
    this.Loaded += this.OnLoadedAsync;
    this.SourceInitialized += this.OnSourceInitialized;
    this.LocationChanged += this.OnWindowLayoutChanged;
    this.SizeChanged += this.OnWindowSizeChanged;
    this.StateChanged += this.OnWindowStateChanged;
  }

  /// <inheritdoc />
  protected override void OnClosed(EventArgs e)
  {
    this.lifetimeCancellation.Cancel();
    this.lifetimeCancellation.Dispose();
    this.refreshGate.Dispose();
    this.updateGate.Dispose();
    this.notifyIcon?.Dispose();
    this.taskbarLargeIcon?.Dispose();
    this.taskbarSmallIcon?.Dispose();
    this.trayIcon?.Dispose();
    this.activitySoundPlayer.Close();
    base.OnClosed(e);
  }

  private async void OnLoadedAsync(object sender, RoutedEventArgs e)
  {
    if (string.IsNullOrWhiteSpace(this.widgetToken))
    {
      this.ShowPairingPanel(
        true,
        "Parear widget",
        "Cole o código do Perfil e o token GitHub uma vez.");
      this.EnableLayoutChangeTracking();
      return;
    }

    this.ShowSnapshotPanel();
    await this.RefreshAsync("startup");
    if (this.HasGitHubUpdateToken())
    {
      _ = this.CheckForUpdatesAsync(true, false);
    }
    else
    {
      this.ShowUpdateStatus("Atualizações desativadas", true);
    }

    this.EnableLayoutChangeTracking();
  }

  private void EnableLayoutChangeTracking()
  {
    this.Dispatcher.BeginInvoke(
      () =>
      {
        this.layoutChangeTrackingEnabled = true;
        this.SetLayoutDirty(false);
      },
      DispatcherPriority.ContextIdle);
  }

  private void ConfigureRefreshTimer()
  {
    _ = this.RunRefreshLoopAsync(this.lifetimeCancellation.Token);
  }

  private void ConfigureUpdateTimer()
  {
    _ = this.RunUpdateLoopAsync(this.lifetimeCancellation.Token);
  }

  private void ConfigureSoundControls()
  {
    this.isApplyingSoundSettings = true;
    List<SapiVoiceOption> voiceOptions = GetSapiVoiceOptions();
    List<WindowsVoiceOption> windowsVoiceOptions = GetWindowsVoiceOptions();
    this.ActivitySoundKindComboBox.Items.Clear();
    this.ActivitySoundKindComboBox.Items.Add(CreateSoundOption("Windows", ActivitySoundWindows));
    this.ActivitySoundKindComboBox.Items.Add(CreateSoundOption("Sino leve", ActivitySoundSoftChime));
    this.ActivitySoundKindComboBox.Items.Add(CreateSoundOption("Pulso duplo", ActivitySoundDoublePulse));
    this.ActivitySoundKindComboBox.Items.Add(CreateSoundOption("Alerta foco", ActivitySoundFocusBell));
    this.ActivitySoundKindComboBox.Items.Add(CreateSoundOption("Brilho curto", ActivitySoundBrightPing));
    this.AddVoiceSoundOptions(voiceOptions, windowsVoiceOptions);

    this.ActivitySoundEnabledCheckBox.IsChecked = !this.settings.IsActivitySoundMuted;
    this.ActivitySoundVolumeSlider.Value = this.settings.ActivitySoundVolumePercent;
    this.SelectSoundKind(this.ResolveSelectableSoundKind(this.settings.ActivitySoundKind, voiceOptions, windowsVoiceOptions));
    this.UpdateActivitySoundVolumeText();
    this.isApplyingSoundSettings = false;
  }

  private void AddVoiceSoundOptions(List<SapiVoiceOption> voiceOptions, List<WindowsVoiceOption> windowsVoiceOptions)
  {
    int portugueseVoiceCount = this.AddLanguageVoiceSoundOptions(
      voiceOptions,
      windowsVoiceOptions,
      PortugueseVoiceLanguages,
      PortugueseVoiceCultureNames,
      "PT");
    if (portugueseVoiceCount == 0)
    {
      this.ActivitySoundKindComboBox.Items.Add(CreateSoundOption(
        "Ler em PT (voz pt-BR ausente)",
        ActivitySoundReadActivityPortuguese));
    }
    int englishVoiceCount = this.AddLanguageVoiceSoundOptions(
      voiceOptions,
      windowsVoiceOptions,
      EnglishVoiceLanguages,
      EnglishVoiceCultureNames,
      "IN");
    if (englishVoiceCount == 0)
    {
      this.ActivitySoundKindComboBox.Items.Add(CreateSoundOption(
        "Ler em IN (voz en-US ausente)",
        ActivitySoundReadActivityEnglish));
    }
  }

  private int AddLanguageVoiceSoundOptions(
    List<SapiVoiceOption> voiceOptions,
    List<WindowsVoiceOption> windowsVoiceOptions,
    string[] languageIds,
    string[] cultureNames,
    string languageLabel)
  {
    List<SapiVoiceOption> matchingVoices = new List<SapiVoiceOption>();
    HashSet<string> addedVoiceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (SapiVoiceOption voiceOption in voiceOptions)
    {
      if (IsLanguageMatch(voiceOption.LanguageId, languageIds))
      {
        matchingVoices.Add(voiceOption);
        addedVoiceNames.Add(voiceOption.DisplayName);
      }
    }

    foreach (SapiVoiceOption voiceOption in matchingVoices)
    {
      this.ActivitySoundKindComboBox.Items.Add(CreateSoundOption(
        $"Ler em {languageLabel} - {voiceOption.DisplayName}",
        CreateVoiceSoundKind(voiceOption)));
    }

    int addedVoiceCount = matchingVoices.Count;
    foreach (WindowsVoiceOption voiceOption in windowsVoiceOptions)
    {
      if (IsCultureMatch(voiceOption.Language, cultureNames) && addedVoiceNames.Add(voiceOption.DisplayName))
      {
        addedVoiceCount++;
        this.ActivitySoundKindComboBox.Items.Add(CreateSoundOption(
          $"Ler em {languageLabel} - {voiceOption.DisplayName}",
          CreateWindowsVoiceSoundKind(voiceOption)));
      }
    }

    return addedVoiceCount;
  }

  private static ComboBoxItem CreateSoundOption(string label, string value)
  {
    return new ComboBoxItem
    {
      Content = label,
      Tag = value,
    };
  }

  private void SelectSoundKind(string soundKind)
  {
    foreach (object item in this.ActivitySoundKindComboBox.Items)
    {
      ComboBoxItem? option = item as ComboBoxItem;
      if (option != null && string.Equals(option.Tag as string, soundKind, StringComparison.Ordinal))
      {
        this.ActivitySoundKindComboBox.SelectedItem = option;
        return;
      }
    }

    this.ActivitySoundKindComboBox.SelectedIndex = 0;
  }

  private void NormalizeSoundSettings()
  {
    if (string.Equals(this.settings.ActivitySoundKind, ActivitySoundReadActivityLegacy, StringComparison.Ordinal))
    {
      this.settings.ActivitySoundKind = ActivitySoundReadActivityPortuguese;
    }

    if (!IsSupportedActivitySoundKind(this.settings.ActivitySoundKind))
    {
      this.settings.ActivitySoundKind = ActivitySoundWindows;
    }

    this.settings.ActivitySoundVolumePercent = Math.Clamp(this.settings.ActivitySoundVolumePercent, 0, 100);
  }

  private static bool IsSupportedActivitySoundKind(string soundKind)
  {
    return string.Equals(soundKind, ActivitySoundWindows, StringComparison.Ordinal)
      || string.Equals(soundKind, ActivitySoundSoftChime, StringComparison.Ordinal)
      || string.Equals(soundKind, ActivitySoundDoublePulse, StringComparison.Ordinal)
      || string.Equals(soundKind, ActivitySoundFocusBell, StringComparison.Ordinal)
      || string.Equals(soundKind, ActivitySoundBrightPing, StringComparison.Ordinal)
      || string.Equals(soundKind, ActivitySoundReadActivityPortuguese, StringComparison.Ordinal)
      || string.Equals(soundKind, ActivitySoundReadActivityEnglish, StringComparison.Ordinal)
      || TryParseVoiceSoundKind(soundKind, out _, out _)
      || TryParseWindowsVoiceSoundKind(soundKind, out _, out _);
  }

  private string ResolveSelectableSoundKind(
    string soundKind,
    List<SapiVoiceOption> voiceOptions,
    List<WindowsVoiceOption> windowsVoiceOptions)
  {
    if (string.Equals(soundKind, ActivitySoundReadActivityPortuguese, StringComparison.Ordinal))
    {
      return FindFirstVoiceSoundKind(voiceOptions, PortugueseVoiceLanguages)
        ?? FindFirstWindowsVoiceSoundKind(windowsVoiceOptions, PortugueseVoiceCultureNames)
        ?? soundKind;
    }

    if (string.Equals(soundKind, ActivitySoundReadActivityEnglish, StringComparison.Ordinal))
    {
      return FindFirstVoiceSoundKind(voiceOptions, EnglishVoiceLanguages)
        ?? FindFirstWindowsVoiceSoundKind(windowsVoiceOptions, EnglishVoiceCultureNames)
        ?? soundKind;
    }

    if (TryParseVoiceSoundKind(soundKind, out string? languageId, out string? tokenId)
      && !HasVoiceOption(voiceOptions, languageId, tokenId))
    {
      if (IsLanguageMatch(languageId, PortugueseVoiceLanguages))
      {
        return FindFirstVoiceSoundKind(voiceOptions, PortugueseVoiceLanguages)
          ?? FindFirstWindowsVoiceSoundKind(windowsVoiceOptions, PortugueseVoiceCultureNames)
          ?? ActivitySoundReadActivityPortuguese;
      }

      if (IsLanguageMatch(languageId, EnglishVoiceLanguages))
      {
        return FindFirstVoiceSoundKind(voiceOptions, EnglishVoiceLanguages)
          ?? FindFirstWindowsVoiceSoundKind(windowsVoiceOptions, EnglishVoiceCultureNames)
          ?? ActivitySoundReadActivityEnglish;
      }

      return ActivitySoundWindows;
    }

    if (TryParseWindowsVoiceSoundKind(soundKind, out string? cultureName, out string? voiceId)
      && !HasWindowsVoiceOption(windowsVoiceOptions, cultureName, voiceId))
    {
      if (IsCultureMatch(cultureName, PortugueseVoiceCultureNames))
      {
        return FindFirstWindowsVoiceSoundKind(windowsVoiceOptions, PortugueseVoiceCultureNames)
          ?? FindFirstVoiceSoundKind(voiceOptions, PortugueseVoiceLanguages)
          ?? ActivitySoundReadActivityPortuguese;
      }

      if (IsCultureMatch(cultureName, EnglishVoiceCultureNames))
      {
        return FindFirstWindowsVoiceSoundKind(windowsVoiceOptions, EnglishVoiceCultureNames)
          ?? FindFirstVoiceSoundKind(voiceOptions, EnglishVoiceLanguages)
          ?? ActivitySoundReadActivityEnglish;
      }

      return ActivitySoundWindows;
    }

    return soundKind;
  }

  private void ConfigureTrayIcon()
  {
    this.notifyIcon = new Forms.NotifyIcon
    {
      Icon = this.LoadTrayIcon(),
      Text = "Widget AppLifePlanner",
      Visible = true,
      ContextMenuStrip = new Forms.ContextMenuStrip(),
    };

    this.notifyIcon.ContextMenuStrip.Items.Add("Mostrar", null, (_, _) => this.ShowFromTray());
    this.notifyIcon.ContextMenuStrip.Items.Add("Atualizar", null, async (_, _) => await this.RefreshAsync("tray"));
    this.notifyIcon.ContextMenuStrip.Items.Add("Mutar alertas", null, (_, _) => this.ToggleActivitySound());
    this.notifyIcon.ContextMenuStrip.Items.Add("Testar alerta", null, (_, _) => this.PlayActivitySoundPreview());
    this.notifyIcon.ContextMenuStrip.Items.Add("Redefinir pareamento", null, (_, _) => this.ResetLocalPairing());
    this.notifyIcon.ContextMenuStrip.Items.Add("Sair", null, (_, _) => this.Close());
    this.notifyIcon.DoubleClick += (_, _) => this.ShowFromTray();
  }

  private System.Drawing.Icon LoadTrayIcon()
  {
    string iconPath = GetWidgetIconPath();
    if (File.Exists(iconPath))
    {
      this.trayIcon = CreateIconFromImage(iconPath);
      return this.trayIcon;
    }

    string? executablePath = Process.GetCurrentProcess().MainModule?.FileName;
    if (!string.IsNullOrWhiteSpace(executablePath))
    {
      this.trayIcon = System.Drawing.Icon.ExtractAssociatedIcon(executablePath);
      if (this.trayIcon != null)
      {
        return this.trayIcon;
      }
    }

    return System.Drawing.SystemIcons.Application;
  }

  private static string GetWidgetIconPath()
  {
    return Path.Combine(
      AppContext.BaseDirectory,
      "Resources",
      "AppLifePlannerWidget.png");
  }

  private void ApplyWindowIcon()
  {
    string iconPath = GetWidgetIconPath();
    if (!File.Exists(iconPath))
    {
      return;
    }

    try
    {
      BitmapImage image = new BitmapImage();
      image.BeginInit();
      image.CacheOption = BitmapCacheOption.OnLoad;
      image.UriSource = new Uri(iconPath, UriKind.Absolute);
      image.EndInit();
      image.Freeze();
      this.Icon = image;
    }
    catch
    {
      this.Icon = null;
    }
  }

  private static void ConfigureAppIdentity()
  {
    try
    {
      SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
    }
    catch (DllNotFoundException)
    {
    }
    catch (EntryPointNotFoundException)
    {
    }
  }

  private static System.Drawing.Icon CreateIconFromImage(string imagePath)
  {
    return CreateIconFromImage(imagePath, 32);
  }

  private static System.Drawing.Icon CreateIconFromImage(string imagePath, int size)
  {
    using System.Drawing.Bitmap source = new System.Drawing.Bitmap(imagePath);
    using System.Drawing.Bitmap scaled = new System.Drawing.Bitmap(size, size);
    using System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(scaled);
    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
    graphics.Clear(System.Drawing.Color.Transparent);
    graphics.DrawImage(
      source,
      new System.Drawing.Rectangle(0, 0, size, size),
      new System.Drawing.Rectangle(0, 0, source.Width, source.Height),
      System.Drawing.GraphicsUnit.Pixel);

    IntPtr iconHandle = scaled.GetHicon();
    try
    {
      using System.Drawing.Icon temporaryIcon = System.Drawing.Icon.FromHandle(iconHandle);
      return (System.Drawing.Icon)temporaryIcon.Clone();
    }
    finally
    {
      DestroyIcon(iconHandle);
    }
  }

  [DllImport("user32.dll", SetLastError = true)]
  private static extern bool DestroyIcon(IntPtr handle);

  [DllImport("shell32.dll", SetLastError = true)]
  private static extern int SetCurrentProcessExplicitAppUserModelID(
    [MarshalAs(UnmanagedType.LPWStr)] string appId);

  [DllImport("user32.dll", SetLastError = true)]
  private static extern int GetWindowLong(IntPtr handle, int index);

  [DllImport("user32.dll", SetLastError = true)]
  private static extern int SetWindowLong(IntPtr handle, int index, int value);

  [DllImport("user32.dll", SetLastError = true)]
  private static extern IntPtr SendMessage(
    IntPtr handle,
    int message,
    IntPtr wordParameter,
    IntPtr longParameter);

  [DllImport("dwmapi.dll", SetLastError = true)]
  private static extern int DwmSetWindowAttribute(
    IntPtr handle,
    int attribute,
    ref int attributeValue,
    int attributeSize);

  private void ShowFromTray()
  {
    this.ShowInTaskbar = true;
    this.Show();
    this.WindowState = WindowState.Normal;
    this.Activate();
    this.EnsureTaskbarPresence();
  }

  private void ShowPairingPanel(
    bool requireWidgetPairing,
    string title,
    string description)
  {
    this.requiresWidgetPairing = requireWidgetPairing;
    this.PairingPanel.Visibility = Visibility.Visible;
    this.SnapshotPanel.Visibility = Visibility.Collapsed;
    this.HeaderText.Text = requireWidgetPairing ? "Parear" : "Atualização";
    this.PairingTitleText.Text = title;
    this.PairingDescriptionText.Text = description;
    this.PairingCodeTextBox.Visibility = requireWidgetPairing ? Visibility.Visible : Visibility.Collapsed;
    this.PairButton.Content = requireWidgetPairing ? "Parear" : "Salvar token";
  }

  private void ShowSnapshotPanel()
  {
    this.PairingPanel.Visibility = Visibility.Collapsed;
    this.SnapshotPanel.Visibility = Visibility.Visible;
    if (!this.HasGitHubUpdateToken())
    {
      this.ShowUpdateStatus("Atualizações desativadas", true);
    }

    this.ApplyResponsiveLayout();
    this.ScheduleSavedCardLayoutApply();
  }

  private void ApplySavedWindowLayout()
  {
    WidgetWindowLayout? layout = this.settings.WindowLayout;
    if (layout == null)
    {
      return;
    }

    if (layout.Width >= this.MinWidth)
    {
      this.Width = layout.Width;
    }

    if (layout.Height >= this.MinHeight)
    {
      this.Height = layout.Height;
    }

    if (this.IsValidWindowPosition(layout.Left, layout.Top, layout.Width, layout.Height))
    {
      this.Left = layout.Left;
      this.Top = layout.Top;
      this.WindowStartupLocation = WindowStartupLocation.Manual;
    }

    this.restoredWindowLayout = true;
  }

  private void ConfigureMovableCards()
  {
    this.RegisterMovableCard("moments", this.HeroCard, 260, 118, 560, 184);
    this.RegisterMovableCard("next", this.NextButton, 220, 54, 360, 78);
    this.RegisterMovableCard("timeline", this.TimelineCard, 260, 96, 560, 276);
    this.RegisterMovableCard("focus", this.FocusCard, 160, 70, 500, 126);
    this.RegisterMovableCard("actions", this.ActionsCard, 180, 70, 500, 126);
  }

  private void RegisterMovableCard(
    string key,
    FrameworkElement element,
    double minWidth,
    double minHeight,
    double defaultWidth,
    double defaultHeight)
  {
    TranslateTransform transform = new TranslateTransform();
    element.RenderTransform = transform;
    element.RenderTransformOrigin = new Point(0, 0);
    element.MinWidth = Math.Max(element.MinWidth, minWidth);
    element.MinHeight = Math.Max(element.MinHeight, minHeight);
    MovableWidgetCard card = new MovableWidgetCard(
      key,
      element,
      transform,
      minWidth,
      minHeight,
      defaultWidth,
      defaultHeight);
    this.movableCards[key] = card;
    element.AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(this.OnMovableCardMouseLeftButtonDown), true);
    element.AddHandler(MouseMoveEvent, new MouseEventHandler(this.OnMovableCardMouseMove), true);
    element.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(this.OnMovableCardMouseLeftButtonUp), true);
  }

  private void ApplySavedCardLayouts()
  {
    this.HostMovableCardsInCanvas();
    if (this.settings.CardLayouts.Count == 0)
    {
      return;
    }

    bool shouldConvertLegacyLayout = !this.settings.CardLayoutsUseAbsoluteCoordinates;
    if (!shouldConvertLegacyLayout && this.HasCorruptedSavedCardLayout())
    {
      this.settings.CardLayouts.Clear();
      this.settingsStore.Save(this.settings);
      return;
    }

    bool appliedAnyLayout = false;
    foreach (KeyValuePair<string, WidgetCardLayout> savedLayout in this.settings.CardLayouts)
    {
      if (!this.movableCards.TryGetValue(savedLayout.Key, out MovableWidgetCard? card))
      {
        continue;
      }

      this.ApplySavedLayoutForCard(card);
      appliedAnyLayout = true;
    }

    if (shouldConvertLegacyLayout && appliedAnyLayout)
    {
      this.CaptureAllCardLayouts();
      this.settingsStore.Save(this.settings);
    }
  }

  private void SanitizeLoadedCardLayouts()
  {
    if (this.settings.CardLayoutSchemaVersion >= CardLayoutSchemaVersion
      && !this.HasCorruptedSavedCardLayout())
    {
      return;
    }

    this.settings.CardLayouts.Clear();
    this.settings.CardLayoutsUseAbsoluteCoordinates = true;
    this.settings.CardLayoutSchemaVersion = CardLayoutSchemaVersion;
    this.settingsStore.Save(this.settings);
  }

  private void SaveLayout()
  {
    this.CaptureAllCardLayouts();
    this.CaptureWindowLayout();
    this.settingsStore.Save(this.settings);
    this.SetLayoutDirty(false);
  }

  private void RestoreSavedLayout()
  {
    this.suppressLayoutChangeTracking = true;
    try
    {
      this.settings = this.settingsStore.Load();
      this.ApplySavedWindowLayout();
      this.ApplySavedCardLayouts();
      this.SetLayoutDirty(false);
    }
    finally
    {
      this.suppressLayoutChangeTracking = false;
    }
  }

  private bool HasCorruptedSavedCardLayout()
  {
    foreach (WidgetCardLayout layout in this.settings.CardLayouts.Values)
    {
      if (layout.X < 0 || layout.Y < 0)
      {
        return true;
      }
    }

    return false;
  }

  private void ApplySavedLayoutForCard(MovableWidgetCard card)
  {
    if (!this.settings.CardLayouts.TryGetValue(card.Key, out WidgetCardLayout? savedLayout))
    {
      return;
    }

    bool hasExplicitBounds = false;
    if (savedLayout.Width is double width && width >= card.MinWidth)
    {
      card.Element.Width = this.SanitizeCardWidth(card, width);
      hasExplicitBounds = true;
    }

    if (savedLayout.Height is double height && height >= card.MinHeight)
    {
      card.Element.Height = this.SanitizeCardHeight(card, height);
      hasExplicitBounds = true;
    }

    card.ExpandedHeight = savedLayout.ExpandedHeight;
    card.ExpandedWidth = savedLayout.ExpandedWidth;
    card.IsCollapsed = savedLayout.IsCollapsed;
    if (hasExplicitBounds)
    {
      PrepareCardForExplicitBounds(card);
    }

    if (card.IsCollapsed)
    {
      this.ApplyCollapsedState(card);
    }
    else
    {
      RestoreExpandedContent(card);
    }
    if (savedLayout.ZIndex > 0)
    {
      this.ApplyCardZIndex(card, savedLayout.ZIndex);
    }

    if (this.settings.CardLayoutsUseAbsoluteCoordinates)
    {
      if (savedLayout.X < 0 || savedLayout.Y < 0)
      {
        this.settings.CardLayouts.Remove(card.Key);
        return;
      }

      Point constrainedPosition = this.ConstrainCardPosition(card, savedLayout.X, savedLayout.Y);
      Canvas.SetLeft(card.Element, constrainedPosition.X);
      Canvas.SetTop(card.Element, constrainedPosition.Y);
      card.Transform.X = 0;
      card.Transform.Y = 0;
    }
    else
    {
      Point constrainedOffset = this.ConstrainCardOffset(card, savedLayout.X, savedLayout.Y);
      card.Transform.X = constrainedOffset.X;
      card.Transform.Y = constrainedOffset.Y;
    }
  }

  private void ScheduleSavedCardLayoutApply()
  {
    if (this.savedCardLayoutApplyCount >= 3)
    {
      return;
    }

    this.savedCardLayoutApplyCount++;
    this.Dispatcher.BeginInvoke(
      () =>
      {
        this.ApplySavedCardLayouts();
        this.ScheduleSavedCardLayoutApply();
      },
      DispatcherPriority.ContextIdle);
  }

  private void HostMovableCardsInCanvas()
  {
    if (this.cardsHostedInCanvas || this.SnapshotPanel.ActualWidth <= 0)
    {
      return;
    }

    this.SnapshotPanel.UpdateLayout();
    foreach (MovableWidgetCard card in this.movableCards.Values)
    {
      this.HostMovableCardInCanvas(card);
    }

    this.AdaptiveCardsGrid.Visibility = Visibility.Collapsed;
    this.cardsHostedInCanvas = true;
  }

  private void HostMovableCardInCanvas(MovableWidgetCard card)
  {
    Point position = card.Element.TransformToAncestor(this.SnapshotPanel).Transform(new Point(0, 0));
    double width = this.SanitizeCardWidth(card, GetCardWidth(card));
    double height = this.SanitizeCardHeight(card, GetCardHeight(card));
    RemoveFromParent(card.Element);
    Canvas.SetLeft(card.Element, position.X);
    Canvas.SetTop(card.Element, position.Y);
    card.Element.Width = width;
    card.Element.Height = height;
    PrepareCardForExplicitBounds(card);
    this.MovableCardsCanvas.Children.Add(card.Element);
  }

  private static void RemoveFromParent(FrameworkElement element)
  {
    if (element.Parent is Panel panel)
    {
      panel.Children.Remove(element);
    }
    else if (element.Parent is Decorator decorator)
    {
      decorator.Child = null;
    }
    else if (element.Parent is ContentControl contentControl)
    {
      contentControl.Content = null;
    }
  }

  private void OnMovableCardMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
  {
    if (sender is not FrameworkElement element
      || this.IsInsideResizeGrip(e.OriginalSource as DependencyObject))
    {
      return;
    }

    MovableWidgetCard? card = this.FindMovableCard(element);
    if (card == null)
    {
      return;
    }

    if (this.IsInsideNestedButton(card, e.OriginalSource as DependencyObject))
    {
      return;
    }

    if (IsInsideCardResizeZone(card, e.GetPosition(card.Element)))
    {
      this.BeginCardResize(card, e.GetPosition(this.SnapshotPanel), true);
      e.Handled = true;
      return;
    }

    if (e.ClickCount == 2)
    {
      this.ToggleCardCollapsed(card);
      e.Handled = true;
      return;
    }

    Point activeMoveStartPosition = this.GetCardVisualPosition(card);
    this.activeMoveCard = card;
    this.activeMoveStartPoint = e.GetPosition(this.SnapshotPanel);
    this.activeMoveStartX = activeMoveStartPosition.X;
    this.activeMoveStartY = activeMoveStartPosition.Y;
    this.activeMoveExceededThreshold = false;
    card.Element.CaptureMouse();
  }

  private void OnMovableCardMouseMove(object sender, MouseEventArgs e)
  {
    if (this.activeResizeCard != null && e.LeftButton == MouseButtonState.Pressed)
    {
      this.UpdateActiveCardResize(e.GetPosition(this.SnapshotPanel));
      e.Handled = true;
      return;
    }

    if (this.activeMoveCard == null || e.LeftButton != MouseButtonState.Pressed)
    {
      return;
    }

    Point currentPoint = e.GetPosition(this.SnapshotPanel);
    Vector delta = currentPoint - this.activeMoveStartPoint;
    if (!this.activeMoveExceededThreshold
      && Math.Abs(delta.X) < CardDragThreshold
      && Math.Abs(delta.Y) < CardDragThreshold)
    {
      return;
    }

    this.activeMoveExceededThreshold = true;
    this.MaterializeCardBounds(this.activeMoveCard);
    this.BringCardToFront(this.activeMoveCard);
    Point constrainedPosition = this.ConstrainCardPosition(
      this.activeMoveCard,
      this.activeMoveStartX + delta.X,
      this.activeMoveStartY + delta.Y);
    Canvas.SetLeft(this.activeMoveCard.Element, constrainedPosition.X);
    Canvas.SetTop(this.activeMoveCard.Element, constrainedPosition.Y);
    this.activeMoveCard.Transform.X = 0;
    this.activeMoveCard.Transform.Y = 0;
    e.Handled = true;
  }

  private void OnMovableCardMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
  {
    if (this.activeResizeCard != null)
    {
      MovableWidgetCard resizedCard = this.activeResizeCard;
      resizedCard.Element.ReleaseMouseCapture();
      this.activeResizeCard = null;
      this.SaveCardLayout(resizedCard);
      e.Handled = true;
      return;
    }

    if (this.activeMoveCard == null)
    {
      return;
    }

    MovableWidgetCard movedCard = this.activeMoveCard;
    movedCard.Element.ReleaseMouseCapture();
    this.activeMoveCard = null;
    if (this.activeMoveExceededThreshold)
    {
      this.SaveCardLayout(movedCard);
      e.Handled = true;
    }
  }

  private void OnCardResizeDragStarted(object sender, DragStartedEventArgs e)
  {
    if (sender is not Thumb thumb
      || thumb.Tag is not string key
      || !this.movableCards.TryGetValue(key, out MovableWidgetCard? card))
    {
      return;
    }

    this.BeginCardResize(card, Mouse.GetPosition(this.SnapshotPanel), false);
    e.Handled = true;
  }

  private void OnCardResizeDragDelta(object sender, DragDeltaEventArgs e)
  {
    if (sender is not Thumb thumb
      || thumb.Tag is not string key
      || !this.movableCards.TryGetValue(key, out MovableWidgetCard? card))
    {
      return;
    }

    if (this.activeResizeCard != card)
    {
      this.BeginCardResize(card, new Point(0, 0), false);
    }

    this.activeResizeTotalHorizontalChange += e.HorizontalChange;
    this.activeResizeTotalVerticalChange += e.VerticalChange;
    Size constrainedSize = this.ApplyCardResize(
      card,
      this.activeResizeStartWidth + this.activeResizeTotalHorizontalChange,
      this.activeResizeStartHeight + this.activeResizeTotalVerticalChange,
      this.activeResizeStartLeft,
      this.activeResizeStartTop);
    card.Element.UpdateLayout();
    this.RefreshAdaptiveCardContent();
    this.SaveCardLayout(card);
  }

  private void OnCardResizeDragCompleted(object sender, DragCompletedEventArgs e)
  {
    if (sender is not Thumb thumb
      || thumb.Tag is not string key
      || !this.movableCards.TryGetValue(key, out MovableWidgetCard? card))
    {
      return;
    }

    this.activeResizeCard = null;
    this.activeResizeTotalHorizontalChange = 0;
    this.activeResizeTotalVerticalChange = 0;
    this.SaveCardLayout(card);
    e.Handled = true;
  }

  private void BeginCardResize(MovableWidgetCard card, Point startPoint, bool captureMouse)
  {
    if (this.activeMoveCard != null)
    {
      this.activeMoveCard.Element.ReleaseMouseCapture();
      this.activeMoveCard = null;
      this.activeMoveExceededThreshold = false;
    }

    if (double.IsNaN(card.Element.Width) || card.Element.Width <= 0)
    {
      card.Element.Width = Math.Max(card.MinWidth, card.Element.ActualWidth);
    }

    if (double.IsNaN(card.Element.Height) || card.Element.Height <= 0)
    {
      card.Element.Height = Math.Max(card.MinHeight, card.Element.ActualHeight);
    }

    PrepareCardForExplicitBounds(card);
    this.BringCardToFront(card);
    this.activeResizeCard = card;
    this.activeResizeStartPoint = startPoint;
    this.activeResizeStartLeft = this.GetCardCanvasLeft(card);
    this.activeResizeStartTop = this.GetCardCanvasTop(card);
    this.activeResizeStartWidth = GetCardWidth(card);
    this.activeResizeStartHeight = GetCardHeight(card);
    this.activeResizeTotalHorizontalChange = 0;
    this.activeResizeTotalVerticalChange = 0;
    if (captureMouse)
    {
      card.Element.CaptureMouse();
    }
  }

  private void UpdateActiveCardResize(Point currentPoint)
  {
    if (this.activeResizeCard == null)
    {
      return;
    }

    Vector delta = currentPoint - this.activeResizeStartPoint;
    Size constrainedSize = this.ApplyCardResize(
      this.activeResizeCard,
      this.activeResizeStartWidth + delta.X,
      this.activeResizeStartHeight + delta.Y,
      this.activeResizeStartLeft,
      this.activeResizeStartTop);
    this.activeResizeCard.Element.UpdateLayout();
    this.RefreshAdaptiveCardContent();
  }

  private void CaptureAllCardLayouts()
  {
    foreach (MovableWidgetCard card in this.movableCards.Values)
    {
      this.SetCardLayout(card);
    }
  }

  private void BringCardToFront(MovableWidgetCard card)
  {
    this.ApplyCardZIndex(card, ++this.currentTopCardZIndex);
  }

  private void ApplyCardZIndex(MovableWidgetCard card, int zIndex)
  {
    Panel.SetZIndex(card.Element, zIndex);
    if (card.Element == this.HeroCard)
    {
      Panel.SetZIndex(this.HeroCard, zIndex);
    }
    else
    {
      Panel.SetZIndex(this.AdaptiveCardsGrid, zIndex);
    }

    this.currentTopCardZIndex = Math.Max(this.currentTopCardZIndex, zIndex);
  }

  private static void PrepareCardForExplicitBounds(MovableWidgetCard card)
  {
    card.Element.HorizontalAlignment = HorizontalAlignment.Left;
    card.Element.VerticalAlignment = VerticalAlignment.Top;
    card.Element.ClipToBounds = true;
  }

  private void ToggleCardCollapsed(MovableWidgetCard card)
  {
    this.MaterializeCardBounds(card);
    if (card.IsCollapsed)
    {
      card.IsCollapsed = false;
      card.Element.Width = Math.Max(card.MinWidth, card.ExpandedWidth ?? card.DefaultWidth);
      card.Element.Height = Math.Max(card.MinHeight, card.ExpandedHeight ?? card.DefaultHeight);
      RestoreExpandedContent(card);
    }
    else
    {
      card.IsCollapsed = true;
      card.ExpandedWidth = GetCardWidth(card);
      card.ExpandedHeight = GetCardHeight(card);
      card.Element.Width = this.GetCollapsedCardWidth(card);
      card.Element.Height = CollapsedCardHeight;
      this.ApplyCollapsedContent(card);
    }

    this.SaveCardLayout(card);
  }

  private void ApplyCollapsedState(MovableWidgetCard card)
  {
    if (!card.IsCollapsed)
    {
      return;
    }

    card.ExpandedWidth ??= Math.Max(card.MinWidth, card.Element.Width);
    card.ExpandedHeight ??= Math.Max(card.MinHeight, card.Element.Height);
    card.Element.Width = this.GetCollapsedCardWidth(card);
    card.Element.Height = CollapsedCardHeight;
    this.ApplyCollapsedContent(card);
  }

  private void ApplyCollapsedContent(MovableWidgetCard card)
  {
    if (card.Element is not Border border)
    {
      return;
    }

    Grid root = new Grid
    {
      ClipToBounds = false,
    };
    root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
    root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    Border visualStrip = new Border
    {
      Width = 20,
      Height = 20,
      Margin = new Thickness(0, 0, 7, 0),
      Background = this.ResolveCollapsedVisualBrush(card),
      CornerRadius = new CornerRadius(8),
    };
    TextBlock title = new TextBlock
    {
      FontSize = 13,
      FontWeight = FontWeights.SemiBold,
      Foreground = (Brush)this.FindResource("PrimaryTextBrush"),
      Text = this.ResolveCollapsedTitle(card),
      TextTrimming = TextTrimming.CharacterEllipsis,
      VerticalAlignment = VerticalAlignment.Center,
    };
    Grid.SetColumn(visualStrip, 0);
    Grid.SetColumn(title, 1);
    root.Children.Add(visualStrip);
    root.Children.Add(title);
    border.Child = root;
  }

  private static void RestoreExpandedContent(MovableWidgetCard card)
  {
    if (card.Element is Border border && card.ExpandedContent != null)
    {
      border.Child = card.ExpandedContent;
    }
  }

  private double GetCollapsedCardWidth(MovableWidgetCard card)
  {
    string title = this.ResolveCollapsedTitle(card);
    double estimatedWidth = title.Length * 7.5 + 58;
    return Math.Clamp(estimatedWidth, 120, Math.Min(card.ExpandedWidth ?? card.DefaultWidth, 260));
  }

  private Brush ResolveCollapsedVisualBrush(MovableWidgetCard card)
  {
    if (card.Key.StartsWith(InsightCardKeyPrefix, StringComparison.Ordinal))
    {
      return new LinearGradientBrush(
        Color.FromArgb(42, 45, 212, 191),
        Color.FromArgb(22, 96, 165, 250),
        35);
    }

    return card.Key switch
    {
      "moments" => new LinearGradientBrush(Color.FromArgb(54, 34, 211, 238), Color.FromArgb(20, 94, 234, 212), 35),
      "next" => new LinearGradientBrush(Color.FromArgb(50, 245, 158, 11), Color.FromArgb(18, 251, 191, 36), 35),
      "timeline" => new LinearGradientBrush(Color.FromArgb(46, 96, 165, 250), Color.FromArgb(18, 56, 189, 248), 35),
      "focus" => new LinearGradientBrush(Color.FromArgb(46, 45, 212, 191), Color.FromArgb(18, 52, 211, 153), 35),
      "actions" => new LinearGradientBrush(Color.FromArgb(46, 251, 146, 60), Color.FromArgb(18, 250, 204, 21), 35),
      _ => (Brush)this.FindResource("GlassControlBrush"),
    };
  }

  private string ResolveCollapsedTitle(MovableWidgetCard card)
  {
    if (card.Key.StartsWith(InsightCardKeyPrefix, StringComparison.Ordinal))
    {
      return FindFirstText(card.Element) ?? "Card";
    }

    return card.Key switch
    {
      "moments" => this.CurrentTitleText.Text,
      "next" => this.NextTitleText.Text,
      "timeline" => this.TimelineTitleText.Text,
      "focus" => this.FocusTitleText.Text,
      "actions" => this.ActionsTitleText.Text,
      _ => FindFirstText(card.Element) ?? "Card",
    };
  }

  private static string? FindFirstText(DependencyObject source)
  {
    int count = VisualTreeHelper.GetChildrenCount(source);
    for (int index = 0; index < count; index++)
    {
      DependencyObject child = VisualTreeHelper.GetChild(source, index);
      if (child is TextBlock textBlock && !string.IsNullOrWhiteSpace(textBlock.Text))
      {
        return textBlock.Text;
      }

      string? nestedText = FindFirstText(child);
      if (!string.IsNullOrWhiteSpace(nestedText))
      {
        return nestedText;
      }
    }

    return null;
  }

  private void MaterializeCardBounds(MovableWidgetCard card)
  {
    Point originBefore = this.GetCardBaseOrigin(card);
    double width = this.SanitizeCardWidth(card, GetCardWidth(card));
    double height = this.SanitizeCardHeight(card, GetCardHeight(card));
    card.Element.Width = width;
    card.Element.Height = height;
    PrepareCardForExplicitBounds(card);
    card.Element.UpdateLayout();
    Point originAfter = this.GetCardBaseOrigin(card);
    card.Transform.X += originBefore.X - originAfter.X;
    card.Transform.Y += originBefore.Y - originAfter.Y;
  }

  private MovableWidgetCard? FindMovableCard(FrameworkElement element)
  {
    foreach (MovableWidgetCard card in this.movableCards.Values)
    {
      if (ReferenceEquals(card.Element, element))
      {
        return card;
      }
    }

    return null;
  }

  private bool IsInsideResizeGrip(DependencyObject? source)
  {
    return FindVisualAncestor<Thumb>(source) != null;
  }

  private static bool IsInsideCardResizeZone(MovableWidgetCard card, Point point)
  {
    double width = GetCardWidth(card);
    double height = GetCardHeight(card);
    return point.X >= width - CardResizeZoneSize
      && point.Y >= height - CardResizeZoneSize
      && point.X <= width
      && point.Y <= height;
  }

  private bool IsInsideNestedButton(
    MovableWidgetCard card,
    DependencyObject? source)
  {
    if (card.Element == this.NextButton)
    {
      return false;
    }

    return FindVisualAncestor<Button>(source) != null;
  }

  private Point ConstrainCardOffset(MovableWidgetCard card, double x, double y)
  {
    if (this.SnapshotPanel.ActualWidth <= 0 || this.SnapshotPanel.ActualHeight <= 0)
    {
      return new Point(x, y);
    }

    Point baseOrigin = this.GetCardBaseOrigin(card);
    double width = GetCardWidth(card);
    double height = GetCardHeight(card);
    double minX = -baseOrigin.X;
    double minY = -baseOrigin.Y;
    double maxX = Math.Max(minX, this.SnapshotPanel.ActualWidth - baseOrigin.X - width);
    double maxY = Math.Max(minY, this.SnapshotPanel.ActualHeight - baseOrigin.Y - height);
    return new Point(
      Math.Clamp(x, minX, maxX),
      Math.Clamp(y, minY, maxY));
  }

  private Point ConstrainCardPosition(MovableWidgetCard card, double x, double y)
  {
    if (this.SnapshotPanel.ActualWidth <= 0 || this.SnapshotPanel.ActualHeight <= 0)
    {
      return new Point(x, y);
    }

    double width = GetCardWidth(card);
    double height = GetCardHeight(card);
    double maxX = Math.Max(0, this.SnapshotPanel.ActualWidth - width);
    double maxY = Math.Max(0, this.SnapshotPanel.ActualHeight - height);
    return new Point(
      Math.Clamp(x, 0, maxX),
      Math.Clamp(y, 0, maxY));
  }

  private Size ConstrainCardSize(MovableWidgetCard card, double width, double height)
  {
    if (this.SnapshotPanel.ActualWidth <= 0 || this.SnapshotPanel.ActualHeight <= 0)
    {
      return new Size(Math.Max(card.MinWidth, width), Math.Max(card.MinHeight, height));
    }

    double left = this.GetCardCanvasLeft(card);
    double top = this.GetCardCanvasTop(card);
    double maxWidth = Math.Max(card.MinWidth, this.SnapshotPanel.ActualWidth - left);
    double maxHeight = Math.Max(card.MinHeight, this.SnapshotPanel.ActualHeight - top);
    return new Size(
      Math.Clamp(width, card.MinWidth, maxWidth),
      Math.Clamp(height, card.MinHeight, maxHeight));
  }

  private Size ApplyCardResize(MovableWidgetCard card, double width, double height)
  {
    return this.ApplyCardResize(
      card,
      width,
      height,
      this.GetCardCanvasLeft(card),
      this.GetCardCanvasTop(card));
  }

  private Size ApplyCardResize(
    MovableWidgetCard card,
    double width,
    double height,
    double preferredLeft,
    double preferredTop)
  {
    if (this.SnapshotPanel.ActualWidth <= 0 || this.SnapshotPanel.ActualHeight <= 0)
    {
      Size fallbackSize = new Size(Math.Max(card.MinWidth, width), Math.Max(card.MinHeight, height));
      card.Element.Width = fallbackSize.Width;
      card.Element.Height = fallbackSize.Height;
      return fallbackSize;
    }

    double targetWidth = Math.Clamp(width, card.MinWidth, this.SnapshotPanel.ActualWidth);
    double targetHeight = Math.Clamp(height, card.MinHeight, this.SnapshotPanel.ActualHeight);
    double targetLeft = Math.Clamp(preferredLeft, 0, Math.Max(0, this.SnapshotPanel.ActualWidth - card.MinWidth));
    double targetTop = Math.Clamp(preferredTop, 0, Math.Max(0, this.SnapshotPanel.ActualHeight - card.MinHeight));

    double overflowRight = targetLeft + targetWidth - this.SnapshotPanel.ActualWidth;
    if (overflowRight > 0)
    {
      targetLeft = Math.Max(0, targetLeft - overflowRight);
    }

    double overflowBottom = targetTop + targetHeight - this.SnapshotPanel.ActualHeight;
    if (overflowBottom > 0)
    {
      targetTop = Math.Max(0, targetTop - overflowBottom);
    }

    targetWidth = Math.Clamp(targetWidth, card.MinWidth, this.SnapshotPanel.ActualWidth - targetLeft);
    targetHeight = Math.Clamp(targetHeight, card.MinHeight, this.SnapshotPanel.ActualHeight - targetTop);
    Canvas.SetLeft(card.Element, targetLeft);
    Canvas.SetTop(card.Element, targetTop);
    card.Transform.X = 0;
    card.Transform.Y = 0;
    card.Element.Width = targetWidth;
    card.Element.Height = targetHeight;
    return new Size(targetWidth, targetHeight);
  }

  private Point GetCardBaseOrigin(MovableWidgetCard card)
  {
    try
    {
      Point origin = card.Element.TransformToAncestor(this.SnapshotPanel).Transform(new Point(0, 0));
      return new Point(origin.X - card.Transform.X, origin.Y - card.Transform.Y);
    }
    catch (InvalidOperationException)
    {
      return new Point(0, 0);
    }
  }

  private static double GetCardWidth(MovableWidgetCard card)
  {
    if (!double.IsNaN(card.Element.Width) && card.Element.Width > 0)
    {
      return card.Element.Width;
    }

    return Math.Max(card.MinWidth, card.Element.ActualWidth);
  }

  private static double GetCardHeight(MovableWidgetCard card)
  {
    if (!double.IsNaN(card.Element.Height) && card.Element.Height > 0)
    {
      return card.Element.Height;
    }

    return Math.Max(card.MinHeight, card.Element.ActualHeight);
  }

  private double SanitizeCardWidth(MovableWidgetCard card, double width)
  {
    if (this.SnapshotPanel.ActualWidth <= 0)
    {
      return Math.Max(card.MinWidth, width);
    }

    double availableWidth = Math.Max(card.MinWidth, this.SnapshotPanel.ActualWidth - 18);
    return Math.Clamp(width, card.MinWidth, availableWidth);
  }

  private double SanitizeCardHeight(MovableWidgetCard card, double height)
  {
    if (this.SnapshotPanel.ActualHeight <= 0)
    {
      return Math.Max(card.MinHeight, height);
    }

    double availableHeight = Math.Max(card.MinHeight, this.SnapshotPanel.ActualHeight - 18);
    return Math.Clamp(height, card.MinHeight, availableHeight);
  }

  private void SaveCardLayout(MovableWidgetCard card)
  {
    this.SetLayoutDirty(true);
  }

  private void SetCardLayout(MovableWidgetCard card)
  {
    Point position = this.GetCardVisualPosition(card);
    this.settings.CardLayoutsUseAbsoluteCoordinates = true;
    this.settings.CardLayoutSchemaVersion = CardLayoutSchemaVersion;
    this.settings.CardLayouts[card.Key] = new WidgetCardLayout
    {
      Height = GetCardHeight(card),
      ExpandedHeight = card.ExpandedHeight,
      ExpandedWidth = card.ExpandedWidth,
      IsCollapsed = card.IsCollapsed,
      Width = GetCardWidth(card),
      X = position.X,
      Y = position.Y,
      ZIndex = Panel.GetZIndex(card.Element),
    };
  }

  private Point GetCardVisualPosition(MovableWidgetCard card)
  {
    try
    {
      return card.Element.TransformToAncestor(this.SnapshotPanel).Transform(new Point(0, 0));
    }
    catch (InvalidOperationException)
    {
      double left = Canvas.GetLeft(card.Element);
      double top = Canvas.GetTop(card.Element);
      return new Point(double.IsNaN(left) ? 0 : left, double.IsNaN(top) ? 0 : top);
    }
  }

  private Point NormalizeCardCanvasPosition(MovableWidgetCard card)
  {
    Point position = this.GetCardVisualPosition(card);
    Point constrainedPosition = this.ConstrainCardPosition(card, position.X, position.Y);
    Canvas.SetLeft(card.Element, constrainedPosition.X);
    Canvas.SetTop(card.Element, constrainedPosition.Y);
    card.Transform.X = 0;
    card.Transform.Y = 0;
    return constrainedPosition;
  }

  private double GetCardCanvasLeft(MovableWidgetCard card)
  {
    double left = Canvas.GetLeft(card.Element);
    return double.IsNaN(left) ? 0 : left;
  }

  private double GetCardCanvasTop(MovableWidgetCard card)
  {
    double top = Canvas.GetTop(card.Element);
    return double.IsNaN(top) ? 0 : top;
  }

  private void OnWindowLayoutChanged(object? sender, EventArgs e)
  {
    this.MarkWindowLayoutChanged();
  }

  private void MarkWindowLayoutChanged()
  {
    if (!this.layoutChangeTrackingEnabled
      || this.suppressLayoutChangeTracking
      || this.WindowState != WindowState.Normal)
    {
      return;
    }

    if (!this.IsLoaded && !this.restoredWindowLayout)
    {
      return;
    }

    this.SetLayoutDirty(true);
  }

  private void CaptureWindowLayout()
  {
    if (this.WindowState != WindowState.Normal)
    {
      return;
    }

    this.settings.WindowLayout = new WidgetWindowLayout
    {
      Height = this.Height,
      Left = this.Left,
      Top = this.Top,
      Width = this.Width,
    };
  }

  private bool IsValidWindowPosition(double left, double top, double width, double height)
  {
    if (!IsFinite(left) || !IsFinite(top))
    {
      return false;
    }

    Rect virtualScreen = new Rect(
      SystemParameters.VirtualScreenLeft,
      SystemParameters.VirtualScreenTop,
      SystemParameters.VirtualScreenWidth,
      SystemParameters.VirtualScreenHeight);
    Rect windowRect = new Rect(left, top, Math.Max(this.MinWidth, width), Math.Max(this.MinHeight, height));
    return virtualScreen.IntersectsWith(windowRect);
  }

  private static bool IsFinite(double value)
  {
    return !double.IsNaN(value) && !double.IsInfinity(value);
  }

  private static T? FindVisualAncestor<T>(DependencyObject? source)
    where T : DependencyObject
  {
    DependencyObject? current = source;
    while (current != null)
    {
      if (current is T match)
      {
        return match;
      }

      current = VisualTreeHelper.GetParent(current);
    }

    return null;
  }

  private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
  {
    if (!this.cardsHostedInCanvas)
    {
      this.ApplyResponsiveLayout();
    }
    else
    {
      foreach (MovableWidgetCard card in this.movableCards.Values)
      {
        Size constrainedSize = this.ConstrainCardSize(
          card,
          GetCardWidth(card),
          GetCardHeight(card));
        card.Element.Width = constrainedSize.Width;
        card.Element.Height = constrainedSize.Height;
        this.NormalizeCardCanvasPosition(card);
      }

      this.RefreshAdaptiveCardContent();
    }

    this.UpdateProgressVisual(this.currentProgressPercent);
    this.MarkWindowLayoutChanged();
  }

  private void RefreshAdaptiveCardContent()
  {
    if (this.currentSnapshot == null)
    {
      return;
    }

    this.RenderAdaptiveCollections(this.currentSnapshot);
  }

  private void OnWindowStateChanged(object? sender, EventArgs e)
  {
    this.EnsureTaskbarPresence();
    if (this.WindowState == WindowState.Minimized)
    {
      this.ShowInTaskbar = true;
      this.Visibility = Visibility.Visible;
    }
  }

  private void OnSourceInitialized(object? sender, EventArgs e)
  {
    this.EnsureTaskbarPresence();
    this.ApplyTaskbarIcon();
    this.ApplyWindowsBackdrop();
  }

  private void EnsureTaskbarPresence()
  {
    this.ShowInTaskbar = true;
    IntPtr handle = new WindowInteropHelper(this).Handle;
    if (handle == IntPtr.Zero)
    {
      return;
    }

    int extendedStyle = GetWindowLong(handle, ExtendedWindowStyleIndex);
    int desiredStyle = (extendedStyle | AppWindowStyle) & ~ToolWindowStyle;
    if (desiredStyle != extendedStyle)
    {
      SetWindowLong(handle, ExtendedWindowStyleIndex, desiredStyle);
    }
  }

  private void ApplyTaskbarIcon()
  {
    IntPtr handle = new WindowInteropHelper(this).Handle;
    string iconPath = GetWidgetIconPath();
    if (handle == IntPtr.Zero || !File.Exists(iconPath))
    {
      return;
    }

    this.taskbarSmallIcon?.Dispose();
    this.taskbarLargeIcon?.Dispose();
    this.taskbarSmallIcon = CreateIconFromImage(iconPath, 16);
    this.taskbarLargeIcon = CreateIconFromImage(iconPath, 32);

    SendMessage(
      handle,
      WindowSetIconMessage,
      new IntPtr(WindowIconSmall),
      this.taskbarSmallIcon.Handle);
    SendMessage(
      handle,
      WindowSetIconMessage,
      new IntPtr(WindowIconBig),
      this.taskbarLargeIcon.Handle);
  }

  private void ApplyWindowsBackdrop()
  {
    IntPtr handle = new WindowInteropHelper(this).Handle;
    if (handle == IntPtr.Zero)
    {
      return;
    }

    try
    {
      int backdropType = DwmAcrylicBackdropType;
      DwmSetWindowAttribute(
        handle,
        DwmSystemBackdropTypeAttribute,
        ref backdropType,
        Marshal.SizeOf<int>());

      int cornerPreference = DwmRoundedCornerPreference;
      DwmSetWindowAttribute(
        handle,
        DwmWindowCornerPreferenceAttribute,
        ref cornerPreference,
        Marshal.SizeOf<int>());
    }
    catch (DllNotFoundException)
    {
    }
    catch (EntryPointNotFoundException)
    {
    }
  }

  private void ApplyResponsiveLayout()
  {
    if (this.AdaptiveCardsGrid == null)
    {
      return;
    }

    bool compactHeight = this.ActualHeight > 0 && this.ActualHeight < CompactLayoutMaxHeight;
    bool tinyHeight = this.ActualHeight > 0 && this.ActualHeight < TinyLayoutMaxHeight;
    bool ultraCompact = this.ActualWidth < 420 && this.ActualHeight < 560;
    bool useWideLayout = this.ActualWidth >= WideLayoutMinWidth && !tinyHeight;
    bool splitSideCards = this.ActualWidth >= 980;
    Thickness compactCardPadding = tinyHeight ? new Thickness(5) : new Thickness(7);

    this.TitleBar.Padding = tinyHeight
      ? new Thickness(10, 5, 8, 2)
      : new Thickness(12, 8, 10, 4);
    this.HeaderIconBadge.Height = tinyHeight ? 26 : 30;
    this.HeaderIconBadge.Width = tinyHeight ? 26 : 30;
    this.AppLabelText.Visibility = tinyHeight ? Visibility.Collapsed : Visibility.Visible;
    this.HeaderText.FontSize = tinyHeight ? 18 : 22;
    this.SnapshotPanel.Margin = tinyHeight
      ? new Thickness(10, 2, 10, 10)
      : new Thickness(14, 4, 14, 14);
    this.HeroCard.Padding = new Thickness(0);
    this.CurrentTitleText.FontSize = tinyHeight ? 22 : 27;
    this.CurrentIconText.FontSize = tinyHeight ? 17 : 19;
    this.ProgressPercentText.FontSize = tinyHeight ? 12 : 13;
    this.CurrentProgressGrid.Margin = new Thickness(0);
    this.HeroMetricsGrid.Margin = tinyHeight ? new Thickness(0, 6, 0, 0) : new Thickness(0, 8, 0, 0);
    this.NextButton.Padding = tinyHeight ? new Thickness(7, 5, 7, 5) : new Thickness(9, 7, 9, 7);
    this.TimelineCard.Visibility = ultraCompact ? Visibility.Collapsed : Visibility.Visible;
    this.TimelineCard.Margin = tinyHeight ? new Thickness(0, 5, 0, 0) : new Thickness(0, 6, 0, 0);
    this.TimelineCard.Padding = tinyHeight ? new Thickness(6) : new Thickness(8);
    this.TimelineTitleText.FontSize = tinyHeight ? 13 : 14;
    this.FocusCard.Padding = compactCardPadding;
    this.ActionsCard.Padding = compactCardPadding;
    this.FocusTitleText.Visibility = tinyHeight ? Visibility.Collapsed : Visibility.Visible;
    this.ActionsTitleText.Visibility = tinyHeight ? Visibility.Collapsed : Visibility.Visible;
    this.InsightDeckTitleText.FontSize = tinyHeight ? 13 : 14;
    this.FocusItemsControl.Margin = tinyHeight ? new Thickness(0) : new Thickness(0, 5, 0, 0);
    this.ActionItemsControl.Margin = tinyHeight ? new Thickness(0) : new Thickness(0, 5, 0, 0);

    this.PrimaryCardsColumn.Width = useWideLayout
      ? new GridLength(1.35, GridUnitType.Star)
      : new GridLength(1, GridUnitType.Star);
    this.SecondaryCardsColumn.Width = useWideLayout
      ? new GridLength(1, GridUnitType.Star)
      : new GridLength(0);

    Grid.SetColumnSpan(this.NextButton, useWideLayout ? 2 : 1);
    Grid.SetRow(this.SideCardsPanel, useWideLayout ? 1 : 2);
    Grid.SetColumn(this.SideCardsPanel, useWideLayout ? 1 : 0);
    this.SideCardsPanel.Margin = useWideLayout
      ? new Thickness(8, 8, 0, 0)
      : new Thickness(0, tinyHeight ? 5 : 8, 0, 0);

    this.FocusActionsPrimaryColumn.Width = new GridLength(1, GridUnitType.Star);
    this.FocusActionsSecondaryColumn.Width = splitSideCards
      ? new GridLength(1, GridUnitType.Star)
      : new GridLength(0);
    Grid.SetRow(this.ActionsCard, splitSideCards ? 0 : 1);
    Grid.SetColumn(this.ActionsCard, splitSideCards ? 1 : 0);
    this.ActionsCard.Margin = splitSideCards
      ? new Thickness(tinyHeight ? 4 : 6, 0, 0, 0)
      : new Thickness(0, tinyHeight ? 4 : 6, 0, 0);

    this.StatusText.Visibility = compactHeight ? Visibility.Collapsed : Visibility.Visible;
    this.CurrentDetailText.Visibility = compactHeight ? Visibility.Collapsed : Visibility.Visible;
    this.NextDetailText.Visibility = compactHeight ? Visibility.Collapsed : Visibility.Visible;
  }

  private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
  {
    try
    {
      using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
      while (await timer.WaitForNextTickAsync(cancellationToken))
      {
        await this.Dispatcher.InvokeAsync(() => _ = this.RefreshAsync("timer"));
      }
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex)
    {
      WidgetDiagnosticsLogger.Error("Refresh timer failed.", ex);
    }
  }

  private async Task RunUpdateLoopAsync(CancellationToken cancellationToken)
  {
    try
    {
      using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
      while (await timer.WaitForNextTickAsync(cancellationToken))
      {
        await this.Dispatcher.InvokeAsync(() => _ = this.CheckForUpdatesAsync(false, false));
      }
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex)
    {
      WidgetDiagnosticsLogger.Error("Update timer failed.", ex);
    }
  }

  private async Task RefreshAsync(string source)
  {
    if (string.IsNullOrWhiteSpace(this.widgetToken))
    {
      return;
    }

    if (!await this.refreshGate.WaitAsync(0))
    {
      WidgetDiagnosticsLogger.Info($"Refresh skipped because another refresh is running. Source={source}.");
      return;
    }

    try
    {
      WidgetDiagnosticsLogger.Info($"Refresh started. Source={source}.");
      if (this.client.IsLocalServerUnavailable(this.settings.BaseUrl))
      {
        this.SwitchUnavailableLocalBaseUrlToHosted();
      }

      TodayWidgetSnapshotDto snapshot = await this.client.GetTodayAsync(
        this.settings.BaseUrl,
        this.widgetToken,
        "pt",
        CancellationToken.None);
      this.currentSnapshot = snapshot;
      this.RenderSnapshot(snapshot);
      WidgetDiagnosticsLogger.Info(
        $"Refresh completed. Source={source}; LocalNow={snapshot.LocalNow:O}; GeneratedAtUtc={snapshot.GeneratedAtUtc:O}.");
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized
      || ex.StatusCode == HttpStatusCode.Forbidden)
    {
      WidgetDiagnosticsLogger.Error($"Refresh rejected by server. Source={source}.", ex);
      this.ClearWidgetToken();
      this.ShowPairingPanel(
        true,
        "Parear widget",
        "O pareamento foi recusado. Gere um novo código no Perfil e cole aqui com o token GitHub.");
    }
    catch (Exception ex)
    {
      WidgetDiagnosticsLogger.Error($"Refresh failed. Source={source}.", ex);
      this.ShowOfflineState("Não foi possível atualizar agora.");
    }
    finally
    {
      this.refreshGate.Release();
    }
  }

  private void SwitchUnavailableLocalBaseUrlToHosted()
  {
    if (!this.client.IsLocalBaseUrl(this.settings.BaseUrl))
    {
      return;
    }

    this.settings.BaseUrl = WidgetSettings.DefaultBaseUrl;
    this.settingsStore.Save(this.settings);
    this.BaseUrlTextBox.Text = this.settings.BaseUrl;
  }

  private void RenderSnapshot(TodayWidgetSnapshotDto snapshot)
  {
    this.ShowSnapshotPanel();
    this.HeaderText.Text = $"{snapshot.Title} · {snapshot.LocalNow:HH:mm}";
    this.StatusText.Text = snapshot.Next == null
      ? $"{snapshot.Metadata.PlannedMomentCount} momentos hoje"
      : $"{snapshot.Metadata.PlannedMomentCount} momentos hoje · próximo {snapshot.Next.Title}";

    TodayWidgetItemDto? current = snapshot.Current;
    string nextActivitySignature = BuildActivitySignature(current);
    bool activityChanged = !string.IsNullOrEmpty(this.currentActivitySignature)
      && !string.Equals(this.currentActivitySignature, nextActivitySignature, StringComparison.Ordinal);
    int progress = Math.Clamp(current?.ProgressPercent ?? 0, 0, 100);
    this.CurrentIconText.Text = ResolveIcon(current?.Title, current?.Category, current?.IsCalendar ?? false);
    ImageSource? heroImage = CreateLocalImageSource(ResolveHeroImagePath(current?.Title, current?.Category));
    this.ApplyHeroBackground(heroImage);
    this.HeroContextImage.Source = null;
    this.HeroContextImage.Visibility = Visibility.Collapsed;
    this.HeroImageOverlay.Visibility = heroImage == null ? Visibility.Collapsed : Visibility.Visible;
    this.CurrentTitleText.Text = current?.Title ?? "Espaço livre";
    this.CurrentDetailText.Text = current == null
      ? "Nada planejado agora"
      : $"{current.StartLocal:HH:mm}-{current.EndLocal:HH:mm} · {current.Category}";
    this.UpdateProgressVisual(progress);
    this.TodayCountText.Text =
      $"{snapshot.Metadata.CompletedMomentCount}/{snapshot.Metadata.PlannedMomentCount}";
    this.RemainingText.Text = current?.RemainingLabel ?? "-";
    this.SimilarCountText.Text = $"{snapshot.Metadata.SimilarMomentCount}×";

    this.NextButton.Tag = snapshot.Next?.Href ?? "routine";
    this.NextIconText.Text = ResolveIcon(snapshot.Next?.Title, snapshot.Next?.Category, snapshot.Next?.IsCalendar ?? false);
    this.NextTitleText.Text = snapshot.Next?.Title ?? "Ajustar rotina";
    this.NextDetailText.Text = snapshot.Next == null
      ? "Sem próximo bloco"
      : $"{snapshot.Next.NextLabel} · {snapshot.Next.StartLocal:HH:mm}-{snapshot.Next.EndLocal:HH:mm}";

    this.RenderAdaptiveCollections(snapshot);
    this.FooterText.Text = $"Atualizado: {snapshot.GeneratedAtUtc.ToLocalTime():HH:mm:ss}";
    if (activityChanged)
    {
      this.PlayActivityChangedAnimation(current?.Title ?? "Espaço livre");
    }

    this.currentActivitySignature = nextActivitySignature;
  }

  private static string BuildActivitySignature(TodayWidgetItemDto? item)
  {
    if (item == null)
    {
      return "free";
    }

    return $"{item.Id}|{item.Title}|{item.StartLocal:O}|{item.EndLocal:O}|{item.IsCalendar}";
  }

  private void ApplyHeroBackground(ImageSource? heroImage)
  {
    if (heroImage == null)
    {
      this.HeroCard.Background = (Brush)this.FindResource("HeroGlassBrush");
      return;
    }

    this.HeroCard.Background = new ImageBrush(heroImage)
    {
      AlignmentX = AlignmentX.Center,
      AlignmentY = AlignmentY.Center,
      Opacity = 0.58,
      Stretch = Stretch.UniformToFill,
    };
  }

  private void PlayActivityChangedAnimation(string title)
  {
    if (!SystemParameters.ClientAreaAnimation)
    {
      return;
    }

    this.PlayActivityChangedSound(title);

    this.ActivityChangedText.Text = $"Agora: {title}";
    this.ActivityChangedBadge.BeginAnimation(OpacityProperty, null);
    this.ActivityChangedBadge.Opacity = 0;
    this.ActivityChangedBadge.Visibility = Visibility.Visible;
    DoubleAnimationUsingKeyFrames badgeAnimation = new DoubleAnimationUsingKeyFrames();
    badgeAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
    badgeAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))));
    badgeAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(9.6))));
    badgeAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(10))));
    badgeAnimation.Completed += (_, _) => this.ActivityChangedBadge.Visibility = Visibility.Collapsed;
    this.ActivityChangedBadge.BeginAnimation(OpacityProperty, badgeAnimation);

    this.ProgressPulseFill.BeginAnimation(OpacityProperty, null);
    this.ProgressPulseFill.Opacity = 0;
    this.ProgressPulseFill.BeginAnimation(
      OpacityProperty,
      new DoubleAnimation
      {
        From = 0.32,
        To = 0.02,
        Duration = TimeSpan.FromMilliseconds(620),
        AutoReverse = true,
        RepeatBehavior = new RepeatBehavior(TimeSpan.FromSeconds(10)),
        EasingFunction = new QuadraticEase
        {
          EasingMode = EasingMode.EaseOut,
        },
      });

    SolidColorBrush pulseBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x2D, 0xD4, 0xBF));
    this.HeroCard.BorderBrush = pulseBrush;
    pulseBrush.BeginAnimation(
      SolidColorBrush.ColorProperty,
      new ColorAnimation
      {
        From = Color.FromArgb(0x88, 0x2D, 0xD4, 0xBF),
        To = Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF),
        Duration = TimeSpan.FromMilliseconds(850),
        AutoReverse = true,
        RepeatBehavior = new RepeatBehavior(TimeSpan.FromSeconds(10)),
      });

    if (this.HeroCard.Background is SolidColorBrush)
    {
      SolidColorBrush backgroundPulseBrush = new SolidColorBrush(Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF));
      this.HeroCard.Background = backgroundPulseBrush;
      backgroundPulseBrush.BeginAnimation(
        SolidColorBrush.ColorProperty,
        new ColorAnimation
        {
          From = Color.FromArgb(0x34, 0x2D, 0xD4, 0xBF),
          To = Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF),
          Duration = TimeSpan.FromMilliseconds(700),
          AutoReverse = true,
          RepeatBehavior = new RepeatBehavior(TimeSpan.FromSeconds(10)),
        });
    }

    ScaleTransform? transform = this.ProgressRingPanel.RenderTransform as ScaleTransform;
    if (transform != null)
    {
      DoubleAnimation scaleAnimation = new DoubleAnimation
      {
        From = 1,
        To = 1.14,
        Duration = TimeSpan.FromMilliseconds(360),
        AutoReverse = true,
        RepeatBehavior = new RepeatBehavior(TimeSpan.FromSeconds(10)),
        EasingFunction = new QuadraticEase
        {
          EasingMode = EasingMode.EaseOut,
        },
      };
      transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
      transform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
    }

  }

  private void PlayActivityChangedSound(string title)
  {
    if (this.settings.IsActivitySoundMuted)
    {
      return;
    }

    try
    {
      if (TryParseWindowsVoiceSoundKind(this.settings.ActivitySoundKind, out string? cultureName, out string? windowsVoiceId))
      {
        this.SpeakActivityTitleWithWindowsVoice(
          title,
          IsCultureMatch(cultureName, EnglishVoiceCultureNames),
          windowsVoiceId);
        return;
      }

      if (TryParseVoiceSoundKind(this.settings.ActivitySoundKind, out string? languageId, out string? tokenId))
      {
        this.SpeakActivityTitle(title, IsLanguageMatch(languageId, EnglishVoiceLanguages), tokenId);
        return;
      }

      if (string.Equals(this.settings.ActivitySoundKind, ActivitySoundReadActivityPortuguese, StringComparison.Ordinal))
      {
        this.SpeakActivityTitle(title, false, null);
        return;
      }

      if (string.Equals(this.settings.ActivitySoundKind, ActivitySoundReadActivityEnglish, StringComparison.Ordinal))
      {
        this.SpeakActivityTitle(title, true, null);
        return;
      }

      if (string.Equals(this.settings.ActivitySoundKind, ActivitySoundWindows, StringComparison.Ordinal))
      {
        this.PlayWindowsActivitySound();
        return;
      }

      this.PlayGeneratedActivitySound(this.settings.ActivitySoundKind);
    }
    catch
    {
      // Sound alerts are optional and must not affect the visual widget.
    }
  }

  private void PlayActivitySoundPreview()
  {
    string title = this.currentSnapshot?.Current?.Title ?? this.CurrentTitleText.Text;
    if (string.IsNullOrWhiteSpace(title))
    {
      title = "Espaço livre";
    }

    this.PlayActivityChangedSound(title);
  }

  private void PlayGeneratedActivitySound(string soundKind)
  {
    string soundPath = this.EnsureGeneratedActivitySound(soundKind);
    this.PlayActivitySoundFile(soundPath);
  }

  private void PlayWindowsActivitySound()
  {
    string? soundPath = ResolveWindowsActivitySoundPath();
    if (!string.IsNullOrWhiteSpace(soundPath))
    {
      this.PlayActivitySoundFile(soundPath);
      return;
    }

    System.Media.SystemSounds.Asterisk.Play();
  }

  private void PlayActivitySoundFile(string soundPath)
  {
    this.activitySoundPlayer.Stop();
    this.activitySoundPlayer.Open(new Uri(soundPath, UriKind.Absolute));
    this.activitySoundPlayer.Volume = this.settings.ActivitySoundVolumePercent / 100d;
    this.activitySoundPlayer.Play();
  }

  private void SpeakActivityTitleWithWindowsVoice(string title, bool speakEnglish, string voiceId)
  {
    string speechText = speakEnglish
      ? $"Now: {TranslateActivityTitleToEnglish(title)}"
      : $"Agora: {title}";
    _ = Task.Run(
      async () =>
      {
        try
        {
          VoiceInformation? selectedVoice = null;
          foreach (VoiceInformation voice in SpeechSynthesizer.AllVoices)
          {
            if (string.Equals(voice.Id, voiceId, StringComparison.OrdinalIgnoreCase))
            {
              selectedVoice = voice;
              break;
            }
          }

          if (selectedVoice == null)
          {
            this.Dispatcher.Invoke(() => this.PlayGeneratedActivitySound(ActivitySoundFocusBell));
            return;
          }

          using SpeechSynthesizer synthesizer = new SpeechSynthesizer
          {
            Voice = selectedVoice,
          };
          using SpeechSynthesisStream stream = await synthesizer.SynthesizeTextToStreamAsync(speechText);
          int streamSize = checked((int)stream.Size);
          using IInputStream inputStream = stream.GetInputStreamAt(0);
          using DataReader reader = new DataReader(inputStream);
          await reader.LoadAsync((uint)streamSize);
          byte[] audioBytes = new byte[streamSize];
          reader.ReadBytes(audioBytes);

          string soundPath = Path.Combine(
            Path.GetTempPath(),
            $"applifeplanner-widget-windows-voice-{Guid.NewGuid():N}.wav");
          await File.WriteAllBytesAsync(soundPath, audioBytes);
          this.Dispatcher.Invoke(() => this.PlayActivitySoundFile(soundPath));
        }
        catch
        {
          this.Dispatcher.Invoke(() => this.PlayGeneratedActivitySound(ActivitySoundFocusBell));
        }
      });
  }

  private static string? ResolveWindowsActivitySoundPath()
  {
    string mediaFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media");
    string[] candidates =
    {
      "Windows Notify System Generic.wav",
      "Windows Notify Calendar.wav",
      "Windows Background.wav",
      "Windows Ding.wav",
    };

    foreach (string candidate in candidates)
    {
      string soundPath = Path.Combine(mediaFolder, candidate);
      if (File.Exists(soundPath))
      {
        return soundPath;
      }
    }

    return null;
  }

  private string EnsureGeneratedActivitySound(string soundKind)
  {
    if (this.activitySoundFiles.TryGetValue(soundKind, out string? existingPath) && File.Exists(existingPath))
    {
      return existingPath;
    }

    string soundFolder = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "AppLifePlanner",
      "WindowsWidget",
      "Sounds");
    Directory.CreateDirectory(soundFolder);
    string soundPath = Path.Combine(soundFolder, $"{soundKind}.wav");
    if (!File.Exists(soundPath))
    {
      WriteGeneratedActivitySound(soundPath, soundKind);
    }

    this.activitySoundFiles[soundKind] = soundPath;
    return soundPath;
  }

  private static void WriteGeneratedActivitySound(string soundPath, string soundKind)
  {
    List<ActivityTone> tones = GetActivitySoundTones(soundKind);
    const int sampleRate = 44100;
    const short bitsPerSample = 16;
    const short channels = 1;
    short blockAlign = channels * bitsPerSample / 8;
    int byteRate = sampleRate * blockAlign;
    int totalSamples = 0;
    foreach (ActivityTone tone in tones)
    {
      totalSamples += (int)(sampleRate * tone.Duration.TotalSeconds);
      totalSamples += (int)(sampleRate * tone.GapAfter.TotalSeconds);
    }

    int dataLength = totalSamples * blockAlign;
    using FileStream fileStream = File.Create(soundPath);
    using BinaryWriter writer = new BinaryWriter(fileStream);
    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
    writer.Write(36 + dataLength);
    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
    writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
    writer.Write(16);
    writer.Write((short)1);
    writer.Write(channels);
    writer.Write(sampleRate);
    writer.Write(byteRate);
    writer.Write(blockAlign);
    writer.Write(bitsPerSample);
    writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
    writer.Write(dataLength);

    foreach (ActivityTone tone in tones)
    {
      WriteToneSamples(writer, sampleRate, tone);
      WriteSilenceSamples(writer, sampleRate, tone.GapAfter);
    }
  }

  private static List<ActivityTone> GetActivitySoundTones(string soundKind)
  {
    if (string.Equals(soundKind, ActivitySoundDoublePulse, StringComparison.Ordinal))
    {
      return new List<ActivityTone>
      {
        new ActivityTone(660, TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(70), 0.42),
        new ActivityTone(880, TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(10), 0.38),
      };
    }

    if (string.Equals(soundKind, ActivitySoundFocusBell, StringComparison.Ordinal))
    {
      return new List<ActivityTone>
      {
        new ActivityTone(784, TimeSpan.FromMilliseconds(210), TimeSpan.FromMilliseconds(55), 0.34),
        new ActivityTone(523, TimeSpan.FromMilliseconds(260), TimeSpan.FromMilliseconds(10), 0.28),
      };
    }

    if (string.Equals(soundKind, ActivitySoundBrightPing, StringComparison.Ordinal))
    {
      return new List<ActivityTone>
      {
        new ActivityTone(1046, TimeSpan.FromMilliseconds(90), TimeSpan.FromMilliseconds(38), 0.34),
        new ActivityTone(1318, TimeSpan.FromMilliseconds(110), TimeSpan.FromMilliseconds(8), 0.28),
      };
    }

    return new List<ActivityTone>
    {
      new ActivityTone(587, TimeSpan.FromMilliseconds(180), TimeSpan.FromMilliseconds(45), 0.32),
      new ActivityTone(740, TimeSpan.FromMilliseconds(220), TimeSpan.FromMilliseconds(10), 0.26),
    };
  }

  private static void WriteToneSamples(BinaryWriter writer, int sampleRate, ActivityTone tone)
  {
    int sampleCount = (int)(sampleRate * tone.Duration.TotalSeconds);
    for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
    {
      double progress = sampleIndex / (double)Math.Max(1, sampleCount - 1);
      double fadeIn = Math.Min(1, progress / 0.12);
      double fadeOut = Math.Min(1, (1 - progress) / 0.18);
      double envelope = Math.Min(fadeIn, fadeOut);
      double sample = Math.Sin(2 * Math.PI * tone.Frequency * sampleIndex / sampleRate) * tone.Amplitude * envelope;
      writer.Write((short)(sample * short.MaxValue));
    }
  }

  private static void WriteSilenceSamples(BinaryWriter writer, int sampleRate, TimeSpan duration)
  {
    int sampleCount = (int)(sampleRate * duration.TotalSeconds);
    for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
    {
      writer.Write((short)0);
    }
  }

  private void SpeakActivityTitle(string title, bool speakEnglish, string? voiceTokenId)
  {
    int volume = this.settings.ActivitySoundVolumePercent;
    string speechText = speakEnglish
      ? $"Now: {TranslateActivityTitleToEnglish(title)}"
      : $"Agora: {title}";
    string[] voiceLanguages = speakEnglish ? EnglishVoiceLanguages : PortugueseVoiceLanguages;
    _ = Task.Run(
      () =>
      {
        object? voice = null;
        try
        {
          Type? voiceType = Type.GetTypeFromProgID("SAPI.SpVoice");
          if (voiceType == null)
          {
            this.Dispatcher.Invoke(() => this.PlayGeneratedActivitySound(ActivitySoundFocusBell));
            return;
          }

          voice = Activator.CreateInstance(voiceType);
          if (voice == null)
          {
            this.Dispatcher.Invoke(() => this.PlayGeneratedActivitySound(ActivitySoundFocusBell));
            return;
          }

          bool selectedExpectedVoice = !string.IsNullOrWhiteSpace(voiceTokenId)
            ? TrySelectSapiVoiceByTokenId(voiceType, voice, voiceTokenId)
            : TrySelectSapiVoice(voiceType, voice, voiceLanguages);
          if (!selectedExpectedVoice && !speakEnglish)
          {
            this.Dispatcher.Invoke(
              () =>
              {
                this.ShowUpdateStatus("Voz pt-BR do Windows não instalada", false);
                this.FooterText.Text = "Instale a voz de texto para fala pt-BR no Windows.";
                this.FooterText.Visibility = Visibility.Visible;
                this.PlayGeneratedActivitySound(ActivitySoundFocusBell);
              });
            return;
          }

          voiceType.InvokeMember("Volume", BindingFlags.SetProperty, null, voice, new object[] { volume });
          voiceType.InvokeMember("Speak", BindingFlags.InvokeMethod, null, voice, new object[] { speechText, 0 });
        }
        catch
        {
          this.Dispatcher.Invoke(() => this.PlayGeneratedActivitySound(ActivitySoundFocusBell));
        }
        finally
        {
          if (voice != null && Marshal.IsComObject(voice))
          {
            Marshal.ReleaseComObject(voice);
          }
        }
      });
  }

  private static bool TrySelectSapiVoice(Type voiceType, object voice, string[] languageIds)
  {
    foreach (string languageId in languageIds)
    {
      object? tokens = null;
      object? token = null;
      try
      {
        tokens = voiceType.InvokeMember(
          "GetVoices",
          BindingFlags.InvokeMethod,
          null,
          voice,
          new object[] { $"Language={languageId}", string.Empty });
        if (tokens == null)
        {
          continue;
        }

        Type tokenCollectionType = tokens.GetType();
        object? countValue = tokenCollectionType.InvokeMember("Count", BindingFlags.GetProperty, null, tokens, null);
        int count = countValue is int typedCount ? typedCount : 0;
        if (count <= 0)
        {
          continue;
        }

        token = tokenCollectionType.InvokeMember("Item", BindingFlags.InvokeMethod, null, tokens, new object[] { 0 });
        if (token == null)
        {
          continue;
        }

        voiceType.InvokeMember("Voice", BindingFlags.SetProperty, null, voice, new[] { token });
        return true;
      }
      catch
      {
        // Voice selection is best effort; SAPI falls back to the default voice.
      }
      finally
      {
        if (token != null && Marshal.IsComObject(token))
        {
          Marshal.ReleaseComObject(token);
        }

        if (tokens != null && Marshal.IsComObject(tokens))
        {
          Marshal.ReleaseComObject(tokens);
        }
      }
    }

    return false;
  }

  private static bool TrySelectSapiVoiceByTokenId(Type voiceType, object voice, string tokenId)
  {
    object? tokens = null;
    object? token = null;
    try
    {
      tokens = voiceType.InvokeMember(
        "GetVoices",
        BindingFlags.InvokeMethod,
        null,
        voice,
        new object[] { string.Empty, string.Empty });
      if (tokens == null)
      {
        return false;
      }

      Type tokenCollectionType = tokens.GetType();
      object? countValue = tokenCollectionType.InvokeMember("Count", BindingFlags.GetProperty, null, tokens, null);
      int count = countValue is int typedCount ? typedCount : 0;
      for (int index = 0; index < count; index++)
      {
        token = tokenCollectionType.InvokeMember("Item", BindingFlags.InvokeMethod, null, tokens, new object[] { index });
        if (token == null)
        {
          continue;
        }

        if (string.Equals(GetSapiTokenId(token), tokenId, StringComparison.OrdinalIgnoreCase))
        {
          voiceType.InvokeMember("Voice", BindingFlags.SetProperty, null, voice, new[] { token });
          return true;
        }

        if (Marshal.IsComObject(token))
        {
          Marshal.ReleaseComObject(token);
        }

        token = null;
      }

      return false;
    }
    catch
    {
      return false;
    }
    finally
    {
      if (token != null && Marshal.IsComObject(token))
      {
        Marshal.ReleaseComObject(token);
      }

      if (tokens != null && Marshal.IsComObject(tokens))
      {
        Marshal.ReleaseComObject(tokens);
      }
    }
  }

  private static List<SapiVoiceOption> GetSapiVoiceOptions()
  {
    List<SapiVoiceOption> voiceOptions = new List<SapiVoiceOption>();
    object? voice = null;
    object? tokens = null;
    object? token = null;
    try
    {
      Type? voiceType = Type.GetTypeFromProgID("SAPI.SpVoice");
      if (voiceType == null)
      {
        return voiceOptions;
      }

      voice = Activator.CreateInstance(voiceType);
      if (voice == null)
      {
        return voiceOptions;
      }

      tokens = voiceType.InvokeMember(
        "GetVoices",
        BindingFlags.InvokeMethod,
        null,
        voice,
        new object[] { string.Empty, string.Empty });
      if (tokens == null)
      {
        return voiceOptions;
      }

      Type tokenCollectionType = tokens.GetType();
      object? countValue = tokenCollectionType.InvokeMember("Count", BindingFlags.GetProperty, null, tokens, null);
      int count = countValue is int typedCount ? typedCount : 0;
      for (int index = 0; index < count; index++)
      {
        token = tokenCollectionType.InvokeMember("Item", BindingFlags.InvokeMethod, null, tokens, new object[] { index });
        if (token == null)
        {
          continue;
        }

        string tokenId = GetSapiTokenId(token);
        string languageId = GetSapiTokenAttribute(token, "Language");
        string name = GetSapiTokenAttribute(token, "Name");
        string description = GetSapiTokenDescription(token);
        if (!string.IsNullOrWhiteSpace(tokenId) && !string.IsNullOrWhiteSpace(languageId))
        {
          voiceOptions.Add(new SapiVoiceOption(
            tokenId,
            languageId,
            CreateVoiceDisplayName(name, description)));
        }

        if (Marshal.IsComObject(token))
        {
          Marshal.ReleaseComObject(token);
        }

        token = null;
      }
    }
    catch
    {
      return voiceOptions;
    }
    finally
    {
      if (token != null && Marshal.IsComObject(token))
      {
        Marshal.ReleaseComObject(token);
      }

      if (tokens != null && Marshal.IsComObject(tokens))
      {
        Marshal.ReleaseComObject(tokens);
      }

      if (voice != null && Marshal.IsComObject(voice))
      {
        Marshal.ReleaseComObject(voice);
      }
    }

    return voiceOptions;
  }

  private static string GetSapiTokenId(object token)
  {
    try
    {
      object? tokenId = token.GetType().InvokeMember("Id", BindingFlags.GetProperty, null, token, null);
      return tokenId?.ToString() ?? string.Empty;
    }
    catch
    {
      return string.Empty;
    }
  }

  private static string GetSapiTokenAttribute(object token, string attributeName)
  {
    try
    {
      object? attributeValue = token.GetType().InvokeMember(
        "GetAttribute",
        BindingFlags.InvokeMethod,
        null,
        token,
        new object[] { attributeName });
      return attributeValue?.ToString() ?? string.Empty;
    }
    catch
    {
      return string.Empty;
    }
  }

  private static string GetSapiTokenDescription(object token)
  {
    try
    {
      object? description = token.GetType().InvokeMember(
        "GetDescription",
        BindingFlags.InvokeMethod,
        null,
        token,
        null);
      return description?.ToString() ?? string.Empty;
    }
    catch
    {
      return string.Empty;
    }
  }

  private static string CreateVoiceDisplayName(string name, string description)
  {
    string displayName = string.IsNullOrWhiteSpace(name) ? description : name;
    if (string.IsNullOrWhiteSpace(displayName))
    {
      return "Windows";
    }

    displayName = displayName.Replace("Microsoft Server Speech Text to Speech Voice", string.Empty, StringComparison.OrdinalIgnoreCase);
    displayName = displayName.Replace("Microsoft", string.Empty, StringComparison.OrdinalIgnoreCase);
    displayName = displayName.Replace("Desktop", string.Empty, StringComparison.OrdinalIgnoreCase);
    displayName = displayName.Trim(' ', '-', '(', ')');
    int commaIndex = displayName.LastIndexOf(',');
    if (commaIndex >= 0 && commaIndex + 1 < displayName.Length)
    {
      displayName = displayName[(commaIndex + 1)..].Trim(' ', '-', '(', ')');
    }

    int dashIndex = displayName.IndexOf(" - ", StringComparison.Ordinal);
    if (dashIndex > 0)
    {
      displayName = displayName[..dashIndex].Trim();
    }

    return string.IsNullOrWhiteSpace(displayName) ? "Windows" : displayName;
  }

  private static string? FindFirstVoiceSoundKind(List<SapiVoiceOption> voiceOptions, string[] languageIds)
  {
    foreach (SapiVoiceOption voiceOption in voiceOptions)
    {
      if (IsLanguageMatch(voiceOption.LanguageId, languageIds))
      {
        return CreateVoiceSoundKind(voiceOption);
      }
    }

    return null;
  }

  private static List<WindowsVoiceOption> GetWindowsVoiceOptions()
  {
    List<WindowsVoiceOption> voiceOptions = new List<WindowsVoiceOption>();
    try
    {
      foreach (VoiceInformation voice in SpeechSynthesizer.AllVoices)
      {
        if (!string.IsNullOrWhiteSpace(voice.Id) && !string.IsNullOrWhiteSpace(voice.Language))
        {
          voiceOptions.Add(new WindowsVoiceOption(
            voice.Id,
            voice.Language,
            CreateVoiceDisplayName(voice.DisplayName, voice.Description)));
        }
      }
    }
    catch
    {
      return voiceOptions;
    }

    return voiceOptions;
  }

  private static string? FindFirstWindowsVoiceSoundKind(List<WindowsVoiceOption> voiceOptions, string[] cultureNames)
  {
    foreach (WindowsVoiceOption voiceOption in voiceOptions)
    {
      if (IsCultureMatch(voiceOption.Language, cultureNames))
      {
        return CreateWindowsVoiceSoundKind(voiceOption);
      }
    }

    return null;
  }

  private static bool HasVoiceOption(List<SapiVoiceOption> voiceOptions, string languageId, string tokenId)
  {
    foreach (SapiVoiceOption voiceOption in voiceOptions)
    {
      if (string.Equals(voiceOption.LanguageId, languageId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(voiceOption.TokenId, tokenId, StringComparison.OrdinalIgnoreCase))
      {
        return true;
      }
    }

    return false;
  }

  private static bool HasWindowsVoiceOption(List<WindowsVoiceOption> voiceOptions, string language, string voiceId)
  {
    foreach (WindowsVoiceOption voiceOption in voiceOptions)
    {
      if (string.Equals(voiceOption.Language, language, StringComparison.OrdinalIgnoreCase)
        && string.Equals(voiceOption.VoiceId, voiceId, StringComparison.OrdinalIgnoreCase))
      {
        return true;
      }
    }

    return false;
  }

  private static bool IsLanguageMatch(string languageId, string[] languageIds)
  {
    foreach (string candidateLanguageId in languageIds)
    {
      if (string.Equals(languageId, candidateLanguageId, StringComparison.OrdinalIgnoreCase))
      {
        return true;
      }
    }

    return false;
  }

  private static bool IsCultureMatch(string cultureName, string[] cultureNames)
  {
    foreach (string candidateCultureName in cultureNames)
    {
      if (string.Equals(cultureName, candidateCultureName, StringComparison.OrdinalIgnoreCase))
      {
        return true;
      }
    }

    return false;
  }

  private static string CreateVoiceSoundKind(SapiVoiceOption voiceOption)
  {
    return $"{ActivitySoundReadActivityVoicePrefix}{voiceOption.LanguageId}|{voiceOption.TokenId}";
  }

  private static string CreateWindowsVoiceSoundKind(WindowsVoiceOption voiceOption)
  {
    return $"{ActivitySoundReadActivityWindowsVoicePrefix}{voiceOption.Language}|{voiceOption.VoiceId}";
  }

  private static bool TryParseVoiceSoundKind(string soundKind, out string languageId, out string tokenId)
  {
    languageId = string.Empty;
    tokenId = string.Empty;
    if (!soundKind.StartsWith(ActivitySoundReadActivityVoicePrefix, StringComparison.Ordinal))
    {
      return false;
    }

    string voiceData = soundKind[ActivitySoundReadActivityVoicePrefix.Length..];
    string[] parts = voiceData.Split(new[] { '|' }, 2);
    if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
    {
      return false;
    }

    languageId = parts[0];
    tokenId = parts[1];
    return true;
  }

  private static bool TryParseWindowsVoiceSoundKind(string soundKind, out string language, out string voiceId)
  {
    language = string.Empty;
    voiceId = string.Empty;
    if (!soundKind.StartsWith(ActivitySoundReadActivityWindowsVoicePrefix, StringComparison.Ordinal))
    {
      return false;
    }

    string voiceData = soundKind[ActivitySoundReadActivityWindowsVoicePrefix.Length..];
    string[] parts = voiceData.Split(new[] { '|' }, 2);
    if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
    {
      return false;
    }

    language = parts[0];
    voiceId = parts[1];
    return true;
  }

  private static string TranslateActivityTitleToEnglish(string title)
  {
    if (string.IsNullOrWhiteSpace(title))
    {
      return "Free time";
    }

    string trimmedTitle = title.Trim();
    if (EnglishActivityTranslations.TryGetValue(trimmedTitle, out string? exactTranslation))
    {
      return exactTranslation;
    }

    StringBuilder translatedTitle = new StringBuilder(trimmedTitle.Length);
    StringBuilder currentWord = new StringBuilder();
    foreach (char character in trimmedTitle)
    {
      if (char.IsLetterOrDigit(character))
      {
        currentWord.Append(character);
        continue;
      }

      AppendTranslatedActivityWord(translatedTitle, currentWord);
      translatedTitle.Append(character);
    }

    AppendTranslatedActivityWord(translatedTitle, currentWord);
    return translatedTitle.ToString();
  }

  private static void AppendTranslatedActivityWord(StringBuilder translatedTitle, StringBuilder currentWord)
  {
    if (currentWord.Length == 0)
    {
      return;
    }

    string word = currentWord.ToString();
    string normalizedWord = RemoveDiacritics(word);
    if (EnglishActivityTranslations.TryGetValue(word, out string? translation)
      || EnglishActivityTranslations.TryGetValue(normalizedWord, out translation))
    {
      translatedTitle.Append(translation);
    }
    else
    {
      translatedTitle.Append(word);
    }

    currentWord.Clear();
  }

  private static string RemoveDiacritics(string value)
  {
    string normalizedValue = value.Normalize(NormalizationForm.FormD);
    StringBuilder builder = new StringBuilder(normalizedValue.Length);
    foreach (char character in normalizedValue)
    {
      UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
      if (category != UnicodeCategory.NonSpacingMark)
      {
        builder.Append(character);
      }
    }

    return builder.ToString().Normalize(NormalizationForm.FormC);
  }

  private void ToggleActivitySound()
  {
    this.settings.IsActivitySoundMuted = !this.settings.IsActivitySoundMuted;
    this.settingsStore.Save(this.settings);
    this.ActivitySoundEnabledCheckBox.IsChecked = !this.settings.IsActivitySoundMuted;
    this.UpdateSoundToggleButton();
  }

  private void UpdateSoundToggleButton()
  {
    if (this.SoundToggleButton == null)
    {
      return;
    }

    this.SoundToggleButton.Content = this.settings.IsActivitySoundMuted ? "\uE74F" : "\uE767";
    this.SoundToggleButton.ToolTip = this.settings.IsActivitySoundMuted
      ? "Alertas sonoros desativados"
      : "Configurar alerta sonoro";
  }

  private void UpdateActivitySoundVolumeText()
  {
    if (this.ActivitySoundVolumeText == null)
    {
      return;
    }

    this.ActivitySoundVolumeText.Text = $"Volume {this.settings.ActivitySoundVolumePercent}%";
  }

  private void SetLayoutDirty(bool isDirty)
  {
    if (!this.layoutChangeTrackingEnabled && isDirty)
    {
      return;
    }

    if (this.suppressLayoutChangeTracking && isDirty)
    {
      return;
    }

    this.hasUnsavedLayoutChanges = isDirty;
    this.UpdateLayoutCommandButtons();
  }

  private void UpdateLayoutCommandButtons()
  {
    if (this.SaveLayoutButton == null || this.RestoreLayoutButton == null)
    {
      return;
    }

    this.SaveLayoutButton.IsEnabled = this.hasUnsavedLayoutChanges;
    this.RestoreLayoutButton.IsEnabled = this.hasUnsavedLayoutChanges;
    this.SaveLayoutButton.ToolTip = this.hasUnsavedLayoutChanges
      ? "Salvar layout"
      : "Layout salvo";
    this.RestoreLayoutButton.ToolTip = this.hasUnsavedLayoutChanges
      ? "Restaurar layout salvo"
      : "Sem alterações de layout";
  }

  private void UpdateProgressVisual(int progress)
  {
    this.currentProgressPercent = Math.Clamp(progress, 0, 100);
    this.ProgressPercentText.Text = $"{this.currentProgressPercent}%";

    this.ProgressRingArc.Data = CreateProgressRingGeometry(this.currentProgressPercent, 32, 29);

    if (this.CurrentProgressGrid.Visibility != Visibility.Visible)
    {
      this.CurrentProgressFill.Width = 0;
      return;
    }

    double trackWidth = this.CurrentProgressTrack.ActualWidth;
    this.CurrentProgressFill.Width = trackWidth <= 0
      ? 0
      : trackWidth * this.currentProgressPercent / 100;
    if (trackWidth <= 0 && !this.pendingProgressLayoutRefresh && this.IsLoaded)
    {
      this.pendingProgressLayoutRefresh = true;
      this.Dispatcher.BeginInvoke(
        () =>
        {
          this.pendingProgressLayoutRefresh = false;
          this.UpdateProgressVisual(this.currentProgressPercent);
        },
        DispatcherPriority.Loaded);
    }
  }

  private static Geometry CreateProgressRingGeometry(
    int progress,
    double center,
    double radius)
  {
    if (progress <= 0)
    {
      return Geometry.Empty;
    }

    if (progress >= 100)
    {
      return new EllipseGeometry(new Point(center, center), radius, radius);
    }

    double angle = (progress / 100d * 359.9d) - 90d;
    double radians = angle * Math.PI / 180d;
    Point startPoint = new Point(center, center - radius);
    Point endPoint = new Point(
      center + (radius * Math.Cos(radians)),
      center + (radius * Math.Sin(radians)));

    PathFigure figure = new PathFigure
    {
      StartPoint = startPoint,
      IsClosed = false,
      IsFilled = false,
    };
    figure.Segments.Add(new ArcSegment
    {
      Point = endPoint,
      Size = new Size(radius, radius),
      IsLargeArc = progress > 50,
      SweepDirection = SweepDirection.Clockwise,
    });

    PathGeometry geometry = new PathGeometry();
    geometry.Figures.Add(figure);
    return geometry;
  }

  private void RenderAdaptiveCollections(TodayWidgetSnapshotDto snapshot)
  {
    int timelineLimit = Math.Min(10, Math.Max(1, snapshot.Timeline.Count));
    int secondaryLimit = this.GetAdaptiveLimit(1, 3, 4);
    bool compactHeight = this.ActualHeight > 0 && this.ActualHeight < CompactLayoutMaxHeight;
    bool useDetailedTimeline = this.ActualWidth >= 380 && !compactHeight;
    double timelineItemWidth = this.GetTimelineItemWidth(timelineLimit);
    bool showExpandedDetails = timelineItemWidth >= 180 && this.ActualHeight >= 470;
    bool showActionDetails = this.ActionsCard.ActualWidth >= 240 || this.ActionsCard.ActualHeight >= 104;
    double focusTextWidth = this.GetFocusTextWidth();
    double timelineItemHeight = showExpandedDetails ? 72 : 58;

    this.TimelineItemsControl.ItemsSource = snapshot.Timeline
      .Take(timelineLimit)
      .Select(item => new CompactWidgetItem
      {
        Extra = showExpandedDetails ? $"{item.StartLocal:HH:mm}-{item.EndLocal:HH:mm} · {item.DurationLabel}" : string.Empty,
        Height = timelineItemHeight,
        Icon = ResolveIcon(item.Title, item.Category, item.IsCalendar),
        Width = timelineItemWidth,
        Time = item.StartLocal.ToString("HH:mm"),
        Title = item.Title,
        Detail = useDetailedTimeline ? $"{item.Category} · {item.Source}" : item.Category,
      })
      .ToList();
    this.FocusItemsControl.ItemsSource = snapshot.Focuses
      .Take(secondaryLimit)
      .Select(item => new CompactFocusItem
      {
        FullText = item.Suggested ? $"{item.Text} · sugerido" : item.Text,
        Text = item.Suggested ? $"{item.Text} · sugerido" : item.Text,
        Width = focusTextWidth,
      })
      .ToList();
    this.ActionItemsControl.ItemsSource = snapshot.Actions.Count == 0
      ? new List<CompactActionItem>
      {
        new CompactActionItem
        {
          Title = "Nada crítico",
          Detail = "Rotina livre para seguir.",
        },
      }
      : snapshot.Actions.Take(secondaryLimit).Select(item => new CompactActionItem
      {
        FullTitle = item.Title,
        Title = item.Title,
        Detail = showActionDetails ? item.Detail : string.Empty,
        Evidence = showActionDetails ? item.Evidence : string.Empty,
      }).ToList();

    this.RenderLooseInsightCards(snapshot);
  }

  private void RenderLooseInsightCards(TodayWidgetSnapshotDto snapshot)
  {
    List<TodayWidgetInsightDto> insights = snapshot.Insights.Take(10).ToList();
    HashSet<string> activeKeys = new HashSet<string>(StringComparer.Ordinal);
    for (int index = 0; index < insights.Count; index++)
    {
      TodayWidgetInsightDto insight = insights[index];
      string key = GetInsightCardKey(insight);
      activeKeys.Add(key);
      CompactInsightItem item = this.ToCompactInsightItem(insight, 236, 88);
      if (this.movableCards.TryGetValue(key, out MovableWidgetCard? existingCard))
      {
        if (!existingCard.IsCollapsed)
        {
          this.UpdateLooseInsightCard(existingCard.Element, item);
        }

        continue;
      }

      Border cardElement = this.CreateLooseInsightCard(key, item);
      this.MovableCardsCanvas.Children.Add(cardElement);
      this.RegisterMovableCard(key, cardElement, 160, 38, 236, 88);
      Canvas.SetLeft(cardElement, 28 + (index % 3 * 252));
      Canvas.SetTop(cardElement, 430 + (index / 3 * 102));
      this.ApplySavedLayoutForCard(this.movableCards[key]);
    }

    foreach (string key in this.liveInsightCardKeys.Except(activeKeys).ToList())
    {
      if (this.movableCards.TryGetValue(key, out MovableWidgetCard? card))
      {
        this.MovableCardsCanvas.Children.Remove(card.Element);
        this.movableCards.Remove(key);
      }
    }

    this.liveInsightCardKeys.Clear();
    foreach (string key in activeKeys)
    {
      this.liveInsightCardKeys.Add(key);
    }
  }

  private CompactInsightItem ToCompactInsightItem(
    TodayWidgetInsightDto item,
    double width,
    double height)
  {
    InsightTonePalette palette = ResolveInsightTonePalette(item.Tone);
    string detail = FormatInsightDetail(item);
    string context = ResolveInsightContext(item);
    return new CompactInsightItem
    {
      Background = palette.Background,
      BorderBrush = palette.BorderBrush,
      Context = context,
      Detail = detail,
      Foreground = palette.Foreground,
      Height = height,
      Href = item.Href,
      Icon = ResolveInsightIcon(item),
      IconBackground = palette.IconBackground,
      MutedForeground = palette.MutedForeground,
      Title = item.Title.ToUpperInvariant(),
      Tooltip = $"{item.Title}: {item.Value} · {detail}. {context}",
      Value = item.Value,
      Width = width,
    };
  }

  private Border CreateLooseInsightCard(string key, CompactInsightItem item)
  {
    Border card = new Border
    {
      Background = (Brush)new BrushConverter().ConvertFromString(item.Background)!,
      BorderBrush = (Brush)new BrushConverter().ConvertFromString(item.BorderBrush)!,
      BorderThickness = new Thickness(1),
      CornerRadius = new CornerRadius(16),
      Padding = new Thickness(0),
      ToolTip = item.Tooltip,
      Width = item.Width,
      Height = item.Height,
      Tag = key,
    };
    Grid root = new Grid
    {
      ClipToBounds = true,
    };
    Grid mediaLayer = this.CreateInsightMediaLayer(item);
    root.Children.Add(mediaLayer);
    Grid contentGrid = new Grid
    {
      ClipToBounds = true,
      Margin = new Thickness(8),
    };
    contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
    contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    Border iconBadge = new Border
    {
      Width = 28,
      Height = 28,
      Background = (Brush)new BrushConverter().ConvertFromString(item.IconBackground)!,
      BorderBrush = (Brush)this.FindResource("GlassStrokeBrush"),
      BorderThickness = new Thickness(1),
      CornerRadius = new CornerRadius(10),
    };
    TextBlock icon = new TextBlock
    {
      FontFamily = new FontFamily("Segoe MDL2 Assets"),
      FontSize = 13,
      Foreground = (Brush)new BrushConverter().ConvertFromString(item.Foreground)!,
      HorizontalAlignment = HorizontalAlignment.Center,
      Text = item.Icon,
      VerticalAlignment = VerticalAlignment.Center,
    };
    iconBadge.Child = icon;
    Grid.SetColumn(iconBadge, 0);
    StackPanel textPanel = new StackPanel
    {
      Margin = new Thickness(8, 0, 0, 0),
    };
    textPanel.Children.Add(new TextBlock
    {
      FontSize = 10,
      FontWeight = FontWeights.SemiBold,
      Foreground = (Brush)new BrushConverter().ConvertFromString(item.MutedForeground)!,
      Text = item.Title,
      TextTrimming = TextTrimming.CharacterEllipsis,
    });
    textPanel.Children.Add(new TextBlock
    {
      FontFamily = new FontFamily("Cascadia Mono, Consolas"),
      FontSize = 14,
      FontWeight = FontWeights.SemiBold,
      Foreground = (Brush)this.FindResource("PrimaryTextBrush"),
      Text = item.Value,
      TextTrimming = TextTrimming.CharacterEllipsis,
    });
    textPanel.Children.Add(new TextBlock
    {
      FontSize = 10,
      Foreground = (Brush)new BrushConverter().ConvertFromString(item.MutedForeground)!,
      Text = item.Detail,
      TextTrimming = TextTrimming.CharacterEllipsis,
    });
    textPanel.Children.Add(new TextBlock
    {
      FontSize = 9,
      Foreground = (Brush)new BrushConverter().ConvertFromString(item.MutedForeground)!,
      Opacity = 0.88,
      Text = item.Context,
      TextTrimming = TextTrimming.CharacterEllipsis,
    });
    Grid.SetColumn(textPanel, 1);
    contentGrid.Children.Add(iconBadge);
    contentGrid.Children.Add(textPanel);
    root.Children.Add(contentGrid);
    card.Child = root;
    return card;
  }

  private void UpdateLooseInsightCard(FrameworkElement element, CompactInsightItem item)
  {
    if (element is not Border card || card.Child is not Grid root)
    {
      return;
    }

    card.Background = (Brush)new BrushConverter().ConvertFromString(item.Background)!;
    card.BorderBrush = (Brush)new BrushConverter().ConvertFromString(item.BorderBrush)!;
    card.Padding = new Thickness(0);
    card.ToolTip = item.Tooltip;
    if (root.Children.Count > 0)
    {
      Grid mediaLayer = this.CreateInsightMediaLayer(item);
      root.Children.RemoveAt(0);
      root.Children.Insert(0, mediaLayer);
    }

    if (root.Children.Count < 2
      || root.Children[1] is not Grid contentGrid
      || contentGrid.Children.Count < 2
      || contentGrid.Children[1] is not StackPanel textPanel)
    {
      return;
    }

    SetText(textPanel, 0, item.Title);
    SetText(textPanel, 1, item.Value);
    SetText(textPanel, 2, item.Detail);
    SetText(textPanel, 3, item.Context);
  }

  private Grid CreateInsightMediaLayer(CompactInsightItem item)
  {
    Grid layer = new Grid
    {
      IsHitTestVisible = false,
      ClipToBounds = true,
    };
    ImageSource? textureSource = CreateLocalImageSource(ResolveInsightImagePath(item.Title));
    if (textureSource != null)
    {
      Border cardShade = new Border
      {
        Background = new SolidColorBrush(Color.FromArgb(112, 0, 0, 0)),
      };
      Image texture = new Image
      {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Opacity = 0.74,
        Source = textureSource,
        Stretch = Stretch.UniformToFill,
        VerticalAlignment = VerticalAlignment.Stretch,
      };
      texture.OpacityMask = new LinearGradientBrush
      {
        StartPoint = new Point(0, 0),
        EndPoint = new Point(1, 0),
        GradientStops = new GradientStopCollection
        {
          new GradientStop(Color.FromArgb(36, 0, 0, 0), 0),
          new GradientStop(Color.FromArgb(154, 0, 0, 0), 0.42),
          new GradientStop(Color.FromArgb(255, 0, 0, 0), 1),
        },
      };
      Border darkOverlay = new Border
      {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Background = new SolidColorBrush(Color.FromArgb(92, 0, 0, 0)),
        VerticalAlignment = VerticalAlignment.Stretch,
      };
      darkOverlay.OpacityMask = new LinearGradientBrush
      {
        StartPoint = new Point(0, 0),
        EndPoint = new Point(1, 0),
        GradientStops = new GradientStopCollection
        {
          new GradientStop(Color.FromArgb(0, 0, 0, 0), 0),
          new GradientStop(Color.FromArgb(128, 0, 0, 0), 0.3),
          new GradientStop(Color.FromArgb(255, 0, 0, 0), 1),
        },
      };
      layer.Children.Add(cardShade);
      layer.Children.Add(texture);
      layer.Children.Add(darkOverlay);
    }

    Border glow = new Border
    {
      Width = 3,
      HorizontalAlignment = HorizontalAlignment.Left,
      Background = (Brush)new BrushConverter().ConvertFromString(item.Foreground)!,
      Opacity = 0.46,
    };
    layer.Children.Add(glow);
    return layer;
  }

  private static ImageSource? CreateLocalImageSource(string path)
  {
    string fullPath = System.IO.Path.Combine(AppContext.BaseDirectory, path.Replace('/', System.IO.Path.DirectorySeparatorChar));
    if (!File.Exists(fullPath))
    {
      return null;
    }

    BitmapImage image = new BitmapImage();
    image.BeginInit();
    image.CacheOption = BitmapCacheOption.OnLoad;
    image.UriSource = new Uri(fullPath, UriKind.Absolute);
    image.EndInit();
    return image;
  }

  private static string ResolveHeroImagePath(string? title, string? category)
  {
    string text = $"{title} {category}".ToUpperInvariant();
    if (text.Contains("ALMOÇO", StringComparison.Ordinal) || text.Contains("COMER", StringComparison.Ordinal))
    {
      return "images/meal-table.webp";
    }

    if (text.Contains("SAÚDE", StringComparison.Ordinal) || text.Contains("ATIVIDADE", StringComparison.Ordinal))
    {
      return "images/health-motion.webp";
    }

    if (text.Contains("CASA", StringComparison.Ordinal) || text.Contains("SERVI", StringComparison.Ordinal))
    {
      return "images/home-interior.webp";
    }

    if (text.Contains("CURSO", StringComparison.Ordinal) || text.Contains("INGL", StringComparison.Ordinal))
    {
      return "images/study-desk.webp";
    }

    return "images/work-desk.webp";
  }

  private static string ResolveInsightImagePath(string title)
  {
    string normalizedTitle = title.ToUpperInvariant();
    if (normalizedTitle.Contains("SALDO", StringComparison.Ordinal)
      || normalizedTitle.Contains("GASTOS", StringComparison.Ordinal))
    {
      return "images/finance-wallet.webp";
    }

    if (normalizedTitle.Contains("RECEITA", StringComparison.Ordinal)
      || normalizedTitle.Contains("APORTE", StringComparison.Ordinal))
    {
      return "images/finance-paper.webp";
    }

    if (normalizedTitle.Contains("CAIXA", StringComparison.Ordinal))
    {
      return "images/cash-texture.webp";
    }

    if (normalizedTitle.Contains("INVEST", StringComparison.Ordinal)
      || normalizedTitle.Contains("PATRIM", StringComparison.Ordinal))
    {
      return "images/investment-glass.webp";
    }

    if (normalizedTitle.Contains("RESERVA", StringComparison.Ordinal)
      || normalizedTitle.Contains("SINAIS", StringComparison.Ordinal))
    {
      return "images/reserve-texture.webp";
    }

    return "images/finance-wallet.webp";
  }

  private static string FormatInsightDetail(TodayWidgetInsightDto item)
  {
    string title = item.Title.ToUpperInvariant();
    if (title.Contains("SINAIS", StringComparison.Ordinal))
    {
      return "alertas financeiros para revisar";
    }

    if (title.Contains("SALDO", StringComparison.Ordinal))
    {
      return "receitas menos gastos do mês";
    }

    if (title.Contains("RECEITA", StringComparison.Ordinal))
    {
      return "entradas operacionais no mês";
    }

    if (title.Contains("GASTOS", StringComparison.Ordinal))
    {
      return "saídas operacionais no mês";
    }

    if (title.Contains("CAIXA", StringComparison.Ordinal))
    {
      return "dinheiro disponível nas contas";
    }

    if (title.Contains("RESERVA", StringComparison.Ordinal))
    {
      return string.IsNullOrWhiteSpace(item.Detail)
        ? "valor acumulado da meta"
        : item.Detail;
    }

    if (title.Contains("PATRIM", StringComparison.Ordinal))
    {
      return "posição financeira consolidada";
    }

    return item.Detail;
  }

  private static string ResolveInsightContext(TodayWidgetInsightDto item)
  {
    string title = item.Title.ToUpperInvariant();
    if (title.Contains("SINAIS", StringComparison.Ordinal))
    {
      return "priorize pendências críticas";
    }

    if (title.Contains("SALDO", StringComparison.Ordinal))
    {
      return item.Value.TrimStart().StartsWith("-", StringComparison.Ordinal)
        ? "resultado negativo no período"
        : "resultado positivo no período";
    }

    if (title.Contains("RECEITA", StringComparison.Ordinal))
    {
      return "classificado no fluxo financeiro";
    }

    if (title.Contains("GASTOS", StringComparison.Ordinal))
    {
      return "despesas classificadas";
    }

    if (title.Contains("CAIXA", StringComparison.Ordinal))
    {
      return "inclui contas consideradas no plano";
    }

    if (title.Contains("RESERVA", StringComparison.Ordinal))
    {
      return "acompanha a meta de segurança";
    }

    if (title.Contains("PATRIM", StringComparison.Ordinal))
    {
      return "contas e investimentos consolidados";
    }

    return "indicador do planejamento";
  }

  private static void SetText(StackPanel panel, int index, string text)
  {
    if (panel.Children.Count > index && panel.Children[index] is TextBlock textBlock)
    {
      textBlock.Text = text;
    }
  }

  private static string GetInsightCardKey(TodayWidgetInsightDto item)
  {
    string identity = string.IsNullOrWhiteSpace(item.Href)
      ? item.Title
      : item.Href;
    return $"{InsightCardKeyPrefix}{identity}";
  }

  private int GetAdaptiveLimit(int tiny, int normal, int expanded)
  {
    if (this.ActualWidth < 420 && this.ActualHeight < 560)
    {
      return tiny;
    }

    if (this.ActualHeight > 0 && this.ActualHeight < TinyLayoutMaxHeight)
    {
      return tiny;
    }

    if (this.ActualWidth >= WideLayoutMinWidth && this.ActualHeight >= 520)
    {
      return expanded;
    }

    return normal;
  }

  private double GetInsightCardWidth()
  {
    double availableWidth = this.InsightDeckPanel.ActualWidth > 0
      ? this.InsightDeckPanel.ActualWidth
      : this.ActualWidth - 36;
    if (availableWidth > 0 && this.ActualWidth >= 760)
    {
      int columns = this.ActualWidth >= 1200 ? 5 : this.ActualWidth >= 900 ? 4 : 3;
      double gapTotal = Math.Max(0, columns - 1) * 7;
      return Math.Clamp((availableWidth - gapTotal) / columns, 178, 260);
    }

    if (this.ActualWidth < 420)
    {
      return Math.Max(112, (this.ActualWidth - 58) / 2);
    }

    return 132;
  }

  private double GetTimelineItemWidth(int itemCount)
  {
    double availableWidth = this.TimelineCard.ActualWidth > 0
      ? this.TimelineCard.ActualWidth - 22
      : this.ActualWidth - 58;
    if (availableWidth <= 0)
    {
      return 132;
    }

    int columns = availableWidth >= 760
      ? Math.Min(itemCount, 3)
      : availableWidth >= 410
        ? Math.Min(itemCount, 2)
        : 1;
    double gapTotal = Math.Max(0, columns - 1) * 6;
    return Math.Clamp((availableWidth - gapTotal) / columns, 190, 300);
  }

  private double GetFocusTextWidth()
  {
    double cardWidth = this.FocusCard.ActualWidth > 0
      ? this.FocusCard.ActualWidth - 56
      : this.ActualWidth - 76;
    if (cardWidth <= 0)
    {
      return 72;
    }

    return Math.Clamp(cardWidth, 72, 360);
  }

  private void ShowOfflineState(string message)
  {
    this.ShowSnapshotPanel();
    this.StatusText.Text = "Sem conexão";
    this.CurrentIconText.Text = "⌁";
    this.CurrentTitleText.Text = "Aguardando";
    this.CurrentDetailText.Text = message;
    this.UpdateProgressVisual(0);
    this.TodayCountText.Text = "-";
    this.RemainingText.Text = "-";
    this.SimilarCountText.Text = "-";
    this.NextTitleText.Text = "Abrir AppLifePlanner";
    this.NextDetailText.Text = "Verifique se o Web está rodando.";
    this.NextButton.Tag = string.Empty;
    this.TimelineItemsControl.ItemsSource = new List<CompactWidgetItem>();
    this.FocusItemsControl.ItemsSource = new List<CompactFocusItem>();
    this.ActionItemsControl.ItemsSource = new List<CompactActionItem>();
    this.InsightItemsControl.ItemsSource = new List<CompactInsightItem>();
    this.FooterText.Text = DateTime.Now.ToString("HH:mm:ss");
  }

  private static string ResolveIcon(string? title, string? category, bool isCalendar)
  {
    if (isCalendar)
    {
      return "\uE787";
    }

    string value = $"{title} {category}".ToLowerInvariant();
    if (value.Contains("trabalho", StringComparison.Ordinal) || value.Contains("work", StringComparison.Ordinal))
    {
      return "\uE821";
    }

    if (value.Contains("almoço", StringComparison.Ordinal) || value.Contains("lunch", StringComparison.Ordinal))
    {
      return "\uE8D4";
    }

    if (value.Contains("física", StringComparison.Ordinal) || value.Contains("exercise", StringComparison.Ordinal))
    {
      return "\uE95E";
    }

    if (value.Contains("casa", StringComparison.Ordinal) || value.Contains("home", StringComparison.Ordinal))
    {
      return "\uE80F";
    }

    if (value.Contains("curso", StringComparison.Ordinal) || value.Contains("study", StringComparison.Ordinal))
    {
      return "\uE82D";
    }

    if (value.Contains("futebol", StringComparison.Ordinal) || value.Contains("football", StringComparison.Ordinal))
    {
      return "\uE9CE";
    }

    return "\uE9D2";
  }

  private static string ResolveInsightIcon(TodayWidgetInsightDto item)
  {
    string value = $"{item.Title} {item.Detail} {item.Tone}".ToLowerInvariant();
    if (value.Contains("gasto", StringComparison.Ordinal)
      || value.Contains("despesa", StringComparison.Ordinal)
      || value.Contains("saldo", StringComparison.Ordinal)
      || value.Contains("danger", StringComparison.Ordinal))
    {
      return "\uE74D";
    }

    if (value.Contains("receita", StringComparison.Ordinal)
      || value.Contains("aporte", StringComparison.Ordinal)
      || value.Contains("positive", StringComparison.Ordinal))
    {
      return "\uE8E5";
    }

    if (value.Contains("patrim", StringComparison.Ordinal)
      || value.Contains("invest", StringComparison.Ordinal)
      || value.Contains("aloca", StringComparison.Ordinal))
    {
      return "\uE9D2";
    }

    return "\uE8A5";
  }

  private static InsightTonePalette ResolveInsightTonePalette(string tone)
  {
    return tone switch
    {
      "positive" => new InsightTonePalette("#08FFFFFF", "#14FFFFFF", "#34D399", "#CBD5E1", "#12FFFFFF"),
      "danger" => new InsightTonePalette("#08FFFFFF", "#14FFFFFF", "#F87171", "#CBD5E1", "#12FFFFFF"),
      "warning" => new InsightTonePalette("#08FFFFFF", "#14FFFFFF", "#FBBF24", "#CBD5E1", "#12FFFFFF"),
      "blue" => new InsightTonePalette("#08FFFFFF", "#14FFFFFF", "#60A5FA", "#CBD5E1", "#12FFFFFF"),
      "teal" => new InsightTonePalette("#08FFFFFF", "#14FFFFFF", "#2DD4BF", "#CBD5E1", "#12FFFFFF"),
      "purple" => new InsightTonePalette("#08FFFFFF", "#14FFFFFF", "#C4B5FD", "#CBD5E1", "#12FFFFFF"),
      "green" => new InsightTonePalette("#08FFFFFF", "#14FFFFFF", "#86EFAC", "#CBD5E1", "#12FFFFFF"),
      _ => new InsightTonePalette("#08FFFFFF", "#14FFFFFF", "#E2E8F0", "#CBD5E1", "#12FFFFFF"),
    };
  }

  private bool HasGitHubUpdateToken()
  {
    try
    {
      return !string.IsNullOrWhiteSpace(WidgetSettingsStore.Unprotect(this.settings.ProtectedGitHubToken));
    }
    catch
    {
      return false;
    }
  }

  private void ShowUpdateStatus(string text, bool requiresConfiguration)
  {
    this.UpdateStatusText.Text = text;
    this.UpdateStatusButton.Visibility = Visibility.Visible;
    this.UpdateStatusButton.IsEnabled = requiresConfiguration;
    this.UpdateStatusButton.ToolTip = requiresConfiguration
      ? "Configurar atualizações automáticas"
      : text;
  }

  private void HideUpdateStatus()
  {
    this.UpdateStatusButton.Visibility = Visibility.Collapsed;
  }

  private void BeginRefreshButtonProgress(string statusText)
  {
    this.refreshButtonBusyDepth++;
    this.RefreshButton.IsEnabled = false;
    this.RefreshButton.ToolTip = statusText;
    this.ShowUpdateStatus(statusText, false);
    this.FooterText.Text = statusText;
    this.FooterText.Visibility = Visibility.Visible;
    if (this.refreshButtonBusyDepth > 1)
    {
      return;
    }

    DoubleAnimation animation = new DoubleAnimation
    {
      From = 0,
      To = 360,
      Duration = TimeSpan.FromMilliseconds(900),
      RepeatBehavior = RepeatBehavior.Forever,
    };
    this.RefreshButtonRotation.BeginAnimation(RotateTransform.AngleProperty, animation);
  }

  private void EndRefreshButtonProgress(string statusText, bool requiresConfiguration)
  {
    this.refreshButtonBusyDepth = Math.Max(0, this.refreshButtonBusyDepth - 1);
    if (this.refreshButtonBusyDepth > 0)
    {
      return;
    }

    this.RefreshButtonRotation.BeginAnimation(RotateTransform.AngleProperty, null);
    this.RefreshButtonRotation.Angle = 0;
    this.RefreshButton.IsEnabled = true;
    this.RefreshButton.ToolTip = "Atualizar";
    this.ShowUpdateStatus(statusText, requiresConfiguration);
    this.FooterText.Text = statusText;
    this.FooterText.Visibility = Visibility.Visible;
  }

  private void CompleteUpdateStatus(
    string statusText,
    bool requiresConfiguration,
    bool showProgress)
  {
    if (showProgress)
    {
      this.EndRefreshButtonProgress(statusText, requiresConfiguration);
      return;
    }

    if (requiresConfiguration)
    {
      this.ShowUpdateStatus(statusText, true);
    }
  }

  private async Task CheckForUpdatesAsync(
    bool forceTokenValidation,
    bool showProgress)
  {
    if (!await this.updateGate.WaitAsync(0))
    {
      WidgetDiagnosticsLogger.Info("Update check skipped because another update check is running.");
      if (showProgress)
      {
        this.EndRefreshButtonProgress("Atualização já em andamento", false);
      }

      return;
    }

    try
    {
      if (showProgress)
      {
        this.BeginRefreshButtonProgress("Verificando release...");
      }

      WidgetDiagnosticsLogger.Info($"Update check started. ForceTokenValidation={forceTokenValidation}.");
      WidgetUpdateService updateService = new WidgetUpdateService(this.settingsStore);
      WidgetUpdateCheckResult result = await updateService.CheckForUpdatesAsync(
        this.settings,
        forceTokenValidation);
      WidgetDiagnosticsLogger.Info($"Update check completed. Result={result}.");
      switch (result)
      {
        case WidgetUpdateCheckResult.MissingGitHubToken:
          this.ClearGitHubUpdateToken();
          this.CompleteUpdateStatus("Atualizações desativadas", true, showProgress);
          return;

        case WidgetUpdateCheckResult.InvalidGitHubToken:
          this.ClearGitHubUpdateToken();
          this.CompleteUpdateStatus("Token de atualização recusado", true, showProgress);
          return;

        case WidgetUpdateCheckResult.NoUpdate:
          this.CompleteUpdateStatus("Widget já está atualizado", false, showProgress);
          return;

        case WidgetUpdateCheckResult.Skipped:
          this.CompleteUpdateStatus("Checagem recente", false, showProgress);
          return;

        case WidgetUpdateCheckResult.TokenValid:
          this.CompleteUpdateStatus("Token validado", false, showProgress);
          return;

        case WidgetUpdateCheckResult.UpdateApplied:
          this.CompleteUpdateStatus("Atualização aplicada", false, showProgress);
          return;
      }
    }
    catch (TimeoutException ex)
    {
      WidgetDiagnosticsLogger.Error("Update check timed out.", ex);
      this.CompleteUpdateStatus("Tempo esgotado ao verificar release", false, showProgress);
    }
    catch (Exception ex)
    {
      WidgetDiagnosticsLogger.Error("Update check failed.", ex);
      this.CompleteUpdateStatus("Falha ao verificar atualização", false, showProgress);
      // Update failures must not interrupt the always-visible daily widget.
    }
    finally
    {
      this.updateGate.Release();
    }
  }

  private async void OnPairClick(object sender, RoutedEventArgs e)
  {
    this.PairingStatusText.Text = this.requiresWidgetPairing ? "Pareando..." : "Salvando...";
    this.PairButton.IsEnabled = false;

    try
    {
      string baseUrl = this.BaseUrlTextBox.Text.Trim();
      string code = this.PairingCodeTextBox.Text.Trim();
      string gitHubToken = this.GitHubTokenBox.Password.Trim();
      if (string.IsNullOrWhiteSpace(gitHubToken))
      {
        this.PairingStatusText.Text = "Cole o token GitHub para ativar as atualizações automáticas.";
        return;
      }

      if (this.requiresWidgetPairing)
      {
        if (string.IsNullOrWhiteSpace(code))
        {
          this.PairingStatusText.Text = "Cole o código gerado no Perfil.";
          return;
        }

        WidgetPairingExchangeResponseDto response = await this.client.PairAsync(
          baseUrl,
          code,
          Environment.MachineName,
          CancellationToken.None);
        this.settings.BaseUrl = string.IsNullOrWhiteSpace(response.BaseUrl) ? baseUrl : response.BaseUrl;
        this.settings.ProtectedWidgetToken = WidgetSettingsStore.Protect(response.Token);
        this.widgetToken = response.Token;
      }
      else
      {
        this.settings.BaseUrl = string.IsNullOrWhiteSpace(baseUrl)
          ? WidgetSettings.DefaultBaseUrl
          : baseUrl;
      }

      this.settings.ProtectedGitHubToken = WidgetSettingsStore.Protect(gitHubToken);
      this.settings.LastUpdateCheckUtc = null;
      this.settingsStore.Save(this.settings);
      this.PairingStatusText.Text = "Configurado";
      this.ShowSnapshotPanel();
      this.HideUpdateStatus();
      if (!string.IsNullOrWhiteSpace(this.widgetToken))
      {
        await this.RefreshAsync("pairing");
      }

      _ = this.CheckForUpdatesAsync(true, false);
    }
    catch
    {
      this.PairingStatusText.Text = this.requiresWidgetPairing
        ? "Não foi possível parear. Confira o código e tente de novo."
        : "Não foi possível salvar. Confira o token GitHub e tente de novo.";
    }
    finally
    {
      this.PairButton.IsEnabled = true;
    }
  }

  private async void OnRefreshClick(object sender, RoutedEventArgs e)
  {
    this.BeginRefreshButtonProgress("Atualizando dados...");
    await this.RefreshAsync("button");
    this.EndRefreshButtonProgress("Dados atualizados", false);
    await this.CheckForUpdatesAsync(true, true);
  }

  private void OnSoundToggleClick(object sender, RoutedEventArgs e)
  {
    this.SoundSettingsPopup.IsOpen = true;
  }

  private void OnActivitySoundEnabledChanged(object sender, RoutedEventArgs e)
  {
    if (this.isApplyingSoundSettings)
    {
      return;
    }

    this.settings.IsActivitySoundMuted = this.ActivitySoundEnabledCheckBox.IsChecked != true;
    this.settingsStore.Save(this.settings);
    this.UpdateSoundToggleButton();
    if (!this.settings.IsActivitySoundMuted)
    {
      this.PlayActivitySoundPreview();
    }
  }

  private void OnActivitySoundKindChanged(object sender, SelectionChangedEventArgs e)
  {
    if (this.isApplyingSoundSettings)
    {
      return;
    }

    ComboBoxItem? selectedItem = this.ActivitySoundKindComboBox.SelectedItem as ComboBoxItem;
    string? selectedSoundKind = selectedItem?.Tag as string;
    if (string.IsNullOrWhiteSpace(selectedSoundKind) || !IsSupportedActivitySoundKind(selectedSoundKind))
    {
      return;
    }

    this.settings.ActivitySoundKind = selectedSoundKind;
    this.settingsStore.Save(this.settings);
    this.PlayActivitySoundPreview();
  }

  private void OnActivitySoundVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
  {
    if (this.isApplyingSoundSettings)
    {
      return;
    }

    this.settings.ActivitySoundVolumePercent = Math.Clamp((int)Math.Round(e.NewValue), 0, 100);
    this.settingsStore.Save(this.settings);
    this.UpdateActivitySoundVolumeText();
    this.pendingActivitySoundVolumePreview = true;
  }

  private void OnActivitySoundVolumeCommit(object sender, RoutedEventArgs e)
  {
    this.PlayPendingActivitySoundVolumePreview();
  }

  private void OnActivitySoundVolumeKeyUp(object sender, KeyEventArgs e)
  {
    if (e.Key == Key.Left
      || e.Key == Key.Right
      || e.Key == Key.Up
      || e.Key == Key.Down
      || e.Key == Key.PageUp
      || e.Key == Key.PageDown
      || e.Key == Key.Home
      || e.Key == Key.End)
    {
      this.PlayPendingActivitySoundVolumePreview();
    }
  }

  private void PlayPendingActivitySoundVolumePreview()
  {
    if (this.isApplyingSoundSettings || !this.pendingActivitySoundVolumePreview)
    {
      return;
    }

    this.pendingActivitySoundVolumePreview = false;
    this.PlayActivitySoundPreview();
  }

  private void OnSaveLayoutClick(object sender, RoutedEventArgs e)
  {
    this.SaveLayout();
  }

  private void OnRestoreLayoutClick(object sender, RoutedEventArgs e)
  {
    this.RestoreSavedLayout();
  }

  private void OnConfigureUpdatesClick(object sender, RoutedEventArgs e)
  {
    this.ShowPairingPanel(
      false,
      "Configurar atualização",
      "Cole um token GitHub com acesso ao repositório para ativar as atualizações automáticas.");
  }

  private void OnMinimizeClick(object sender, RoutedEventArgs e)
  {
    this.EnsureTaskbarPresence();
    this.WindowState = WindowState.Minimized;
  }

  private void ResetLocalPairing()
  {
    this.ClearWidgetToken();
    this.ClearGitHubUpdateToken();
    this.currentSnapshot = null;
    this.ShowPairingPanel(
      true,
      "Parear widget",
      "Cole o código do Perfil e o token GitHub uma vez.");
  }

  private void ClearWidgetToken()
  {
    this.settings.ProtectedWidgetToken = string.Empty;
    this.settingsStore.Save(this.settings);
    this.widgetToken = string.Empty;
  }

  private void ClearGitHubUpdateToken()
  {
    this.settings.ProtectedGitHubToken = string.Empty;
    this.settings.LastUpdateCheckUtc = null;
    this.settingsStore.Save(this.settings);
    this.GitHubTokenBox.Clear();
  }

  private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
  {
    if (e.ButtonState == MouseButtonState.Pressed)
    {
      this.DragMove();
    }
  }

  private sealed class CompactWidgetItem
  {
    public string Extra { get; set; } = string.Empty;

    public double Height { get; set; }

    public string Icon { get; set; } = string.Empty;

    public double Width { get; set; }

    public string Time { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;
  }

  private sealed class MovableWidgetCard
  {
    public MovableWidgetCard(
      string key,
      FrameworkElement element,
      TranslateTransform transform,
      double minWidth,
      double minHeight,
      double defaultWidth,
      double defaultHeight)
    {
      this.Key = key;
      this.Element = element;
      this.Transform = transform;
      this.MinWidth = minWidth;
      this.MinHeight = minHeight;
      this.DefaultWidth = defaultWidth;
      this.DefaultHeight = defaultHeight;
      if (element is Border border)
      {
        this.ExpandedContent = border.Child;
      }
    }

    public string Key { get; }

    public FrameworkElement Element { get; }

    public TranslateTransform Transform { get; }

    public double MinWidth { get; }

    public double MinHeight { get; }

    public double DefaultWidth { get; }

    public double DefaultHeight { get; }

    public UIElement? ExpandedContent { get; }

    public double? ExpandedWidth { get; set; }

    public double? ExpandedHeight { get; set; }

    public bool IsCollapsed { get; set; }
  }

  private sealed class ActivityTone
  {
    public ActivityTone(
      double frequency,
      TimeSpan duration,
      TimeSpan gapAfter,
      double amplitude)
    {
      this.Frequency = frequency;
      this.Duration = duration;
      this.GapAfter = gapAfter;
      this.Amplitude = amplitude;
    }

    public double Frequency { get; }

    public TimeSpan Duration { get; }

    public TimeSpan GapAfter { get; }

    public double Amplitude { get; }
  }

  private sealed class SapiVoiceOption
  {
    public SapiVoiceOption(string tokenId, string languageId, string displayName)
    {
      this.TokenId = tokenId;
      this.LanguageId = languageId;
      this.DisplayName = displayName;
    }

    public string TokenId { get; }

    public string LanguageId { get; }

    public string DisplayName { get; }
  }

  private sealed class WindowsVoiceOption
  {
    public WindowsVoiceOption(string voiceId, string language, string displayName)
    {
      this.VoiceId = voiceId;
      this.Language = language;
      this.DisplayName = displayName;
    }

    public string VoiceId { get; }

    public string Language { get; }

    public string DisplayName { get; }
  }

  private sealed class CompactFocusItem
  {
    public string FullText { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public double Width { get; set; }
  }

  private sealed class CompactActionItem
  {
    public string FullTitle { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    public string Evidence { get; set; } = string.Empty;
  }

  private sealed class CompactInsightItem
  {
    public string Background { get; set; } = string.Empty;

    public string BorderBrush { get; set; } = string.Empty;

    public string Context { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    public string Foreground { get; set; } = string.Empty;

    public double Height { get; set; }

    public string Href { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public string IconBackground { get; set; } = string.Empty;

    public string MutedForeground { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Tooltip { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public double Width { get; set; }
  }

  private sealed class InsightTonePalette
  {
    public InsightTonePalette(
      string background,
      string borderBrush,
      string foreground,
      string mutedForeground,
      string iconBackground)
    {
      this.Background = background;
      this.BorderBrush = borderBrush;
      this.Foreground = foreground;
      this.MutedForeground = mutedForeground;
      this.IconBackground = iconBackground;
    }

    public string Background { get; }

    public string BorderBrush { get; }

    public string Foreground { get; }

    public string MutedForeground { get; }

    public string IconBackground { get; }
  }
}
