using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyMealPlannerExtended.Services;

namespace DailyMealPlannerExtended.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly SupabaseAuthService _authService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Sign in to sync your meal plans across devices";

    [ObservableProperty]
    private bool _hasError;

    public LoginViewModel()
    {
        _authService = new SupabaseAuthService();
    }

    [RelayCommand]
    private async Task SignInWithGoogleAsync()
    {
        try
        {
            IsLoading = true;
            HasError = false;
            StatusMessage = "Opening browser for authentication...";

            var success = await _authService.SignInWithGoogleAsync();

            if (success)
            {
                StatusMessage = "Successfully signed in!";
                Logger.Instance.Information("User signed in: {Email}", _authService.CurrentUser?.Email);

                // Notify parent that login succeeded
                OnLoginSuccessful();
            }
            else
            {
                HasError = true;
                StatusMessage = "Failed to sign in. Please try again.";
                Logger.Instance.Warning("Sign in failed");
            }
        }
        catch (TimeoutException)
        {
            HasError = true;
            StatusMessage = "Authentication timed out. Please try again.";
            Logger.Instance.Warning("Authentication timed out");
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = $"Error: {ex.Message}";
            Logger.Instance.Error(ex, "Failed to sign in with Google");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ContinueOffline()
    {
        Logger.Instance.Information("User chose to continue offline");
        OnContinueOffline();
    }

    public event EventHandler? LoginSuccessful;
    public event EventHandler? ContinueOfflineRequested;

    private void OnLoginSuccessful()
    {
        LoginSuccessful?.Invoke(this, EventArgs.Empty);
    }

    private void OnContinueOffline()
    {
        ContinueOfflineRequested?.Invoke(this, EventArgs.Empty);
    }

    public SupabaseAuthService GetAuthService() => _authService;
}
