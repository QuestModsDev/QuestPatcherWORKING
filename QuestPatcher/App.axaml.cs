﻿using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using QuestPatcher.Core;
using QuestPatcher.Models;
using QuestPatcher.Resources;
using QuestPatcher.Services;
using Serilog;

namespace QuestPatcher
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void LogCriticalError(Exception ex)
        {
            try
            {
                // Save the exception to a file on your desktop.
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string guid = Guid.NewGuid().ToString();

                string crashPath = Path.Combine(desktopPath, $"QuestPatcher-CRASH-{guid}.txt");
                using var stream = File.Create(crashPath);
                using var writer = new StreamWriter(stream);

                var specialFolders = new SpecialFolders();

                writer.WriteLine($"QuestPatcher Unhandled Exception (version {VersionUtil.QuestPatcherVersion})");
                writer.WriteLine($"Full log here: {specialFolders.LogsFolder}");
                writer.WriteLine();
                writer.WriteLine(ex.ToString());
            }
            catch (Exception crashSaveEx)
            {
                Log.Fatal(crashSaveEx, "Failed to save crash log");
            }

            Log.Fatal(ex, "Unhandled exception!");
        }

        private void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs args)
        {
            if (!args.IsTerminating)
            {
                return;
            }

            LogCriticalError((Exception) args.ExceptionObject);
            if (args.IsTerminating)
            {
                Log.CloseAndFlush();
            }
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
        {
            LogCriticalError(args.Exception);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

                try
                {
                    var questPatcherService = new QuestPatcherUiService(desktop);
                    Avalonia.Logging.Logger.Sink = new SerilogSink
                    {
                        Logger = Log.Logger,
                        LogLevel = Serilog.Events.LogEventLevel.Warning
                    };

                    desktop.Exit += (_, _) =>
                    {
                        questPatcherService.CleanUp();
                    };
                }
                catch (Exception ex)
                {
                    // Load the default dark theme if we crashed so early in startup that themes hadn't yet been loaded
                    if (Styles.Count == 1)
                    {
                        Styles.Insert(0,
                            Theme.LoadEmbeddedTheme("Styles/Themes/QuestPatcherDark.axaml", "Dark").ThemeStying);
                    }

                    string title;
                    string text;
                    try
                    {
                        title = Strings.Loading_CriticalError_Title;
                        text = Strings.Loading_CriticalError_Text;
                    }
                    catch (Exception e)
                    {
                        // In case the resources failed to load
                        title = "Critical Error";
                        text = "QuestPatcher encountered a critical error during early startup, which was unrecoverable.";
                        Log.Error(e, "Failed to load critical error message from resource, using default");
                    }

                    var dialog = new DialogBuilder
                    {
                        Title = title,
                        Text = text,
                        HideCancelButton = true
                    };
                    dialog.WithException(ex);
                    dialog.OpenDialogue(null, WindowStartupLocation.CenterScreen);
                }
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
