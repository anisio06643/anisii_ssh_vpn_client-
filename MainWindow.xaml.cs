using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using Renci.SshNet;
using System.Drawing;

namespace SSH_VPN_Client
{
    public partial class MainWindow : Window
    {
        private SshClient? _sshClient;
        private ForwardedPortDynamic? _socksProxy;
        private DateTime _connectionStartTime;

        public MainWindow()
        {
            InitializeComponent();
            LoadLastConfigOnStartup();
           // Log("This Tool Made For Your Privacy , Because Personally I think Goverment Is A Bitch , And One of My Rules is >> Never Trust A BITCH  So yeah enjoy @Anisii 2025");
        }

        // --- Custom Title Bar Handlers --
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        // -------------------------------

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_sshClient != null && _sshClient.IsConnected)
                {
                    Log("Already connected.");
                    return;
                }

                _sshClient = new SshClient(
                    hostBox.Text,
                    int.TryParse(portBox.Text, out int port) ? port : 22,
                    userBox.Text,
                    passBox.Password
                );

                Log("Connecting to SSH...");
                _sshClient.Connect();

                if (_sshClient.IsConnected)
                {
                    Log("SSH connection successful.");

                    _socksProxy = new ForwardedPortDynamic("127.0.0.1", 1080);
                    _sshClient.AddForwardedPort(_socksProxy);
                    _socksProxy.Start();

                    if (_socksProxy.IsStarted)
                    {
                        Log("✅ SOCKS5 proxy started at 127.0.0.1:1080");
                        SystemProxyHelper.SetSystemProxy("127.0.0.1:1080");
                        Log("🛠️ System proxy enabled.");
                    }
                    else
                    {
                        Log("⚠️ Failed to start SOCKS proxy.");
                    }

                    _connectionStartTime = DateTime.Now;
                    statusLabel.Content = "Connected Succesfully !";
                    
                    Log(DateTime.Now.ToString());
                }
                else
                {
                    Log("SSH connection failed.");
                    statusLabel.Content = "🔴 Disconnected";
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Connection error: {ex.Message}");
                statusLabel.Content = "🔴 Disconnected";
                
            }
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_socksProxy != null && _socksProxy.IsStarted)
                {
                    _socksProxy.Stop();
                    Log(" SOCKS proxy stopped.");
                }

                if (_sshClient != null && _sshClient.IsConnected)
                {
                    _sshClient.Disconnect();
                    Log("SSH disconnected.");
                }

                SystemProxyHelper.DisableSystemProxy();
                Log("🧹 System proxy disabled.");
                statusLabel.Content = "🔴 Disconnected";
            }
            catch (Exception ex)
            {
                Log($"Disconnection error: {ex.Message}");
            }
        }

        private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var config = new SshConfig
            {
                Host = hostBox.Text,
                Port = int.TryParse(portBox.Text, out var port) ? port : 22,
                Username = userBox.Text,
                Password = passBox.Password
            };

            var dialog = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                FileName = "config.json"
            };

            if (dialog.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
                Log($"Config saved to: {dialog.FileName}");
                RegistryHelper.SaveLastConfigPath(dialog.FileName);
            }
        }

        private void LoadConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(dialog.FileName);
                    var config = JsonSerializer.Deserialize<SshConfig>(json);

                    if (config != null)
                    {
                        hostBox.Text = config.Host ?? "";
                        portBox.Text = config.Port.ToString();
                        userBox.Text = config.Username ?? "";
                        passBox.Password = config.Password ?? "";
                        Log("Config loaded successfully.");
                        RegistryHelper.SaveLastConfigPath(dialog.FileName);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Failed to load config: {ex.Message}");
                }
            }
        }

        private void LoadLastConfigOnStartup()
        {
            var lastPath = RegistryHelper.LoadLastConfigPath();
            if (!string.IsNullOrEmpty(lastPath) && File.Exists(lastPath))
            {
                try
                {
                    var json = File.ReadAllText(lastPath);
                    var config = JsonSerializer.Deserialize<SshConfig>(json);

                    if (config != null)
                    {
                        hostBox.Text = config.Host ?? "";
                        portBox.Text = config.Port.ToString();
                        userBox.Text = config.Username ?? "";
                        passBox.Password = config.Password ?? "";
                        Log($"Loaded last config from registry: {lastPath}");
                    }
                }
                catch (Exception ex)
                {
                    Log($"Failed to load last config on startup: {ex.Message}");
                }
            }
        }

        private void Log(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            Dispatcher.Invoke(() =>
            {
                logBox.Items.Add($"[{time}] {message}");
                if (logBox.Items.Count > 0)
                    logBox.ScrollIntoView(logBox.Items[logBox.Items.Count - 1]);
            });
        }
    }

    public class SshConfig
    {
        public string? Host { get; set; }
        public int Port { get; set; } = 22;
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public static class RegistryHelper
    {
        private const string KeyPath = @"Software\SSH_VPN_Client_By_KingAnisii";

        public static void SaveLastConfigPath(string path)
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
            key?.SetValue("LastConfigPath", path, RegistryValueKind.String);
        }

        public static string? LoadLastConfigPath()
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue("LastConfigPath") as string;
        }
    }

    public static class SystemProxyHelper
    {
        [DllImport("wininet.dll", SetLastError = true)]
        public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        private const int INTERNET_OPTION_REFRESH = 37;

        public static void SetSystemProxy(string proxyAddress)
        {
            using (var registry = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", writable: true))
            {
                if (registry != null)
                {
                    registry.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
                    registry.SetValue("ProxyServer", $"socks={proxyAddress}", RegistryValueKind.String);
                }
            }

            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }

        public static void DisableSystemProxy()
        {
            using (var registry = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", writable: true))
            {
                if (registry != null)
                {
                    registry.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
                    registry.DeleteValue("ProxyServer", false);
                    
                }
            }

            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }
    }
}
