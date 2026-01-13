using System.Windows;

namespace PaLX.Client
{
    /// <summary>
    /// Fenêtre de notification élégante affichée lorsqu'un utilisateur est expulsé d'un salon
    /// Design moderne avec animation d'entrée et icône pulsante
    /// </summary>
    public partial class KickNotificationWindow : Window
    {
        public KickNotificationWindow(string roomName, string reason)
        {
            InitializeComponent();
            
            RoomNameText.Text = roomName;
            ReasonText.Text = string.IsNullOrWhiteSpace(reason) ? "Aucune raison spécifiée" : reason;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
