using Microsoft.UI.Reactor;
using Microsoft.UI.Xaml.Controls;
using WinUIEx;
using static Microsoft.UI.Reactor.Factories;

namespace ReactorTrayWorker
{
    internal static class TrayService
    {
        internal static void InitializeWinUIWindow(Action stopHost)
        {
            ReactorApp.Run(_ =>
            {
                bool isQuitting = false;
                ReactorWindow? settingsWindow = null;
                settingsWindow = ReactorApp.OpenWindow(
                    new WindowSpec
                    {
                        Title = "My Worker Settings",
                        Width = 560,
                        Height = 460,
                        ActivateOnOpen = false,
                    },
                    () => new SettingsWindow(),
                    configure: host =>
                    {
                        // Configure Tray icon
                        string executablePath = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).DirectoryName!;
                        TrayIcon icon = new(0, Path.Combine(executablePath, @"trayicon.ico"), "My Worker")
                        {
                            IsVisible = true
                        };
                        var showWindow = static () => {
                            ReactorApp.PrimaryWindow?.Activate();
                            ReactorApp.PrimaryWindow?.NativeWindow.SetForegroundWindow();
                            ReactorApp.PrimaryWindow?.AppWindow.IsShownInSwitchers = true;
                        };
                        // Left click on the tray icon will show the window:
                        icon.Selected += (_, _) => showWindow();

                        // Context menu options:
                        icon.ContextMenu += (_, e) =>
                        {
                            MenuFlyout flyout = new();
                            flyout.Items.Add(new MenuFlyoutItem() { Text = "Open" });
                            ((MenuFlyoutItem)flyout.Items[0]).Click += (_, _) => showWindow();

                            flyout.Items.Add(new MenuFlyoutItem() { Text = "Quit" });
                            ((MenuFlyoutItem)flyout.Items[1]).Click += (_, _) =>
                            {
                                isQuitting = true;
                                ReactorApp.PrimaryWindow?.Close();
                                icon.Dispose();
                                stopHost.Invoke();
                            };
                            e.Flyout = flyout;
                        };

                        // Prevent closing out the window and just hide to tray instead unless we're quitting
                        host.Window.AppWindow.Closing += (_, e) =>
                        {
                            e.Cancel = !isQuitting;
                            settingsWindow?.Hide();
                        };

                        // If the window is minimized, we don't want it to show in the Alt+Tab switcher + taskbar,
                        // so we set IsShownInSwitchers to false when minimized.
                        WindowManager manager = WindowManager.Get(host.Window);
                        manager.WindowStateChanged += (_, state) =>
                        {
                            bool isVisible = state != WinUIEx.WindowState.Minimized;
                            manager.AppWindow.IsShownInSwitchers = isVisible;
                        };
                    });
            });
        }
    }
}
