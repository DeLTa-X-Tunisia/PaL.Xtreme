using System.Windows;
using System.Windows.Input;

namespace PaLX.Client
{
    public partial class AlreadyConnectedWindow : Window
    {
        public bool ForceConnect { get; private set; } = false;
        
        public string Username { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string DeviceIP { get; set; } = "";
        public DateTime? ConnectedSince { get; set; }

        public AlreadyConnectedWindow(string username, string? deviceName, string? deviceIP, DateTime? connectedSince)
        {
            InitializeComponent();
            
            Username = username;
            DeviceName = deviceName ?? "Appareil inconnu";
            DeviceIP = deviceIP ?? "";
            ConnectedSince = connectedSince;
            
            // Mise à jour de l'interface
            UsernameRun.Text = username;
            DeviceInfoText.Text = string.IsNullOrEmpty(deviceIP) 
                ? DeviceName 
                : $"{DeviceName} ({deviceIP})";
            
            if (connectedSince.HasValue)
            {
                var duration = DateTime.Now - connectedSince.Value;
                if (duration.TotalMinutes < 1)
                    ConnectionTimeText.Text = "Connecté à l'instant";
                else if (duration.TotalHours < 1)
                    ConnectionTimeText.Text = $"Connecté depuis {(int)duration.TotalMinutes} min";
                else if (duration.TotalDays < 1)
                    ConnectionTimeText.Text = $"Connecté depuis {(int)duration.TotalHours}h {duration.Minutes}min";
                else
                    ConnectionTimeText.Text = $"Connecté depuis {(int)duration.TotalDays} jour(s)";
            }
            else
            {
                ConnectionTimeText.Text = "Session active";
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ForceConnect = false;
            DialogResult = false;
            Close();
        }

        private void ForceConnectButton_Click(object sender, RoutedEventArgs e)
        {
            ForceConnect = true;
            DialogResult = true;
            Close();
        }
    }
}
