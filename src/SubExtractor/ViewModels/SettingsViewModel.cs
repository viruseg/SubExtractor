using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using SubExtractor.Core.Ffmpeg;

namespace SubExtractor.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private string _versionText = string.Empty;
    private string _updateButtonText = "Update";
    private double _updateProgressValue;
    private bool _updateProgressIndeterminate;
    private Visibility _updateVisibility = Visibility.Collapsed;
    private Visibility _updateProgressVisibility = Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string VersionText
    {
        get => _versionText;
        private set => Set(ref _versionText, value);
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
        UpdateVisibility = Visibility.Collapsed;
        ResetUpdateButton();
    }

    public void SetVersion(string display) => VersionText = display;

    public void ShowVersionError() => VersionText = "Error";

    public void ShowUpdateAvailable()
    {
        UpdateVisibility = Visibility.Visible;
        ResetUpdateButton();
    }

    public void BeginUpdate()
    {
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
        UpdateVisibility = Visibility.Collapsed;
        ResetUpdateButton();
    }

    public void ShowUpdateFailed()
    {
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
