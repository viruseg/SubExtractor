using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using SubExtractor.Core.Ffmpeg;

namespace SubExtractor.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private string _versionText = string.Empty;
    private string _statusText = "Checking for updates...";
    private string _updateButtonText = "Update";
    private double _updateProgressValue;
    private bool _updateProgressIndeterminate;
    private Visibility _retryVisibility = Visibility.Collapsed;
    private Visibility _updateVisibility = Visibility.Collapsed;
    private Visibility _updateProgressVisibility = Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string VersionText
    {
        get => _versionText;
        private set => Set(ref _versionText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public string UpdateButtonText
    {
        get => _updateButtonText;
        private set => Set(ref _updateButtonText, value);
    }

    public double UpdateProgressValue
    {
        get => _updateProgressValue;
        private set => Set(ref _updateProgressValue, value);
    }

    public bool UpdateProgressIndeterminate
    {
        get => _updateProgressIndeterminate;
        private set => Set(ref _updateProgressIndeterminate, value);
    }

    public Visibility RetryVisibility
    {
        get => _retryVisibility;
        private set => Set(ref _retryVisibility, value);
    }

    public Visibility UpdateVisibility
    {
        get => _updateVisibility;
        private set => Set(ref _updateVisibility, value);
    }

    public Visibility UpdateProgressVisibility
    {
        get => _updateProgressVisibility;
        private set => Set(ref _updateProgressVisibility, value);
    }

    public void BeginCheck()
    {
        StatusText = "Checking for updates...";
        RetryVisibility = Visibility.Collapsed;
        UpdateVisibility = Visibility.Collapsed;
        ResetUpdateButton();
    }

    public void SetVersion(string display) =>
        VersionText = display.Length == 0 ? "Not installed" : display;

    public void ShowUpdateAvailable()
    {
        StatusText = string.Empty;
        RetryVisibility = Visibility.Collapsed;
        UpdateVisibility = Visibility.Visible;
        ResetUpdateButton();
    }

    public void ShowUpToDate()
    {
        StatusText = "Up to date";
        RetryVisibility = Visibility.Collapsed;
        UpdateVisibility = Visibility.Collapsed;
        ResetUpdateButton();
    }

    public void ShowCannotCompare()
    {
        StatusText = "Can't compare ffmpeg versions";
        RetryVisibility = Visibility.Collapsed;
        UpdateVisibility = Visibility.Collapsed;
        ResetUpdateButton();
    }

    public void ShowCheckFailed()
    {
        StatusText = "Couldn't check for updates";
        RetryVisibility = Visibility.Visible;
        UpdateVisibility = Visibility.Collapsed;
        ResetUpdateButton();
    }

    public void BeginUpdate()
    {
        StatusText = string.Empty;
        RetryVisibility = Visibility.Collapsed;
        UpdateVisibility = Visibility.Visible;
        UpdateButtonText = "Downloading...";
        UpdateProgressValue = 0;
        UpdateProgressIndeterminate = true;
        UpdateProgressVisibility = Visibility.Visible;
    }

    public void ApplyUpdateProgress(FfmpegInstallProgress progress)
    {
        switch (progress.Stage)
        {
            case "Downloading":
                if (progress.Percent is { } percent)
                {
                    var value = Math.Clamp(percent, 0, 100);
                    UpdateProgressValue = value;
                    UpdateProgressIndeterminate = false;
                    UpdateButtonText = $"{value:0}%";
                }
                else
                {
                    UpdateProgressIndeterminate = true;
                    UpdateButtonText = "Downloading...";
                }

                UpdateProgressVisibility = Visibility.Visible;
                break;

            case "Verifying":
                UpdateButtonText = "Verifying...";
                UpdateProgressVisibility = Visibility.Visible;
                UpdateProgressIndeterminate = true;
                break;

            case "Extracting":
                UpdateButtonText = "Extracting...";
                UpdateProgressVisibility = Visibility.Visible;
                UpdateProgressIndeterminate = true;
                break;

            case "Installing":
                UpdateButtonText = "Installing...";
                UpdateProgressVisibility = Visibility.Visible;
                UpdateProgressIndeterminate = true;
                break;

            case "Done":
                UpdateProgressValue = 100;
                UpdateProgressIndeterminate = false;
                UpdateProgressVisibility = Visibility.Visible;
                UpdateButtonText = "100%";
                break;
        }
    }

    public void ShowUpdateSucceeded(string display)
    {
        SetVersion(display);
        StatusText = "Up to date";
        RetryVisibility = Visibility.Collapsed;
        UpdateVisibility = Visibility.Collapsed;
        ResetUpdateButton();
    }

    public void ShowUpdateFailed(string message)
    {
        StatusText = $"Update failed: {message}";
        UpdateVisibility = Visibility.Visible;
        ResetUpdateButton();
    }

    private void ResetUpdateButton()
    {
        UpdateButtonText = "Update";
        UpdateProgressValue = 0;
        UpdateProgressIndeterminate = false;
        UpdateProgressVisibility = Visibility.Collapsed;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
