//-----------------------------------------------------------------------
// <copyright file="App.xaml.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.WindowsWidget;

using System.Windows;
using Velopack;

/// <summary>
/// WPF application entry point for the AppLifePlanner Windows widget.
/// </summary>
public partial class App : Application
{
  /// <summary>
  /// Application entry point. Velopack hooks must run before WPF startup.
  /// </summary>
  /// <param name="args">Command-line arguments.</param>
  [STAThread]
  public static void Main(string[] args)
  {
    VelopackApp.Build().Run();

    App app = new App();
    app.InitializeComponent();
    app.Run();
  }

  /// <inheritdoc />
  protected override void OnStartup(StartupEventArgs e)
  {
    base.OnStartup(e);
    MainWindow window = new MainWindow();
    this.MainWindow = window;
    window.Show();
  }
}
