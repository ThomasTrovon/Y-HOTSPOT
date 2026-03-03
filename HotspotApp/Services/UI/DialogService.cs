using System.Runtime.InteropServices.WindowsRuntime;
using HotspotApp.Constants;
using HotspotApp.Helpers;
using HotspotApp.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace HotspotApp.Services.UI;

public sealed class DialogService
{
    private XamlRoot? _xamlRoot;

    public void SetXamlRoot(XamlRoot xamlRoot)
    {
        _xamlRoot = xamlRoot;
    }

    public async Task ShowErrorAsync(string message)
    {
        if (_xamlRoot == null)
        {
            return;
        }

        ContentDialog dialog = new()
        {
            XamlRoot = _xamlRoot,
            Title = AppStrings.Get("Text_ErrorDialogTitle"),
            Content = message,
            CloseButtonText = AppStrings.Get("Text_ErrorDialogCloseButtonText"),
        };

        await dialog.ShowAsync();
    }

    public async Task<bool> ShowResetConfirmationAsync()
    {
        if (_xamlRoot == null)
        {
            return false;
        }

        ContentDialog dialog = new()
        {
            XamlRoot = _xamlRoot,
            Title = AppStrings.Get("Text_ResetDialogTitle"),
            Content = AppStrings.Get("Text_ResetDialogContent"),
            PrimaryButtonText = AppStrings.Get("Text_ResetDialogPrimaryButtonText"),
            CloseButtonText = AppStrings.Get("Text_ResetDialogCancelButtonText"),
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public async Task ShowHotspotQrAsync(string ssid, string passphrase)
    {
        if (_xamlRoot == null)
        {
            return;
        }

        var bytes = QrCodeHelper.GetWifiQrBytes(ssid, passphrase);

        var imageSource = new BitmapImage();
        using (var stream = new InMemoryRandomAccessStream())
        {
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);
            await imageSource.SetSourceAsync(stream);
        }

        var image = new Image
        {
            Width = 240,
            Height = 240,
            Stretch = Stretch.Uniform,
            Source = imageSource,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var copyButton = new Button
        {
            Content = AppStrings.Get("Text_QrDialogCopyImageButton"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = 240,
            CornerRadius = new CornerRadius(0),
            Margin = new Thickness(0, -12, 0, 0),
        };

        var ssidText = new TextBlock
        {
            Text = $"{AppStrings.Get("Text_SsidLabel")}: {ssid}",
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };

        var passText = new TextBlock
        {
            Text = $"{AppStrings.Get("Text_PasswordLabel")}: {passphrase}",
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            Margin = new Thickness(0, -12, 0, 0),
        };

        copyButton.Click += async (s, e) =>
        {
            try
            {
                var dataPackage = new DataPackage
                {
                    RequestedOperation = DataPackageOperation.Copy,
                };

                var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(bytes.AsBuffer());
                stream.Seek(0);

                dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
                Clipboard.SetContent(dataPackage);

                var originalContent = copyButton.Content;
                copyButton.Content = AppStrings.Get("Text_QrDialogCopiedButton");
                await Task.Delay(2000);
                copyButton.Content = originalContent;
            }
            catch { }
        };

        var content = new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        content.Children.Add(image);
        content.Children.Add(copyButton);
        content.Children.Add(ssidText);
        content.Children.Add(passText);

        ContentDialog dialog = new()
        {
            XamlRoot = _xamlRoot,
            Content = content,
            CloseButtonText = AppStrings.Get("Text_QrDialogCloseButtonText"),
            DefaultButton = ContentDialogButton.Close,
        };

        await dialog.ShowAsync();
    }

    public async Task ShowAboutAsync()
    {
        if (_xamlRoot == null)
        {
            return;
        }

        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(
            new TextBlock
            {
                Text = AppStrings.Format("Text_AboutVersionFormat", AppConstants.Version),
            }
        );
        stack.Children.Add(
            new TextBlock
            {
                Text = AppStrings.Format("Text_AboutAuthorFormat", AppConstants.Author),
            }
        );

        var linksStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0),
        };

        var githubBtn = new Button { Content = AppStrings.Get("Text_AboutGithubButton") };
        githubBtn.Click += async (s, e) =>
            await Windows.System.Launcher.LaunchUriAsync(new Uri(AppConstants.GithubUrl));

        var releasesBtn = new Button { Content = AppStrings.Get("Text_AboutReleasesButton") };
        releasesBtn.Click += async (s, e) =>
            await Windows.System.Launcher.LaunchUriAsync(new Uri(AppConstants.ReleasesUrl));

        linksStack.Children.Add(githubBtn);
        linksStack.Children.Add(releasesBtn);
        stack.Children.Add(linksStack);

        ContentDialog dialog = new()
        {
            XamlRoot = _xamlRoot,
            Title = AppConstants.AppName,
            Content = stack,
            CloseButtonText = AppStrings.Get("Text_QrDialogCloseButtonText"),
            DefaultButton = ContentDialogButton.Close,
        };

        await dialog.ShowAsync();
    }

    public async Task<bool> ShowConfigLoadErrorAsync()
    {
        if (_xamlRoot == null)
        {
            return false;
        }

        ContentDialog dialog = new()
        {
            XamlRoot = _xamlRoot,
            Title = AppStrings.Get("Text_ConfigLoadErrorTitle"),
            Content = AppStrings.Get("Text_ConfigLoadErrorMessage"),
            PrimaryButtonText = AppStrings.Get("Text_ConfigLoadErrorContinueButton"),
            SecondaryButtonText = AppStrings.Get("Text_ConfigLoadErrorExitButton"),
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
