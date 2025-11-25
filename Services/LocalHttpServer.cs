using System.Net;
using System.Text;

namespace DailyMealPlannerExtended.Services;

/// <summary>
/// Simple HTTP server for handling OAuth callbacks on localhost
/// </summary>
public class LocalHttpServer : IDisposable
{
    private HttpListener? _listener;
    private readonly int _port;
    private TaskCompletionSource<string>? _callbackReceived;

    public LocalHttpServer(int port = 5555)
    {
        _port = port;
    }

    public string GetCallbackUrl() => $"http://localhost:{_port}/callback";

    public async Task<string> StartAndWaitForCallbackAsync(CancellationToken cancellationToken = default)
    {
        _callbackReceived = new TaskCompletionSource<string>();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_port}/");

        try
        {
            _listener.Start();
            Logger.Instance.Information("Local HTTP server started on port {Port}. Waiting for OAuth callback...", _port);
            Logger.Instance.Information("Server is listening at: http://localhost:{Port}/callback", _port);

            // Handle requests in background
            _ = Task.Run(async () => await HandleRequestsAsync(cancellationToken), cancellationToken);

            // Wait for callback with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(5)); // 5 minute timeout

            var callback = await _callbackReceived.Task.WaitAsync(timeoutCts.Token);
            return callback;
        }
        catch (OperationCanceledException)
        {
            Logger.Instance.Information("OAuth callback timeout or cancelled");
            throw new TimeoutException("OAuth authentication timed out");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to start local HTTP server");
            throw;
        }
    }

    private async Task HandleRequestsAsync(CancellationToken cancellationToken)
    {
        while (_listener?.IsListening == true && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => ProcessRequestAsync(context), cancellationToken);
            }
            catch (HttpListenerException)
            {
                // Listener was stopped
                break;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(ex, "Error handling HTTP request");
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;

            Logger.Instance.Information("Received request: {Url}", request.Url);
            Logger.Instance.Information("Request path: {Path}", request.Url?.AbsolutePath);

            var path = request.Url?.AbsolutePath?.TrimEnd('/');

            if (path == "/callback")
            {
                Logger.Instance.Information("Received callback request at /callback");

                // Send success response to browser with JavaScript to extract hash tokens
                var successHtml = @"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <title>Authentication Successful</title>
                        <style>
                            body {
                                font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
                                display: flex;
                                justify-content: center;
                                align-items: center;
                                height: 100vh;
                                margin: 0;
                                background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                            }
                            .container {
                                background: white;
                                padding: 40px;
                                border-radius: 12px;
                                box-shadow: 0 10px 25px rgba(0,0,0,0.2);
                                text-align: center;
                            }
                            h1 { color: #667eea; margin: 0 0 20px 0; }
                            p { color: #666; margin: 0; }
                            .status { margin-top: 20px; font-size: 14px; color: #999; }
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <h1>✓ Authentication Successful!</h1>
                            <p>Processing your login...</p>
                            <p class='status' id='status'>Extracting tokens...</p>
                        </div>
                        <script>
                            console.log('Callback page loaded');
                            console.log('Full URL:', window.location.href);
                            console.log('Hash:', window.location.hash);

                            document.getElementById('status').textContent = 'Checking for tokens...';

                            // Supabase returns tokens in the URL hash fragment
                            if (window.location.hash) {
                                var hashParams = new URLSearchParams(window.location.hash.substring(1));
                                var accessToken = hashParams.get('access_token');
                                var refreshToken = hashParams.get('refresh_token');

                                console.log('Access token found:', !!accessToken);
                                console.log('Refresh token found:', !!refreshToken);

                                if (accessToken) {
                                    document.getElementById('status').textContent = 'Sending tokens to app...';

                                    fetch('/callback/token?access_token=' + encodeURIComponent(accessToken) +
                                          '&refresh_token=' + encodeURIComponent(refreshToken || ''))
                                    .then(response => {
                                        console.log('Token sent to app:', response.status);
                                        document.getElementById('status').textContent = 'Success! You can close this window.';
                                        setTimeout(() => window.close(), 2000);
                                    })
                                    .catch(err => {
                                        console.error('Error sending tokens:', err);
                                        document.getElementById('status').textContent = 'Error: ' + err.message;
                                    });
                                } else {
                                    console.error('No access token found in hash');
                                    document.getElementById('status').textContent = 'Error: No access token found';
                                }
                            } else {
                                console.error('No hash fragment in URL');
                                document.getElementById('status').textContent = 'Error: No authentication data received';
                            }
                        </script>
                    </body>
                    </html>";

                var buffer = Encoding.UTF8.GetBytes(successHtml);
                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer);
                response.OutputStream.Close();

                // Don't complete yet - wait for the token endpoint
            }
            else if (path == "/callback/token")
            {
                // Extract tokens from query parameters sent by JavaScript
                var accessToken = request.QueryString["access_token"];
                var refreshToken = request.QueryString["refresh_token"];

                if (!string.IsNullOrEmpty(accessToken))
                {
                    var tokenData = $"access_token={accessToken}&refresh_token={refreshToken}";
                    _callbackReceived?.TrySetResult(tokenData);

                    Logger.Instance.Information("OAuth tokens received successfully");
                }

                // Send minimal response
                var buffer = Encoding.UTF8.GetBytes("OK");
                response.ContentType = "text/plain";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer);
                response.OutputStream.Close();

                // Stop listener after receiving callback
                Stop();
            }
            else if (path == "/callback/token")
            {
                // Extract tokens from query parameters sent by JavaScript
                var accessToken = request.QueryString["access_token"];
                var refreshToken = request.QueryString["refresh_token"];

                if (!string.IsNullOrEmpty(accessToken))
                {
                    var tokenData = $"access_token={accessToken}&refresh_token={refreshToken}";
                    _callbackReceived?.TrySetResult(tokenData);

                    Logger.Instance.Information("OAuth tokens received successfully");
                }

                // Send minimal response
                var buffer = Encoding.UTF8.GetBytes("OK");
                response.ContentType = "text/plain";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer);
                response.OutputStream.Close();

                // Stop listener after receiving callback
                Stop();
            }
            else
            {
                response.StatusCode = 404;
                response.Close();
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Error processing HTTP request");
        }
    }

    public void Stop()
    {
        try
        {
            _listener?.Stop();
            _listener?.Close();
            Logger.Instance.Information("Local HTTP server stopped");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Error stopping local HTTP server");
        }
    }

    public void Dispose()
    {
        Stop();
        _listener = null;
    }
}
