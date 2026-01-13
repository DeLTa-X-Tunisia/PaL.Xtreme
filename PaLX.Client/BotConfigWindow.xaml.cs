using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PaLX.Client.Services;

namespace PaLX.Client
{
    public partial class BotConfigWindow : Window
    {
        private readonly int _roomId;
        private BotConfigDto? _config;
        private ObservableCollection<BannedWordViewModel> _bannedWords = new();

        public BotConfigWindow(int roomId)
        {
            InitializeComponent();
            _roomId = roomId;
            BannedWordsContainer.ItemsSource = _bannedWords;
            
            Loaded += BotConfigWindow_Loaded;
        }

        private async void BotConfigWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Charger la config du bot
                _config = await ApiService.Instance.GetBotConfigAsync(_roomId);
                
                if (_config != null)
                {
                    // Appliquer la config aux contrôles
                    BotEnabledToggle.IsChecked = _config.IsEnabled;
                    BotNameTextBox.Text = _config.BotName;
                    
                    WelcomeMessageCheckBox.IsChecked = _config.WelcomeMessageEnabled;
                    ModerationCheckBox.IsChecked = _config.ModerationEnabled;
                    MentionResponseCheckBox.IsChecked = _config.MentionResponseEnabled;
                    QuizCheckBox.IsChecked = _config.QuizEnabled;
                    TopicSuggestionCheckBox.IsChecked = _config.TopicSuggestionEnabled;
                    
                    WarningsBeforeKickTextBox.Text = _config.WarningsBeforeKick.ToString();
                    WarningResetMinutesTextBox.Text = _config.WarningResetMinutes.ToString();
                    
                    WelcomeMessageTextBox.Text = _config.WelcomeMessageTemplate;
                    WarningMessageTextBox.Text = _config.WarningMessageTemplate;
                    KickMessageTextBox.Text = _config.KickMessageTemplate;
                }
                else
                {
                    // Valeurs par défaut
                    BotEnabledToggle.IsChecked = true;
                    BotNameTextBox.Text = "PaLX Bot";
                    WelcomeMessageCheckBox.IsChecked = true;
                    ModerationCheckBox.IsChecked = true;
                    MentionResponseCheckBox.IsChecked = true;
                    WarningsBeforeKickTextBox.Text = "3";
                    WarningResetMinutesTextBox.Text = "60";
                    WelcomeMessageTextBox.Text = "Bienvenue {username} dans le salon ! 👋";
                    WarningMessageTextBox.Text = "⚠️ {username}, merci de respecter les règles du salon.";
                    KickMessageTextBox.Text = "❌ {username} a été expulsé pour comportement inapproprié.";
                }

                // Charger les mots interdits
                await LoadBannedWordsAsync();
            }
            catch (Exception ex)
            {
                ShowError($"Erreur lors du chargement: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadBannedWordsAsync()
        {
            try
            {
                var words = await ApiService.Instance.GetBannedWordsAsync(_roomId);
                _bannedWords.Clear();
                
                foreach (var word in words)
                {
                    _bannedWords.Add(new BannedWordViewModel
                    {
                        Id = word.Id,
                        Word = word.Word,
                        Severity = word.Severity,
                        SeverityColor = GetSeverityColor(word.Severity)
                    });
                }
            }
            catch
            {
                // Silencieux
            }
        }

        private Brush GetSeverityColor(string severity)
        {
            return severity switch
            {
                "Warning" => new SolidColorBrush(Color.FromRgb(234, 179, 8)),   // Yellow
                "Kick" => new SolidColorBrush(Color.FromRgb(249, 115, 22)),     // Orange
                "Ban" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),       // Red
                _ => new SolidColorBrush(Color.FromRgb(156, 163, 175))          // Gray
            };
        }

        private void BotEnabledToggle_Changed(object sender, RoutedEventArgs e)
        {
            // Optionnel: mettre à jour immédiatement
        }

        private async void AddBannedWord_Click(object sender, RoutedEventArgs e)
        {
            var word = NewWordTextBox.Text.Trim();
            if (string.IsNullOrEmpty(word))
            {
                ShowError("Veuillez entrer un mot à interdire.");
                return;
            }

            var severity = (SeverityComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Warning";

            try
            {
                var result = await ApiService.Instance.AddBannedWordAsync(_roomId, new AddBannedWordDto
                {
                    Word = word,
                    Severity = severity
                });

                if (result != null)
                {
                    _bannedWords.Add(new BannedWordViewModel
                    {
                        Id = result.Id,
                        Word = result.Word,
                        Severity = result.Severity,
                        SeverityColor = GetSeverityColor(result.Severity)
                    });
                    
                    NewWordTextBox.Text = "";
                }
                else
                {
                    ShowError("Impossible d'ajouter le mot interdit.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Erreur: {ex.Message}");
            }
        }

        private async void RemoveBannedWord_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int wordId)
            {
                try
                {
                    var success = await ApiService.Instance.RemoveBannedWordAsync(_roomId, wordId);
                    if (success)
                    {
                        var wordToRemove = _bannedWords.FirstOrDefault(w => w.Id == wordId);
                        if (wordToRemove != null)
                        {
                            _bannedWords.Remove(wordToRemove);
                        }
                    }
                }
                catch
                {
                    ShowError("Impossible de supprimer le mot.");
                }
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validation
                if (!int.TryParse(WarningsBeforeKickTextBox.Text, out var warningsBeforeKick) || warningsBeforeKick < 1)
                {
                    ShowError("Le nombre d'avertissements doit être un nombre positif.");
                    return;
                }

                if (!int.TryParse(WarningResetMinutesTextBox.Text, out var warningResetMinutes) || warningResetMinutes < 1)
                {
                    ShowError("La durée de reset doit être un nombre positif.");
                    return;
                }

                var dto = new UpdateBotConfigDto
                {
                    BotName = BotNameTextBox.Text.Trim(),
                    IsEnabled = BotEnabledToggle.IsChecked ?? true,
                    WelcomeMessageEnabled = WelcomeMessageCheckBox.IsChecked ?? true,
                    ModerationEnabled = ModerationCheckBox.IsChecked ?? true,
                    MentionResponseEnabled = MentionResponseCheckBox.IsChecked ?? true,
                    QuizEnabled = QuizCheckBox.IsChecked ?? false,
                    TopicSuggestionEnabled = TopicSuggestionCheckBox.IsChecked ?? false,
                    WelcomeMessageTemplate = WelcomeMessageTextBox.Text,
                    WarningMessageTemplate = WarningMessageTextBox.Text,
                    KickMessageTemplate = KickMessageTextBox.Text,
                    WarningsBeforeKick = warningsBeforeKick,
                    WarningResetMinutes = warningResetMinutes
                };

                var result = await ApiService.Instance.UpdateBotConfigAsync(_roomId, dto);
                
                if (result != null)
                {
                    var alert = new CustomAlertWindow("Configuration enregistrée !", "La configuration du bot a été sauvegardée avec succès.");
                    alert.Owner = this;
                    alert.ShowDialog();
                    
                    DialogResult = true;
                    Close();
                }
                else
                {
                    ShowError("Impossible de sauvegarder la configuration.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Erreur lors de la sauvegarde: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowError(string message)
        {
            var alert = new CustomAlertWindow("Erreur", message);
            alert.Owner = this;
            alert.ShowDialog();
        }
    }

    public class BannedWordViewModel
    {
        public int Id { get; set; }
        public string Word { get; set; } = string.Empty;
        public string Severity { get; set; } = "Warning";
        public Brush SeverityColor { get; set; } = Brushes.Yellow;
    }
}
