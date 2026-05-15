using System.Windows;
using EasySave.Core.Services;
using EasySave.GUI.Services;
using EasySave.Core.ViewModels;
using EasySave.GUI.ViewModels;

namespace EasySave.GUI
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Wire up services (v2.0: unlimited jobs)
            var configService    = new ConfigService();
            var stateService     = new StateService();
            var settingsService  = new SettingsService();
            var businessSoftware = new BusinessSoftwareService();

            var backupViewModel = new BackupViewModel(
                configService,
                stateService,
                settingsService,
                businessSoftware,
                maxJobs: 0); // 0 = unlimited

            AppThemeService.ApplyPalette(backupViewModel.Settings.UiThemePalette);

            var mainViewModel = new MainViewModel(backupViewModel);

            var window = new MainWindow { DataContext = mainViewModel };
            window.Show();
        }
    }
}
