using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using System.Diagnostics;

namespace DailyMealPlannerExtended.Services;

public class SupabaseAuthService
{
    private Supabase.Client? _client;
    private LocalHttpServer? _httpServer;
    private readonly NetworkConnectivityService _connectivityService;

    public bool IsAuthenticated => _client?.Auth.CurrentSession != null;
    public Session? CurrentSession => _client?.Auth.CurrentSession;
    public User? CurrentUser => _client?.Auth.CurrentUser;
    public ConnectionStatus ConnectionStatus => _connectivityService.Status;
    public bool IsOnline => _connectivityService.IsOnline;

    public event EventHandler<bool>? AuthStateChanged;
    public event EventHandler<ConnectionStatus>? ConnectionStatusChanged;

    public SupabaseAuthService(NetworkConnectivityService connectivityService)
    {
        _connectivityService = connectivityService;
        _connectivityService.ConnectionStatusChanged += OnConnectionStatusChanged;
    }

    private void OnConnectionStatusChanged(object? sender, ConnectionStatus status)
    {
        Logger.Instance.Information("Connection status changed: {Status}", status);
        ConnectionStatusChanged?.Invoke(this, status);
    }

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

            // Only initialize if online
            if (_connectivityService.IsOnline)
            {
                await _client.InitializeAsync();
                Logger.Instance.Information("Supabase client initialized successfully (online)");

                // Subscribe to auth state changes
                _client.Auth.AddStateChangedListener(OnAuthStateChanged);

                // Try to restore session from storage
                await TryRestoreSessionAsync();
            }
            else
            {
                Logger.Instance.Warning("Initializing Supabase client in offline mode");
                // Client is created but not initialized - will be initialized when connection is restored
            }

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

                    // Store user ID for account validation
                    if (CurrentUser?.Id != null)
                    {
                        await StoreUserIdAsync(CurrentUser.Id);
                    }

                    _connectivityService.UpdateAuthenticatedStatus(true);
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

    public async Task SignOutAsync(bool clearLocalData = true)
    {
        try
        {
            if (_client != null && _connectivityService.IsOnline)
            {
                await _client.Auth.SignOut();
            }

            await ClearSessionAsync();

            if (clearLocalData)
            {
                await ClearAllLocalDataAsync();
            }

            _connectivityService.UpdateAuthenticatedStatus(false);
            Logger.Instance.Information("Signed out successfully (clearLocalData: {ClearData})", clearLocalData);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to sign out");
            throw;
        }
    }

    public async Task ClearAllLocalDataAsync()
    {
        try
        {
            var dataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DailyMealPlanner",
                "DailyMealPlannerExtended"
            );

            if (!Directory.Exists(dataFolder))
            {
                Logger.Instance.Information("Data folder does not exist, nothing to clear");
                return;
            }

            // Clear user preferences
            var preferencesFile = Path.Combine(dataFolder, "user_preferences.json");
            if (File.Exists(preferencesFile))
            {
                File.Delete(preferencesFile);
                Logger.Instance.Information("Cleared user preferences");
            }

            // Clear SQLite databases - need to clear connection pools first
            var snapshotsDb = Path.Combine(dataFolder, "snapshots.db");
            var favoritesDb = Path.Combine(dataFolder, "favorites.db");

            // Clear SQLite connection pools to release file locks
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            // Small delay to ensure connections are fully released
            await Task.Delay(100);

            // Clear snapshots database
            if (File.Exists(snapshotsDb))
            {
                try
                {
                    File.Delete(snapshotsDb);
                    Logger.Instance.Information("Cleared snapshots database");
                }
                catch (IOException ex)
                {
                    Logger.Instance.Warning(ex, "Could not delete snapshots.db (file in use), will retry");
                    // Force garbage collection to release any remaining connections
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    await Task.Delay(200);
                    File.Delete(snapshotsDb);
                    Logger.Instance.Information("Cleared snapshots database on retry");
                }
            }

            // Clear favorites database
            if (File.Exists(favoritesDb))
            {
                try
                {
                    File.Delete(favoritesDb);
                    Logger.Instance.Information("Cleared favorites database");
                }
                catch (IOException ex)
                {
                    Logger.Instance.Warning(ex, "Could not delete favorites.db (file in use), will retry");
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    await Task.Delay(200);
                    File.Delete(favoritesDb);
                    Logger.Instance.Information("Cleared favorites database on retry");
                }
            }

            // Clear stored user ID
            var userIdFile = Path.Combine(dataFolder, "current_user_id.txt");
            if (File.Exists(userIdFile))
            {
                File.Delete(userIdFile);
                Logger.Instance.Information("Cleared stored user ID");
            }

            Logger.Instance.Information("All local data cleared successfully");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to clear local data");
            throw;
        }
    }

    public async Task<string?> GetStoredUserIdAsync()
    {
        try
        {
            var dataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DailyMealPlanner",
                "DailyMealPlannerExtended"
            );

            var userIdFile = Path.Combine(dataFolder, "current_user_id.txt");

            if (File.Exists(userIdFile))
            {
                return await File.ReadAllTextAsync(userIdFile);
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to get stored user ID");
            return null;
        }
    }

    public async Task StoreUserIdAsync(string userId)
    {
        try
        {
            var dataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DailyMealPlanner",
                "DailyMealPlannerExtended"
            );

            if (!Directory.Exists(dataFolder))
            {
                Directory.CreateDirectory(dataFolder);
            }

            var userIdFile = Path.Combine(dataFolder, "current_user_id.txt");
            await File.WriteAllTextAsync(userIdFile, userId);
            Logger.Instance.Information("Stored user ID: {UserId}", userId);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to store user ID");
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
                        // Validate account matches local data
                        await ValidateAccountMatchAsync();

                        _connectivityService.UpdateAuthenticatedStatus(true);
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

    public async Task ValidateAccountMatchAsync()
    {
        try
        {
            if (!IsAuthenticated || CurrentUser?.Id == null)
            {
                return;
            }

            var storedUserId = await GetStoredUserIdAsync();

            if (storedUserId != null && storedUserId != CurrentUser.Id)
            {
                Logger.Instance.Warning(
                    "Account mismatch detected! Stored: {StoredId}, Current: {CurrentId}. Clearing local data.",
                    storedUserId, CurrentUser.Id
                );

                // Account switched without proper logout - clear everything
                await ClearAllLocalDataAsync();
                await StoreUserIdAsync(CurrentUser.Id);

                Logger.Instance.Information("Local data cleared due to account mismatch");
            }
            else if (storedUserId == null)
            {
                // First login or user ID not stored yet
                await StoreUserIdAsync(CurrentUser.Id);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to validate account match");
        }
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
