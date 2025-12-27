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
using PaLX.Admin.Services;
using System.Threading.Tasks;

namespace PaLX.Admin
{
    public partial class ChatWindow : Window
    {
        private string _currentUser;
        private string _currentUserDisplayName;
        private string _partnerUser;
        private DateTime _lastTypingSent = DateTime.MinValue;
        private System.Media.SoundPlayer? _messageSound;
        private bool _isBlocked = false;
        private bool _isBlockedByPartner = false;

        public ChatWindow(string currentUser, string partnerUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _currentUserDisplayName = currentUser; // Default
            _partnerUser = partnerUser;
            
            // Subscribe to SignalR
            ApiService.Instance.OnPrivateMessageReceived += OnPrivateMessageReceived;
            ApiService.Instance.OnUserTyping += OnUserTyping;

            // Subscribe to Block Events
            ApiService.Instance.OnUserBlocked += OnUserBlocked;
            ApiService.Instance.OnUserBlockedBy += OnUserBlockedBy;
            ApiService.Instance.OnUserUnblocked += OnUserUnblocked;
            ApiService.Instance.OnUserUnblockedBy += OnUserUnblockedBy;

            // Load Sound
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                _messageSound = new System.Media.SoundPlayer(System.IO.Path.Combine(baseDir, "Assets", "Sounds", "message.wav"));
                _messageSound.LoadAsync();
            }
            catch { }

            InitializeWebView();
            
            Loaded += async (s, e) => 
            {
                await LoadPartnerInfoAsync();
                await CheckBlockStatusAsync();
            };
        }

        private void OnUserBlocked(string blockedUser)
        {
            if (blockedUser == _partnerUser)
            {
                Dispatcher.Invoke(() => 
                {
                    _isBlocked = true;
                    UpdateBlockUi();

                    string msg = $"Vous avez bloqué {PartnerName.Text} – Blocage PERMANENT – {DateTime.Now:dd/MM/yyyy HH:mm}";
                    string script = $"addStatusMessage('{msg}', 'status-blocked');";
                    ChatWebView.ExecuteScriptAsync(script);
                });
            }
        }

        private void OnUserBlockedBy(string blocker)
        {
            if (blocker == _partnerUser)
            {
                Dispatcher.Invoke(() => 
                {
                    _isBlockedByPartner = true;
                    UpdateBlockUi();

                    string msg = $"{PartnerName.Text} vous a bloqué – Blocage PERMANENT – {DateTime.Now:dd/MM/yyyy HH:mm}";
                    string script = $"addStatusMessage('{msg}', 'status-blocked');";
                    ChatWebView.ExecuteScriptAsync(script);
                });
            }
        }

        private void OnUserUnblocked(string unblockedUser)
        {
            if (unblockedUser == _partnerUser)
            {
                Dispatcher.Invoke(() => 
                {
                    _isBlocked = false;
                    UpdateBlockUi();

                    string msg = $"Vous avez débloqué {PartnerName.Text} – {DateTime.Now:dd/MM/yyyy HH:mm}";
                    string script = $"addStatusMessage('{msg}', 'status-unblocked');";
                    ChatWebView.ExecuteScriptAsync(script);
                });
            }
        }

        private void OnUserUnblockedBy(string blocker)
        {
            if (blocker == _partnerUser)
            {
                Dispatcher.Invoke(() => 
                {
                    _isBlockedByPartner = false;
                    UpdateBlockUi();

                    string msg = $"{PartnerName.Text} vous a débloqué – {DateTime.Now:dd/MM/yyyy HH:mm}";
                    string script = $"addStatusMessage('{msg}', 'status-unblocked');";
                    ChatWebView.ExecuteScriptAsync(script);
                });
            }
        }

        private void UpdateBlockUi()
        {
            if (_isBlocked || _isBlockedByPartner)
            {
                MessageInput.IsEnabled = false;
                SendButton.IsEnabled = false;
                AttachmentButton.IsEnabled = false;
                AudioButton.IsEnabled = false;
                EmojiButton.IsEnabled = false;
                
                if (_isBlocked)
                {
                    BlockButton.ToolTip = "Débloquer cet utilisateur";
                    BlockIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
                    TypingIndicator.Text = $"Vous avez bloqué {PartnerName.Text}.";
                }
                else
                {
                    BlockButton.Visibility = Visibility.Collapsed;
                    TypingIndicator.Text = $"{PartnerName.Text} vous a bloqué.";
                }
                
                TypingIndicator.Visibility = Visibility.Visible;
                TypingIndicator.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
            }
            else
            {
                MessageInput.IsEnabled = true;
                SendButton.IsEnabled = true;
                AttachmentButton.IsEnabled = true;
                AudioButton.IsEnabled = true;
                EmojiButton.IsEnabled = true;

                BlockButton.Visibility = Visibility.Visible;
                BlockButton.ToolTip = "Bloquer cet utilisateur";
                BlockIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
                
                TypingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadPartnerInfoAsync()
        {
            // Load my profile for display name
            var myProfile = await ApiService.Instance.GetUserProfileAsync(_currentUser);
            if (myProfile != null) _currentUserDisplayName = $"{myProfile.LastName} {myProfile.FirstName}";

            // Load partner details (avatar, status)
            var profile = await ApiService.Instance.GetUserProfileAsync(_partnerUser);
            if (profile != null)
            {
                PartnerName.Text = $"{profile.LastName} {profile.FirstName}";
                
                // Set Avatar
                if (!string.IsNullOrEmpty(profile.AvatarPath) && System.IO.File.Exists(profile.AvatarPath))
                {
                    try
                    {
                        AvatarBrush.ImageSource = new BitmapImage(new Uri(profile.AvatarPath, UriKind.Absolute));
                    }
                    catch { /* Keep default */ }
                }
            }
            else
            {
                PartnerName.Text = _partnerUser;
            }

            // Get Status from Friends list (as Profile doesn't have status)
            var friends = await ApiService.Instance.GetFriendsAsync();
            var friend = friends.FirstOrDefault(f => f.Username == _partnerUser);
            if (friend != null)
            {
                PartnerStatus.Text = friend.Status;
                var statusColor = GetStatusColor(friend.Status);
                PartnerStatus.Foreground = statusColor;
                StatusIndicator.Fill = statusColor;
            }
        }

        private void OnPrivateMessageReceived(string sender, string message)
        {
            if (sender == _partnerUser)
            {
                Dispatcher.Invoke(() => 
                {
                    var msg = new ChatMessageDto 
                    { 
                        Content = message, 
                        Sender = sender,
                        Receiver = _currentUser,
                        Timestamp = DateTime.Now 
                    };
                    AppendMessageToUi(msg);
                    
                    if (!this.IsActive)
                    {
                        _messageSound?.Play();
                    }
                });
            }
        }

        private void OnUserTyping(string user)
        {
            if (user == _partnerUser)
            {
                Dispatcher.Invoke(() =>
                {
                    TypingIndicator.Visibility = Visibility.Visible;
                    string name = PartnerName.Text.Split(' ')[0];
                    TypingIndicator.Text = $"{name} est en train d'écrire...";
                    
                    // Hide after 3 seconds
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    timer.Tick += (s, args) => 
                    {
                        TypingIndicator.Visibility = Visibility.Collapsed;
                        timer.Stop();
                    };
                    timer.Start();
                });
            }
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
                
                // Map Assets folder for Smileys
                string assetsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                ChatWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "assets", assetsPath, CoreWebView2HostResourceAccessKind.Allow);
                
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
        .status-blocked { color: #D32F2F; font-weight: bold; font-size: 14px; margin-top: 20px; margin-bottom: 20px; border: 2px solid #D32F2F; padding: 10px; border-radius: 8px; background-color: #FFEBEE; }
        .status-unblocked { color: #4CAF50; font-weight: bold; font-size: 14px; margin-top: 20px; margin-bottom: 20px; border: 2px solid #4CAF50; padding: 10px; border-radius: 8px; background-color: #E8F5E9; }
        .smiley { width: 40px; height: 40px; vertical-align: middle; margin: 0 2px; }
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

            // Replace smiley codes with images
            // Format: [smiley:b_s_1.png]
            content = content.replace(/\[smiley:(b_s_\d+\.png)\]/g, '<img src=""https://assets/Smiley/$1"" class=""smiley"" />');

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
                ChatWebView.CoreWebView2.DOMContentLoaded += async (s, e) => await LoadHistoryAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur critique du composant Chat (WebView2) : {ex.Message}\n\nAssurez-vous que 'WebView2 Runtime' est installé.", "Erreur Chat", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadHistoryAsync()
        {
            if (ChatWebView.CoreWebView2 == null) return;

            var messages = await ApiService.Instance.GetChatHistoryAsync(_partnerUser);
            foreach (var msg in messages)
            {
                if (msg.Content == "[SYSTEM_BLOCK]")
                {
                    string text;
                    string partnerName = !string.IsNullOrEmpty(PartnerName.Text) ? PartnerName.Text : _partnerUser;

                    if (msg.Sender == _currentUser)
                        text = $"Vous avez bloqué {partnerName} – Blocage PERMANENT – {msg.Timestamp:dd/MM/yyyy HH:mm}";
                    else
                        text = $"{partnerName} vous a bloqué – Blocage PERMANENT – {msg.Timestamp:dd/MM/yyyy HH:mm}";
                    
                    string script = $"addStatusMessage('{text}', 'status-blocked');";
                    await ChatWebView.ExecuteScriptAsync(script);
                }
                else if (msg.Content == "[SYSTEM_UNBLOCK]")
                {
                    string text;
                    string partnerName = !string.IsNullOrEmpty(PartnerName.Text) ? PartnerName.Text : _partnerUser;

                    if (msg.Sender == _currentUser)
                        text = $"Vous avez débloqué {partnerName} – {msg.Timestamp:dd/MM/yyyy HH:mm}";
                    else
                        text = $"{partnerName} vous a débloqué – {msg.Timestamp:dd/MM/yyyy HH:mm}";
                    
                    string script = $"addStatusMessage('{text}', 'status-unblocked');";
                    await ChatWebView.ExecuteScriptAsync(script);
                }
                else
                {
                    AppendMessageToUi(msg);
                }
            }
            
            // Initial Status Message (at the bottom)
            UpdateStatusMessageInChat();

            // Mark as read
            await ApiService.Instance.MarkMessagesAsReadAsync(_partnerUser);
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

        private async Task CheckBlockStatusAsync()
        {
            bool blockedByMe = await ApiService.Instance.IsUserBlockedAsync(_currentUser, _partnerUser);
            bool blockedByPartner = await ApiService.Instance.IsUserBlockedAsync(_partnerUser, _currentUser);

            if (blockedByMe != _isBlocked || blockedByPartner != _isBlockedByPartner)
            {
                _isBlocked = blockedByMe;
                _isBlockedByPartner = blockedByPartner;
                UpdateBlockUi();
            }
        }



        private async void Block_Click(object sender, RoutedEventArgs e)
        {
            if (_isBlocked)
            {
                // Unblock
                var result = await ApiService.Instance.UnblockUserAsync(_partnerUser);
                if (result.Success)
                {
                    _isBlocked = false;
                    // Message will be added via SignalR event
                }
                else
                {
                    new CustomAlertWindow(result.Message, "Erreur").ShowDialog();
                }
            }
            else
            {
                // Block
                var confirm = new CustomConfirmWindow($"Voulez-vous vraiment bloquer {_partnerUser} ?\nVous ne pourrez plus échanger de messages.", "Confirmer le blocage");
                if (confirm.ShowDialog() == true)
                {
                    // Pass a default reason to ensure consistency with MainView
                    var result = await ApiService.Instance.BlockUserAsync(_partnerUser, 0, null, "Bloqué depuis le chat");
                    if (result.Success)
                    {
                        _isBlocked = true;
                        // Message will be added via SignalR event
                    }
                    else
                    {
                        new CustomAlertWindow(result.Message, "Erreur").ShowDialog();
                    }
                }
            }
            await CheckBlockStatusAsync(); // Refresh status immediately
        }

        private void AppendMessageToUi(ChatMessageDto msg)
        {
            string base64Content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(msg.Content));
            string time = msg.Timestamp.ToString("HH:mm");
            bool isMine = msg.Sender == _currentUser;
            string script = $"addMessage('{base64Content}', {(isMine ? "true" : "false")}, '{time}');";
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

        private async void MessageInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string text = GetTextFromRichTextBox(MessageInput).Trim();
                if (!string.IsNullOrEmpty(text) && (DateTime.Now - _lastTypingSent).TotalSeconds > 2)
                {
                    await ApiService.Instance.SendTypingIndicatorAsync(_partnerUser);
                    _lastTypingSent = DateTime.Now;
                }
            }
            catch { /* Ignore typing errors to prevent crash */ }
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

        private void EmojiButton_Click(object sender, RoutedEventArgs e)
        {
            if (SmileyPanel.Children.Count == 0)
            {
                LoadSmileys();
            }
            SmileyPopup.IsOpen = !SmileyPopup.IsOpen;
        }

        private void LoadSmileys()
        {
            string smileyPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Smiley");
            if (System.IO.Directory.Exists(smileyPath))
            {
                var files = System.IO.Directory.GetFiles(smileyPath, "*.png");
                var sortedFiles = files.OrderBy(f => {
                    string name = System.IO.Path.GetFileNameWithoutExtension(f);
                    string numberPart = name.Replace("b_s_", "");
                    if (int.TryParse(numberPart, out int n)) return n;
                    return 999;
                });

                foreach (var file in sortedFiles)
                {
                    var btn = new Button
                    {
                        Width = 32,
                        Height = 32,
                        Margin = new Thickness(2),
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Cursor = Cursors.Hand,
                        Tag = System.IO.Path.GetFileName(file)
                    };

                    var img = new Image
                    {
                        Source = new BitmapImage(new Uri(file, UriKind.Absolute)),
                        Stretch = Stretch.Uniform
                    };
                    
                    btn.Content = img;
                    btn.Click += Smiley_Click;
                    SmileyPanel.Children.Add(btn);
                }
            }
        }

        private void Smiley_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string filename)
            {
                string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Smiley", filename);
                
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                var image = new Image { Source = bitmap, Width = 30, Height = 30, Stretch = Stretch.Uniform, Tag = filename };
                
                // Insert into RichTextBox
                var container = new InlineUIContainer(image, MessageInput.CaretPosition);
                
                // Move caret after the image
                MessageInput.CaretPosition = container.ElementEnd;
                MessageInput.Focus();
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
                        if (inline is InlineUIContainer uiContainer && uiContainer.Child is Image img && img.Tag is string filename)
                        {
                            html.Append($"[smiley:{filename}]");
                        }
                        else if (inline is Run run)
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

        private async void SendMessage()
        {
            if (_isBlocked || _isBlockedByPartner)
            {
                new CustomAlertWindow("Vous ne pouvez pas envoyer de message à cet utilisateur.").ShowDialog();
                return;
            }

            string content = ConvertRichTextBoxToHtml(MessageInput);
            string plainText = GetTextFromRichTextBox(MessageInput).Trim();
            
            if (string.IsNullOrEmpty(plainText) && !content.Contains("[smiley:")) return;

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

            try
            {
                // Send via API
                await ApiService.Instance.SendPrivateMessageAsync(_partnerUser, content);
                
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

                var tempMsg = new ChatMessageDto 
                { 
                    Content = content, 
                    Sender = _currentUser,
                    Receiver = _partnerUser,
                    Timestamp = DateTime.Now 
                };
                AppendMessageToUi(tempMsg);
            }
            catch (Exception)
            {
                new CustomAlertWindow("Une erreur est survenue lors de l'envoi du message. Veuillez réessayer.", "Erreur d'envoi").ShowDialog();
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