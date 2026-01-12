using System.Windows;
using System.Windows.Input;

namespace PaLX.Client
{
    public partial class WhisperWindow : Window
    {
        public int RecipientUserId { get; private set; }
        public string RecipientName { get; private set; }
        public string? WhisperMessage { get; private set; }
        public bool IsSent { get; private set; } = false;

        public WhisperWindow(int recipientUserId, string recipientName)
        {
            InitializeComponent();
            RecipientUserId = recipientUserId;
            RecipientName = recipientName;
            RecipientNameText.Text = recipientName;
            
            WhisperTextBox.TextChanged += (s, e) =>
            {
                CharCountText.Text = $"{WhisperTextBox.Text.Length}/500";
            };
            
            Loaded += (s, e) => WhisperTextBox.Focus();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            IsSent = false;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsSent = false;
            Close();
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            SendWhisper();
        }

        private void WhisperTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SendWhisper();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                IsSent = false;
                Close();
            }
        }

        private void SendWhisper()
        {
            var message = WhisperTextBox.Text.Trim();
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            WhisperMessage = message;
            IsSent = true;
            Close();
        }
    }
}
