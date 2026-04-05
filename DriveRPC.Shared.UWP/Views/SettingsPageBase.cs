using DriveRPC.Shared.Services;
using DriveRPC.Shared.UWP.Controls;
using DriveRPC.Shared.UWP.Helpers;
using DriveRPC.Shared.UWP.Models;
using DriveRPC.Shared.UWP.Services;
using DriveRPC.Shared.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace DriveRPC.Shared.UWP.Views
{
    public abstract class SettingsPageBase : Page
    {
        protected readonly SettingsViewModel _vm;
        protected readonly ISecureStorage _secureStorage;
        protected readonly SettingsImportExportService _importExport;
        protected readonly QrCodeScannerService _qrCodeScanner;

        protected bool _loading = true;
        protected bool _suppressAppearanceChange;

        protected SettingsPageBase(
            ISecureStorage secureStorage,
            IAppDataResetService appDataResetService,
            IAppearancePresetStore presetStore)
        {
            _secureStorage = secureStorage;
            _vm = new SettingsViewModel(appDataResetService, presetStore);
            _importExport = new SettingsImportExportService();
            _qrCodeScanner = new QrCodeScannerService();
        }

        protected async void AppearanceRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressAppearanceChange || _loading) return;

            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                var selectedMode = SettingsPageBase.TagToMode(tag);

                SetAppearance(selectedMode);
            }
        }

        protected void SetAppearance(AppearanceMode mode)
        {
            AppearanceService.Set(mode);
            ApplyAppearanceWithoutRestart();
        }

        protected void ApplyAppearanceWithoutRestart()
        {
            var window = Window.Current;
            window.Content = null;
            var appResources = Application.Current.Resources;
            appResources.MergedDictionaries.Clear();

            switch (AppearanceService.Current)
            {
                case AppearanceMode.Win11:
                    appResources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/Win11.xaml") });
                    break;
                case AppearanceMode.Win10_1709:
                    appResources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/Win10_1709.xaml") });
                    break;
                default:
                    appResources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/Win10_1507.xaml") });
                    break;
            }

            var frame = new Frame();
            window.Content = frame;
            frame.Navigate(NavigationHelper.GetPageType("Shell"), null);
            window.Activate();
        }

        public static AppearanceMode TagToMode(string tag)
        {
            if (tag == "1709") return AppearanceMode.Win10_1709;
            if (tag == "11") return AppearanceMode.Win11;
            return AppearanceMode.Win10_1507;
        }

        public static string ModeToTag(AppearanceMode mode)
        {
            if (mode == AppearanceMode.Win10_1709) return "1709";
            if (mode == AppearanceMode.Win11) return "11";
            return "1507";
        }

        protected async void BtnResetAllSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = CreateDialog();
            dialog.Title = "Reset All Settings";
            dialog.Content = "This will delete all DriveRPC configuration and cached data. Continue?";
            dialog.PrimaryButtonText = "Yes";
            dialog.SecondaryButtonText = "No";

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            try
            {
                await _vm.ResetAllAsync();
#if UWP1507
                await ShowSimpleDialogAsync("Restart Required", "The app will now close. Please restart it to apply the reset.");
                Application.Current.Exit();
#else
                await ShowSimpleDialogAsync("Restarting", "The app will now restart to apply the reset.");
                await CoreApplication.RequestRestartAsync("");
#endif
            }
            catch (Exception ex) { await ShowSimpleDialogAsync("Error", ex.Message); }
        }

        protected async void BtnImportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = CreateDialog();
            dialog.Title = "Import Settings & Vehicles";
            dialog.Content = "Choose how you want to import your exported DriveRPC data.";
            dialog.PrimaryButtonText = "Paste Text";
            dialog.SecondaryButtonText = "Scan QR Code";

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await PromptForPastedImportTextAsync();
            }
            else if (result == ContentDialogResult.Secondary)
            {
                await ImportFromScannerAsync();
            }
        }

        protected async void BtnExportSettings_Click(object sender, RoutedEventArgs e)
        {
            var presets = await _vm.LoadPresetsAsync();
            if (presets == null || presets.Count == 0)
            {
                await ShowSimpleDialogAsync("Nothing to Export", "You do not have any saved vehicles to export yet.");
                return;
            }

            var exportedText = _importExport.ExportToText(ModeToTag(AppearanceService.Current), presets);
            if (string.IsNullOrWhiteSpace(exportedText))
            {
                await ShowSimpleDialogAsync("Export Failed", "DriveRPC could not build the export payload.");
                return;
            }

            var qrCodeBitmap = await BarcodeUIService.GenerateQrCodeBitmapAsync(exportedText);
            if (qrCodeBitmap == null)
            {
                await ShowSimpleDialogAsync("Export Failed", "DriveRPC could not generate the QR code.");
                return;
            }

            await ShowExportDialogAsync(qrCodeBitmap, exportedText);
        }

        protected virtual ContentDialog CreateDialog() => new ContentDialog();

        protected async Task ShowSimpleDialogAsync(string title, string content)
        {
            var dialog = CreateDialog();
            dialog.Title = title;
            dialog.Content = content;
            dialog.PrimaryButtonText = "OK";
            await dialog.ShowAsync();
        }

        private async Task PromptForPastedImportTextAsync()
        {
            var textBox = new TextBox
            {
                AcceptsReturn = true,
                Height = 180,
                PlaceholderText = "Paste the exported DriveRPC text here.",
                TextWrapping = TextWrapping.Wrap
            };
            textBox.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);

            var dialog = CreateDialog();
            dialog.Title = "Paste Import Data";
            dialog.Content = textBox;
            dialog.PrimaryButtonText = "Import";
            dialog.SecondaryButtonText = "Cancel";

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            await ImportFromTextAsync(textBox.Text);
        }

        private async Task ImportFromScannerAsync()
        {
            try
            {
                var scannedText = await _qrCodeScanner.ScanAsync();
                if (string.IsNullOrWhiteSpace(scannedText))
                {
                    await ShowSimpleDialogAsync("Scan Failed", "No QR code data was detected.");
                    return;
                }

                await ImportFromTextAsync(scannedText);
            }
            catch (Exception ex)
            {
                await ShowSimpleDialogAsync("Scan Failed", ex.Message);
            }
        }

        private async Task ImportFromTextAsync(string importedText)
        {
            var payload = _importExport.ImportFromText(importedText);
            if (payload == null || payload.Vehicles == null || payload.Vehicles.Count == 0)
            {
                await ShowSimpleDialogAsync("Import Failed", "The imported data was empty or not recognised.");
                return;
            }

            var confirm = CreateDialog();
            confirm.Title = "Replace Existing Data";
            confirm.Content = $"Import {payload.Vehicles.Count} vehicle{(payload.Vehicles.Count == 1 ? "" : "s")} and replace your current settings? Your Discord token will not be changed.";
            confirm.PrimaryButtonText = "Import";
            confirm.SecondaryButtonText = "Cancel";

            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
                return;

            await _vm.ReplacePresetsAsync(payload.Vehicles);

            var importedAppearance = string.IsNullOrWhiteSpace(payload.AppearanceTag)
                ? AppearanceService.Current
                : TagToMode(payload.AppearanceTag);
            bool appearanceChanged = importedAppearance != AppearanceService.Current;

            await ShowSimpleDialogAsync(
                "Import Complete",
                appearanceChanged
                    ? "Your vehicles were imported successfully. DriveRPC will now apply the imported appearance."
                    : "Your settings and vehicles were imported successfully.");

            if (appearanceChanged)
            {
                SetAppearance(importedAppearance);
            }
        }

        private async Task ShowExportDialogAsync(WriteableBitmap qrCodeBitmap, string exportedText)
        {
            var image = new Image
            {
                Height = 320,
                Width = 320,
                Source = qrCodeBitmap,
                Stretch = Stretch.Uniform
            };

            var description = new TextBlock
            {
                Margin = new Thickness(0, 12, 0, 0),
                Text = "Scan this QR code on another device to import your DriveRPC settings and vehicles.",
                TextWrapping = TextWrapping.Wrap
            };

            var dialog = CreateDialog();
            dialog.Title = "Export Settings & Vehicles";
            dialog.Content = new StackPanel
            {
                Children =
                {
                    image,
                    description
                }
            };
            dialog.PrimaryButtonText = "Copy Text";
            dialog.SecondaryButtonText = "Close";

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(exportedText);
                Clipboard.SetContent(dataPackage);
                Clipboard.Flush();

                await ShowSimpleDialogAsync("Copied", "The exported DriveRPC text has been copied to the clipboard.");
            }
        }

        protected TextBlock DisplayNameText => FindName("DisplayNameText") as TextBlock;
        protected TextBlock UsernameText => FindName("UsernameText") as TextBlock;
        protected ImageBrush UserAvatarBrush => FindName("UserAvatarBrush") as ImageBrush;
        protected Button BtnManageAccount => FindName("BtnManageAccount") as Button;
        protected DiscordAccountControl AccountControl => FindName("AccountControl") as DiscordAccountControl;

        protected async void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var scrollContent = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Inlines =
                    {
                        new Run { Text = "DriveRPC", FontWeight = FontWeights.Bold, FontSize = 18 },
                        new LineBreak(),
                        new Run { Text = $"Version {OSHelper.AppVersion} ({OSHelper.PlatformFamily}) {OSHelper.Architecture}" },
                        new LineBreak(),
                        new LineBreak(),

                        new Run { Text = "Copyright © 2026 MegaBytesMe" },
                        new LineBreak(),
                        new LineBreak(),

                        new Run { Text = "DriveRPC is an app which is designed to share your driving as a Discord activity." },
                        new LineBreak(),
                        new LineBreak(),

                        new Run { Text = "Source code available on " },
                        new Hyperlink
                        {
                            NavigateUri = new Uri("https://github.com/megabytesme/DriveRPC"),
                            Inlines = { new Run { Text = "GitHub" } }
                        },
                        new LineBreak(),

                        new Run { Text = "Found a bug? Report it here: " },
                        new Hyperlink
                        {
                            NavigateUri = new Uri("https://github.com/megabytesme/DriveRPC/issues"),
                            Inlines = { new Run { Text = "Issue Tracker" } }
                        },
                        new LineBreak(),
                        new LineBreak(),

                        new Run { Text = "Like what you see? Consider supporting me on " },
                        new Hyperlink
                        {
                            NavigateUri = new Uri("https://ko-fi.com/megabytesme"),
                            Inlines = { new Run { Text = "Ko-fi!" } }
                        },
                        new LineBreak(),
                        new LineBreak(),

                        new Hyperlink
                        {
                            NavigateUri = new Uri("https://github.com/megabytesme/DriveRPC/blob/master/LICENSE.md"),
                            Inlines = { new Run { Text = "License:" } }
                        },
                        new LineBreak(),
                        new Run { Text = "• App (Client): CC BY-NC-SA 4.0" }
                    },
                    TextWrapping = TextWrapping.Wrap
                }
            };

            var dialog = CreateDialog();
            dialog.Title = "About";
            dialog.Content = scrollContent;
            dialog.PrimaryButtonText = "OK";
            await dialog.ShowAsync();
        }

        protected async void DisclaimerButton_Click(object sender, RoutedEventArgs e)
        {
            var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };

            textBlock.Inlines.Add(new Run
            {
                Text = "This is an unofficial, third-party Discord RPC client. This project is "
            });
            textBlock.Inlines.Add(new Run
            {
                Text = "not affiliated with, endorsed, or sponsored by Discord Inc.",
                FontWeight = FontWeights.Bold
            });
            textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new Run { Text = "\"Discord\" is a trademark of Discord Inc." });
            textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new Run { Text = "By using this client, you take full responsibility of any ban risks." });
            textBlock.Inlines.Add(new Run
            {
                Text = "The author (MegaBytesMe) claims no responsibility for any issues that may arise from using this app."
            });

            var dialog = CreateDialog();
            dialog.Title = "Disclaimer";
            dialog.Content = new ScrollViewer { Content = textBlock };
            dialog.PrimaryButtonText = "I Understand";
            await dialog.ShowAsync();
        }
    }
}
