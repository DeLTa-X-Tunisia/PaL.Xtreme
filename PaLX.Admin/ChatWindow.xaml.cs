using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Text;
using Microsoft.Web.WebView2.Core;

namespace PaLX.Admin
{
    public partial class ChatWindow : Window
    {
        private string _currentUser;
        private string _partnerUser;
        private DatabaseService _dbService;
        private DispatcherTimer _pollTimer;
        private int _lastMessageId = 0;
        private DateTime _lastTypingSent = DateTime.MinValue;

        public ChatWindow(string currentUser, string partnerUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _partnerUser = partnerUser;
            _dbService = new DatabaseService();

            // Load partner full name
            string fullName = _dbService.GetUserFullName(partnerUser);
            PartnerName.Text = fullName;

            // Load partner details (avatar, status)
            var friend = _dbService.GetFriends(currentUser).FirstOrDefault(f => f.Username == partnerUser);
            if (friend != null)
            {
                if (!string.IsNullOrEmpty(friend.DisplayName) && friend.DisplayName != partnerUser)
                    PartnerName.Text = friend.DisplayName;
                
                if (!string.IsNullOrEmpty(fullName) && fullName != partnerUser)
                    PartnerName.Text = fullName;

                PartnerStatus.Text = friend.Status;
                
                // Set Status Color
                var statusColor = GetStatusColor(friend.Status);
                PartnerStatus.Foreground = statusColor;
                StatusIndicator.Fill = statusColor;

                // Set Avatar
                if (!string.IsNullOrEmpty(friend.AvatarPath) && System.IO.File.Exists(friend.AvatarPath))
                {
                    try
                    {
                        AvatarBrush.ImageSource = new BitmapImage(new Uri(friend.AvatarPath, UriKind.Absolute));
                    }
                    catch { /* Keep default */ }
                }
            }

            InitializeWebView();
            
            _pollTimer = new DispatcherTimer();
            _pollTimer.Interval = TimeSpan.FromSeconds(2);
            _pollTimer.Tick += PollTimer_Tick;
            _pollTimer.Start();
        }

        private SolidColorBrush GetStatusColor(string status)
        {
            return status?.ToLower() switch
            {
                "en ligne" => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                "occupé" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),   // Red
                "absent" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),   // Orange
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))         // Gray
            };
        }

        private void AttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder for file attachment logic
            MessageBox.Show("Fonctionnalité d'envoi de fichier à venir.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void InitializeWebView()
        {
            try
            {
                // Ensure the environment is ready
                await ChatWebView.EnsureCoreWebView2Async();
                
                string html = @"
<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: 'Segoe UI', sans-serif; background-color: #f5f5f5; margin: 0; padding: 10px; overflow-x: hidden; }
        .message { display: flex; margin-bottom: 10px; animation: fadeIn 0.3s ease; }
        .message.mine { justify-content: flex-end; }
        .bubble { max-width: 75%; padding: 10px 15px; border-radius: 18px; position: relative; word-wrap: break-word; box-shadow: 0 1px 2px rgba(0,0,0,0.1); }
        .mine .bubble { background-color: #E3F2FD; color: black; border-bottom-right-radius: 4px; }
        .theirs .bubble { background-color: white; color: black; border: 1px solid #e0e0e0; border-bottom-left-radius: 4px; }
        .timestamp { font-size: 10px; margin-top: 4px; opacity: 0.7; text-align: right; }
        .status-message { text-align: center; margin: 15px 0; font-size: 12px; font-weight: 600; opacity: 0.9; animation: fadeIn 0.5s ease; }
        .status-online { color: #4CAF50; }
        .status-busy { color: #F44336; }
        .status-away { color: #FF9800; }
        .status-offline { color: #9E9E9E; }
        @keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
    </style>
</head>
<body>
    <div id=""chat-container""></div>
    <script>
        function addStatusMessage(text, statusClass) {
            const container = document.getElementById('chat-container');
            const msgDiv = document.createElement('div');
            msgDiv.className = 'status-message ' + statusClass;
            msgDiv.innerText = text;
            container.appendChild(msgDiv);
            window.scrollTo(0, document.body.scrollHeight);
        }

        function addMessage(contentBase64, isMine, time) {
            const container = document.getElementById('chat-container');
            const msgDiv = document.createElement('div');
            msgDiv.className = 'message ' + (isMine ? 'mine' : 'theirs');
            
            // Decode Base64 (UTF-8 safe)
            let content = '';
            try {
                content = decodeURIComponent(escape(atob(contentBase64)));
            } catch (e) {
                content = atob(contentBase64); // Fallback
            }

            msgDiv.innerHTML = `<div class=""bubble"">${content}<div class=""timestamp"">${time}</div></div>`;
            container.appendChild(msgDiv);
            window.scrollTo(0, document.body.scrollHeight);
        }
        function clearChat() {
            document.getElementById('chat-container').innerHTML = '';
        }
    </script>
</body>
</html>";
                ChatWebView.NavigateToString(html);
                ChatWebView.CoreWebView2.DOMContentLoaded += (s, e) => LoadHistory();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur critique du composant Chat (WebView2) : {ex.Message}\n\nAssurez-vous que 'WebView2 Runtime' est installé.", "Erreur Chat", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadHistory()
        {
            var messages = _dbService.GetMessages(_currentUser, _partnerUser);
            foreach (var msg in messages)
            {
                AppendMessageToUi(msg);
                if (msg.Id > _lastMessageId) _lastMessageId = msg.Id;
            }
            
            // Initial Status Message (at the bottom)
            UpdateStatusMessageInChat();

            // Mark as read
            _dbService.MarkMessagesAsRead(_partnerUser, _currentUser);
        }

        private void UpdateStatusMessageInChat()
        {
            string status = PartnerStatus.Text;
            string name = PartnerName.Text;
            string cssClass = "status-offline";
            
            if (status.ToLower() == "en ligne") cssClass = "status-online";
            else if (status.ToLower() == "occupé" || status.ToLower() == "ne pas déranger") cssClass = "status-busy";
            else if (status.ToLower() == "absent") cssClass = "status-away";

            string message = $"{name} est actuellement {status}";
            string script = $"addStatusMessage('{message}', '{cssClass}');";
            ChatWebView.ExecuteScriptAsync(script);
        }

        private void PollTimer_Tick(object? sender, EventArgs e)
        {
            var messages = _dbService.GetMessages(_currentUser, _partnerUser);
            var newMessages = messages.Where(m => m.Id > _lastMessageId).ToList();
            
            foreach (var msg in newMessages)
            {
                AppendMessageToUi(msg);
                if (msg.Id > _lastMessageId) _lastMessageId = msg.Id;
            }
            
            if (newMessages.Any())
            {
                _dbService.MarkMessagesAsRead(_partnerUser, _currentUser);
            }

            // Check Typing Status
            bool isTyping = _dbService.GetTypingStatus(_partnerUser, _currentUser);
            if (isTyping)
            {
                TypingIndicator.Visibility = Visibility.Visible;
                string name = PartnerName.Text.Split(' ')[0];
                TypingIndicator.Text = $"{name} est en train d'écrire...";
            }
            else
            {
                TypingIndicator.Visibility = Visibility.Collapsed;
            }

            // Update Partner Status
            string currentStatus = _dbService.GetUserStatus(_partnerUser);
            if (PartnerStatus.Text != currentStatus)
            {
                PartnerStatus.Text = currentStatus;
                var statusColor = GetStatusColor(currentStatus);
                PartnerStatus.Foreground = statusColor;
                StatusIndicator.Fill = statusColor;
                
                // Inject status update in chat
                UpdateStatusMessageInChat();
            }
        }

        private void AppendMessageToUi(ChatMessage msg)
        {
            string base64Content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(msg.Content));
            string time = msg.Timestamp.ToString("HH:mm");
            string script = $"addMessage('{base64Content}', {(msg.IsMine ? "true" : "false")}, '{time}');";
            ChatWebView.ExecuteScriptAsync(script);
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    return;
                }
                e.Handled = true;
                SendMessage();
            }
        }

        private void MessageInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_dbService == null) return;

            string text = GetTextFromRichTextBox(MessageInput).Trim();
            if (!string.IsNullOrEmpty(text) && (DateTime.Now - _lastTypingSent).TotalSeconds > 2)
            {
                _dbService.SetTypingStatus(_currentUser, _partnerUser, true);
                _lastTypingSent = DateTime.Now;
            }
        }

        private string GetTextFromRichTextBox(RichTextBox rtb)
        {
            TextRange textRange = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
            return textRange.Text;
        }

        private void ApplyFormatting(DependencyProperty property, object? value)
        {
            if (MessageInput == null) return;
            MessageInput.Selection.ApplyPropertyValue(property, value);
            MessageInput.Focus();
        }

        private void FormatBold_Click(object sender, RoutedEventArgs e)
        {
            var currentWeight = MessageInput.Selection.GetPropertyValue(TextElement.FontWeightProperty);
            if (currentWeight != DependencyProperty.UnsetValue && (FontWeight)currentWeight == FontWeights.Bold)
                ApplyFormatting(TextElement.FontWeightProperty, FontWeights.Normal);
            else
                ApplyFormatting(TextElement.FontWeightProperty, FontWeights.Bold);
        }

        private void FormatItalic_Click(object sender, RoutedEventArgs e)
        {
            var currentStyle = MessageInput.Selection.GetPropertyValue(TextElement.FontStyleProperty);
            if (currentStyle != DependencyProperty.UnsetValue && (FontStyle)currentStyle == FontStyles.Italic)
                ApplyFormatting(TextElement.FontStyleProperty, FontStyles.Normal);
            else
                ApplyFormatting(TextElement.FontStyleProperty, FontStyles.Italic);
        }

        private void FormatUnderline_Click(object sender, RoutedEventArgs e)
        {
            var currentDeco = MessageInput.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
            TextDecorationCollection? decorations = currentDeco as TextDecorationCollection;
            if (decorations != null && decorations.Count > 0 && decorations.Any(d => d.Location == TextDecorationLocation.Underline))
                ApplyFormatting(Inline.TextDecorationsProperty, null);
            else
                ApplyFormatting(Inline.TextDecorationsProperty, TextDecorations.Underline);
        }
        
        private void Color_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string colorCode)
            {
                try 
                {
                    Color color = (Color)ColorConverter.ConvertFromString(colorCode);
                    ApplyFormatting(TextElement.ForegroundProperty, new SolidColorBrush(color));
                    BtnColor.IsChecked = false;
                }
                catch { }
            }
        }

        private string ConvertRichTextBoxToHtml(RichTextBox rtb)
        {
            StringBuilder html = new StringBuilder();
            foreach (Block block in rtb.Document.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    foreach (Inline inline in paragraph.Inlines)
                    {
                        if (inline is Run run)
                        {
                            string text = System.Net.WebUtility.HtmlEncode(run.Text);
                            if (run.FontWeight == FontWeights.Bold) text = $"<b>{text}</b>";
                            if (run.FontStyle == FontStyles.Italic) text = $"<i>{text}</i>";
                            if (run.TextDecorations.Any(d => d.Location == TextDecorationLocation.Underline)) text = $"<u>{text}</u>";
                            
                            if (run.Foreground is SolidColorBrush brush && brush.Color != Colors.Black && brush.Color != (Color)ColorConverter.ConvertFromString("#FF333333"))
                            {
                                text = $"<span style='color:{brush.Color}'>{text}</span>";
                            }
                            html.Append(text);
                        }
                        else if (inline is LineBreak)
                        {
                            html.Append("<br/>");
                        }
                    }
                    html.Append("<br/>");
                }
            }
            string result = html.ToString();
            if (result.EndsWith("<br/>")) result = result.Substring(0, result.Length - 5);
            return result;
        }

        private void SendMessage()
        {
            string plainText = GetTextFromRichTextBox(MessageInput).Trim();
            if (string.IsNullOrEmpty(plainText)) return;

            string content = ConvertRichTextBoxToHtml(MessageInput);

            // Capture current formatting to persist it
            object fontWeight = MessageInput.Selection.GetPropertyValue(TextElement.FontWeightProperty);
            object fontStyle = MessageInput.Selection.GetPropertyValue(TextElement.FontStyleProperty);
            object textDecorations = MessageInput.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
            object foreground = MessageInput.Selection.GetPropertyValue(TextElement.ForegroundProperty);

            // If mixed, try to get from the last run
            if (fontWeight == DependencyProperty.UnsetValue || fontStyle == DependencyProperty.UnsetValue || 
                textDecorations == DependencyProperty.UnsetValue || foreground == DependencyProperty.UnsetValue)
            {
                var lastBlock = MessageInput.Document.Blocks.LastBlock as Paragraph;
                if (lastBlock != null && lastBlock.Inlines.LastInline is Run lastRun)
                {
                    if (fontWeight == DependencyProperty.UnsetValue) fontWeight = lastRun.FontWeight;
                    if (fontStyle == DependencyProperty.UnsetValue) fontStyle = lastRun.FontStyle;
                    if (textDecorations == DependencyProperty.UnsetValue) textDecorations = lastRun.TextDecorations;
                    if (foreground == DependencyProperty.UnsetValue) foreground = lastRun.Foreground;
                }
            }

            int newId = _dbService.SendMessage(_currentUser, _partnerUser, content);
            
            // Clear and restore formatting
            MessageInput.Document.Blocks.Clear();
            Paragraph p = new Paragraph();
            Run r = new Run();
            p.Inlines.Add(r);
            MessageInput.Document.Blocks.Add(p);

            if (fontWeight != DependencyProperty.UnsetValue && fontWeight != null) r.FontWeight = (FontWeight)fontWeight;
            if (fontStyle != DependencyProperty.UnsetValue && fontStyle != null) r.FontStyle = (FontStyle)fontStyle;
            if (textDecorations != DependencyProperty.UnsetValue && textDecorations != null) r.TextDecorations = (TextDecorationCollection)textDecorations;
            if (foreground != DependencyProperty.UnsetValue && foreground != null) r.Foreground = (Brush)foreground;

            MessageInput.CaretPosition = r.ContentEnd;
            MessageInput.Focus();

            _dbService.SetTypingStatus(_currentUser, _partnerUser, false);
            
            if (newId > 0)
            {
                _lastMessageId = newId;
                var tempMsg = new ChatMessage 
                { 
                    Id = newId,
                    Content = content, 
                    IsMine = true, 
                    Timestamp = DateTime.Now 
                };
                AppendMessageToUi(tempMsg);
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
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
    }
}