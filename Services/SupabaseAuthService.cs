using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using System.Diagnostics;

namespace DailyMealPlannerExtended.Services;

public class SupabaseAuthService
{
    private Supabase.Client? _client;
    private LocalHttpServer? _httpServer;

    public bool IsAuthenticated => _client?.Auth.CurrentSession != null;
    public Session? CurrentSession => _client?.Auth.CurrentSession;
    public User? CurrentUser => _client?.Auth.CurrentUser;

    public event EventHandler<bool>? AuthStateChanged;

    public async Task<Supabase.Client> InitializeAsync()
    {
        if (_client != null)
            return _client;

        if (!SupabaseConfig.IsConfigured())
        {
            throw new InvalidOperationException("Supabase is not configured. Check your .env file.");
        }

        try
        {
            var options = new Supabase.SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = false, // We don't need realtime for now
            };

            _client = new Supabase.Client(SupabaseConfig.Url, SupabaseConfig.PublishableKey, options);
            await _client.InitializeAsync();

            // Subscribe to auth state changes
            _client.Auth.AddStateChangedListener(OnAuthStateChanged);

            Logger.Instance.Information("Supabase client initialized successfully");

            // Try to restore session from storage
            await TryRestoreSessionAsync();

            return _client;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to initialize Supabase client");
            throw;
        }
    }

    private void OnAuthStateChanged(IGotrueClient<User, Session> sender, Constants.AuthState state)
    {
        Logger.Instance.Information("Auth state changed: {State}", state);
        AuthStateChanged?.Invoke(this, IsAuthenticated);
    }

    public async Task<bool> SignInWithGoogleAsync()
    {
        try
        {
            if (_client == null)
            {
                await InitializeAsync();
            }

            // Start local HTTP server
            _httpServer = new LocalHttpServer(port: 5555);
            var redirectUrl = _httpServer.GetCallbackUrl();

            Logger.Instance.Information("Starting Google OAuth flow with redirect: {RedirectUrl}", redirectUrl);

            // Generate OAuth URL
            var authState = await _client!.Auth.SignIn(Supabase.Gotrue.Constants.Provider.Google, new SignInOptions
            {
                RedirectTo = redirectUrl
            });

            Logger.Instance.Information("OAuth URL generated: {Url}", authState.Uri.ToString());

            // Open browser for user to authenticate
            OpenBrowser(authState.Uri.ToString());

            // Wait for callback
            var tokenData = await _httpServer.StartAndWaitForCallbackAsync();

            // Parse tokens
            var tokens = ParseTokenData(tokenData);
            if (tokens.ContainsKey("access_token"))
            {
                // Set the session manually
                var session = await _client.Auth.SetSession(
                    tokens["access_token"],
                    tokens.GetValueOrDefault("refresh_token", "")
                );

                if (session != null)
                {
                    await SaveSessionAsync(session);
                    Logger.Instance.Information("Successfully signed in with Google: {Email}", CurrentUser?.Email);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to sign in with Google");
            return false;
        }
        finally
        {
            _httpServer?.Dispose();
            _httpServer = null;
        }
    }

    private Dictionary<string, string> ParseTokenData(string tokenData)
    {
        var result = new Dictionary<string, string>();
        var pairs = tokenData.Split('&');

        foreach (var pair in pairs)
        {
            var keyValue = pair.Split('=');
            if (keyValue.Length == 2)
            {
                result[keyValue[0]] = keyValue[1];
            }
        }

        return result;
    }

    private void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            Logger.Instance.Information("Opened browser for OAuth");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to open browser");
            throw;
        }
    }

    public async Task SignOutAsync()
    {
        try
        {
            if (_client != null)
            {
                await _client.Auth.SignOut();
                await ClearSessionAsync();
                Logger.Instance.Information("Signed out successfully");
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to sign out");
        }
    }

    private async Task SaveSessionAsync(Session session)
    {
        try
        {
            var sessionFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DailyMealPlanner",
                "DailyMealPlannerExtended"
            );

            if (!Directory.Exists(sessionFolder))
            {
                Directory.CreateDirectory(sessionFolder);
            }

            var sessionFile = Path.Combine(sessionFolder, "session.json");
            var sessionJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                access_token = session.AccessToken,
                refresh_token = session.RefreshToken,
                expires_at = session.ExpiresAt()
            });

            await File.WriteAllTextAsync(sessionFile, sessionJson);
            Logger.Instance.Information("Session saved to disk");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to save session");
        }
    }

    private async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            var sessionFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DailyMealPlanner",
                "DailyMealPlannerExtended"
            );

            var sessionFile = Path.Combine(sessionFolder, "session.json");

            Logger.Instance.Information("Checking for saved session at: {Path}", sessionFile);

            if (!File.Exists(sessionFile))
            {
                Logger.Instance.Information("No saved session file found");
                return false;
            }

            Logger.Instance.Information("Found saved session file, attempting to restore...");

            var sessionJson = await File.ReadAllTextAsync(sessionFile);
            var sessionData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(sessionJson);

            if (sessionData != null &&
                sessionData.ContainsKey("access_token") &&
                sessionData.ContainsKey("refresh_token"))
            {
                var accessToken = sessionData["access_token"].ToString();
                var refreshToken = sessionData["refresh_token"].ToString();

                if (!string.IsNullOrEmpty(accessToken) && _client != null)
                {
                    Logger.Instance.Information("Attempting to set session with tokens...");
                    var session = await _client.Auth.SetSession(accessToken!, refreshToken!);

                    if (session != null)
                    {
                        Logger.Instance.Information("Session restored successfully: {Email}", CurrentUser?.Email);
                        return true;
                    }
                    else
                    {
                        Logger.Instance.Warning("SetSession returned null - tokens may be expired");
                    }
                }
                else
                {
                    Logger.Instance.Warning("Missing access token or client not initialized");
                }
            }
            else
            {
                Logger.Instance.Warning("Session data missing required keys");
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to restore session");
        }

        return false;
    }

    private async Task ClearSessionAsync()
    {
        try
        {
            var sessionFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DailyMealPlanner",
                "DailyMealPlannerExtended"
            );

            var sessionFile = Path.Combine(sessionFolder, "session.json");

            if (File.Exists(sessionFile))
            {
                File.Delete(sessionFile);
                Logger.Instance.Information("Session cleared from disk");
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to clear session");
        }

        await Task.CompletedTask;
    }

    public Supabase.Client GetClient()
    {
        if (_client == null)
        {
            throw new InvalidOperationException("Supabase client not initialized. Call InitializeAsync() first.");
        }

        return _client;
    }
}
