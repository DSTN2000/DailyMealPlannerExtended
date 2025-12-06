using System.Net.NetworkInformation;

namespace DailyMealPlannerExtended.Services;

public enum ConnectionStatus
{
    Online,
    Offline,
    AuthenticatedOffline
}

public class NetworkConnectivityService
{
    private bool _isOnline;
    private Timer? _connectivityCheckTimer;

    public bool IsOnline => _isOnline;
    public ConnectionStatus Status { get; private set; }

    public event EventHandler<ConnectionStatus>? ConnectionStatusChanged;

    public NetworkConnectivityService()
    {
        _isOnline = CheckConnectivity();
        Status = _isOnline ? ConnectionStatus.Online : ConnectionStatus.Offline;

        // Start periodic connectivity check (every 10 seconds)
        _connectivityCheckTimer = new Timer(
            CheckConnectivityCallback,
            null,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10)
        );

        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        UpdateConnectivityStatus();
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        UpdateConnectivityStatus();
    }

    private void CheckConnectivityCallback(object? state)
    {
        UpdateConnectivityStatus();
    }

    private void UpdateConnectivityStatus()
    {
        var wasOnline = _isOnline;
        _isOnline = CheckConnectivity();

        if (wasOnline != _isOnline)
        {
            var newStatus = _isOnline ? ConnectionStatus.Online : ConnectionStatus.Offline;
            Status = newStatus;

            Logger.Instance.Information("Network connectivity changed: {Status}", newStatus);
            ConnectionStatusChanged?.Invoke(this, newStatus);
        }
    }

    private bool CheckConnectivity()
    {
        try
        {
            // Check if network is available
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return false;
            }

            // Check for active network interfaces with operational status up
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var iface in interfaces)
            {
                // Skip loopback and tunnel interfaces
                if (iface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    iface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                // Check if interface is up and has a gateway
                if (iface.OperationalStatus == OperationalStatus.Up)
                {
                    var ipProperties = iface.GetIPProperties();
                    if (ipProperties.GatewayAddresses.Count > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning(ex, "Failed to check network connectivity");
            return false;
        }
    }

    public void UpdateAuthenticatedStatus(bool isAuthenticated)
    {
        if (isAuthenticated && !_isOnline)
        {
            Status = ConnectionStatus.AuthenticatedOffline;
        }
        else if (!isAuthenticated)
        {
            Status = _isOnline ? ConnectionStatus.Online : ConnectionStatus.Offline;
        }
        else
        {
            Status = ConnectionStatus.Online;
        }

        ConnectionStatusChanged?.Invoke(this, Status);
    }

    public void Dispose()
    {
        _connectivityCheckTimer?.Dispose();
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
    }
}
