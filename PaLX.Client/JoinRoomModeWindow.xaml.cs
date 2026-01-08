using System.Windows;

namespace PaLX.Client
{
    /// <summary>
    /// Fenêtre modale élégante pour choisir le mode d'entrée dans un salon (Admin système uniquement)
    /// Normal: visible par tous
    /// Invisible: seuls les admins de rang égal ou supérieur le verront
    /// </summary>
    public partial class JoinRoomModeWindow : Window
    {
        public bool? IsInvisibleMode { get; private set; } = null;
        public string RoomName { get; set; } = "";

        public JoinRoomModeWindow(string roomName)
        {
            InitializeComponent();
            RoomName = roomName;
            RoomNameText.Text = $"Rejoindre « {roomName} »";
        }

        private void NormalMode_Click(object sender, RoutedEventArgs e)
        {
            IsInvisibleMode = false;
            DialogResult = true;
            Close();
        }

        private void InvisibleMode_Click(object sender, RoutedEventArgs e)
        {
            IsInvisibleMode = true;
            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            IsInvisibleMode = null;
            DialogResult = false;
            Close();
        }
    }
}
