using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PaLX.Client.Controls
{
    /// <summary>
    /// Contrôle personnalisé qui affiche du texte avec formatage HTML et smileys
    /// Supporte: &lt;b&gt;, &lt;i&gt;, &lt;u&gt;, &lt;span style='color:...'&gt;, [smiley:xxx.png]
    /// Gère les balises imbriquées comme &lt;span&gt;&lt;b&gt;&lt;i&gt;texte&lt;/i&gt;&lt;/b&gt;&lt;/span&gt;
    /// </summary>
    public class RichMessageTextBlock : TextBlock
    {
        public static readonly DependencyProperty RichContentProperty =
            DependencyProperty.Register(
                nameof(RichContent),
                typeof(string),
                typeof(RichMessageTextBlock),
                new PropertyMetadata(string.Empty, OnRichContentChanged));

        public string RichContent
        {
            get => (string)GetValue(RichContentProperty);
            set => SetValue(RichContentProperty, value);
        }

        private static void OnRichContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RichMessageTextBlock control && e.NewValue is string content)
            {
                control.ParseAndRender(content);
            }
        }

        private void ParseAndRender(string content)
        {
            Inlines.Clear();

            if (string.IsNullOrEmpty(content))
                return;

            try
            {
                var inlines = ParseHtmlContent(content, new TextStyle());
                foreach (var inline in inlines)
                {
                    Inlines.Add(inline);
                }
            }
            catch
            {
                // Fallback: afficher le texte brut en cas d'erreur
                Inlines.Add(new Run(content));
            }
        }

        // Structure pour stocker le style courant
        private class TextStyle
        {
            public bool IsBold { get; set; }
            public bool IsItalic { get; set; }
            public bool IsUnderline { get; set; }
            public string? Color { get; set; }

            public TextStyle Clone()
            {
                return new TextStyle
                {
                    IsBold = this.IsBold,
                    IsItalic = this.IsItalic,
                    IsUnderline = this.IsUnderline,
                    Color = this.Color
                };
            }
        }

        private List<Inline> ParseHtmlContent(string content, TextStyle currentStyle)
        {
            var result = new List<Inline>();
            int pos = 0;

            while (pos < content.Length)
            {
                // Chercher le prochain smiley ou tag HTML
                int smileyStart = content.IndexOf("[smiley:", pos, StringComparison.OrdinalIgnoreCase);
                int tagStart = content.IndexOf('<', pos);

                // Si aucun pattern trouvé, ajouter le reste comme texte
                if (smileyStart == -1 && tagStart == -1)
                {
                    string remaining = content.Substring(pos);
                    if (!string.IsNullOrEmpty(remaining))
                    {
                        result.Add(CreateStyledRun(remaining, currentStyle));
                    }
                    break;
                }

                // Déterminer quel pattern vient en premier
                int nextPattern = -1;
                bool isSmiley = false;

                if (smileyStart != -1 && (tagStart == -1 || smileyStart < tagStart))
                {
                    nextPattern = smileyStart;
                    isSmiley = true;
                }
                else if (tagStart != -1)
                {
                    nextPattern = tagStart;
                    isSmiley = false;
                }

                // Ajouter le texte avant le pattern
                if (nextPattern > pos)
                {
                    string textBefore = content.Substring(pos, nextPattern - pos);
                    if (!string.IsNullOrEmpty(textBefore))
                    {
                        result.Add(CreateStyledRun(textBefore, currentStyle));
                    }
                }

                if (isSmiley)
                {
                    // Parser le smiley [smiley:pxt_XX/N.ext] ou ancien format [smiley:b_s_X.png]
                    var smileyMatch = Regex.Match(content.Substring(nextPattern), @"^\[smiley:(pxt_\d{2}/\d+\.(png|gif)|b_s_\d+\.png)\]", RegexOptions.IgnoreCase);
                    if (smileyMatch.Success)
                    {
                        string smileyFile = smileyMatch.Groups[1].Value;
                        var inlineImage = CreateSmileyInline(smileyFile);
                        if (inlineImage != null)
                            result.Add(inlineImage);
                        else
                            result.Add(new Run("[" + smileyFile + "]"));
                        pos = nextPattern + smileyMatch.Length;
                    }
                    else
                    {
                        // Pas un vrai smiley, traiter comme texte
                        result.Add(CreateStyledRun("[", currentStyle));
                        pos = nextPattern + 1;
                    }
                }
                else
                {
                    // Parser le tag HTML
                    var tagMatch = Regex.Match(content.Substring(nextPattern), @"^<(span|b|i|u)(?:\s+style=['""]([^'""]*)['""])?\s*>", RegexOptions.IgnoreCase);
                    if (tagMatch.Success)
                    {
                        string tagName = tagMatch.Groups[1].Value.ToLower();
                        string style = tagMatch.Groups[2].Value;

                        // Créer un nouveau style basé sur le tag
                        var newStyle = currentStyle.Clone();
                        ApplyTagStyle(newStyle, tagName, style);

                        // Trouver la balise fermante correspondante
                        int tagContentStart = nextPattern + tagMatch.Length;
                        int closingTagPos = FindClosingTag(content, tagContentStart, tagName);

                        if (closingTagPos != -1)
                        {
                            string innerContent = content.Substring(tagContentStart, closingTagPos - tagContentStart);
                            
                            // Parser récursivement le contenu interne
                            var innerInlines = ParseHtmlContent(innerContent, newStyle);
                            result.AddRange(innerInlines);

                            // Avancer après la balise fermante
                            pos = closingTagPos + $"</{tagName}>".Length;
                        }
                        else
                        {
                            // Pas de balise fermante, traiter le tag comme texte
                            result.Add(CreateStyledRun(tagMatch.Value, currentStyle));
                            pos = nextPattern + tagMatch.Length;
                        }
                    }
                    else
                    {
                        // Vérifier si c'est une balise fermante orpheline
                        var closingMatch = Regex.Match(content.Substring(nextPattern), @"^</(span|b|i|u)>", RegexOptions.IgnoreCase);
                        if (closingMatch.Success)
                        {
                            // Ignorer les balises fermantes orphelines
                            pos = nextPattern + closingMatch.Length;
                        }
                        else
                        {
                            // Pas un vrai tag, traiter '<' comme texte
                            result.Add(CreateStyledRun("<", currentStyle));
                            pos = nextPattern + 1;
                        }
                    }
                }
            }

            return result;
        }

        private int FindClosingTag(string content, int startPos, string tagName)
        {
            int depth = 1;
            int pos = startPos;
            string openTag = $"<{tagName}";
            string closeTag = $"</{tagName}>";

            while (pos < content.Length && depth > 0)
            {
                int nextOpen = content.IndexOf(openTag, pos, StringComparison.OrdinalIgnoreCase);
                int nextClose = content.IndexOf(closeTag, pos, StringComparison.OrdinalIgnoreCase);

                if (nextClose == -1)
                    return -1; // Pas de balise fermante

                if (nextOpen != -1 && nextOpen < nextClose)
                {
                    // Une balise ouvrante avant la fermante - incrémenter la profondeur
                    depth++;
                    pos = nextOpen + openTag.Length;
                }
                else
                {
                    // Balise fermante trouvée
                    depth--;
                    if (depth == 0)
                        return nextClose;
                    pos = nextClose + closeTag.Length;
                }
            }

            return -1;
        }

        private void ApplyTagStyle(TextStyle style, string tagName, string inlineStyle)
        {
            switch (tagName)
            {
                case "b":
                    style.IsBold = true;
                    break;
                case "i":
                    style.IsItalic = true;
                    break;
                case "u":
                    style.IsUnderline = true;
                    break;
                case "span":
                    // Parser le style inline pour span
                    if (!string.IsNullOrEmpty(inlineStyle))
                    {
                        ParseInlineStyleToTextStyle(style, inlineStyle);
                    }
                    break;
            }
        }

        private void ParseInlineStyleToTextStyle(TextStyle style, string inlineStyle)
        {
            // Parser: color:#XXXXXX ou color:red, etc.
            var colorMatch = Regex.Match(inlineStyle, @"color:\s*([^;]+)", RegexOptions.IgnoreCase);
            if (colorMatch.Success)
            {
                style.Color = colorMatch.Groups[1].Value.Trim();
            }

            // Parser: font-weight:bold
            if (inlineStyle.Contains("font-weight:bold", StringComparison.OrdinalIgnoreCase) ||
                inlineStyle.Contains("font-weight: bold", StringComparison.OrdinalIgnoreCase))
            {
                style.IsBold = true;
            }

            // Parser: font-style:italic
            if (inlineStyle.Contains("font-style:italic", StringComparison.OrdinalIgnoreCase) ||
                inlineStyle.Contains("font-style: italic", StringComparison.OrdinalIgnoreCase))
            {
                style.IsItalic = true;
            }

            // Parser: text-decoration:underline
            if (inlineStyle.Contains("text-decoration:underline", StringComparison.OrdinalIgnoreCase) ||
                inlineStyle.Contains("text-decoration: underline", StringComparison.OrdinalIgnoreCase))
            {
                style.IsUnderline = true;
            }
        }

        private Run CreateStyledRun(string text, TextStyle style)
        {
            var run = new Run(text);

            if (style.IsBold)
                run.FontWeight = FontWeights.Bold;

            if (style.IsItalic)
                run.FontStyle = FontStyles.Italic;

            if (style.IsUnderline)
                run.TextDecorations = System.Windows.TextDecorations.Underline;

            if (!string.IsNullOrEmpty(style.Color))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(style.Color);
                    run.Foreground = new SolidColorBrush(color);
                }
                catch { /* Ignorer les couleurs invalides */ }
            }

            return run;
        }

        private InlineUIContainer? CreateSmileyInline(string smileyFile)
        {
            try
            {
                // Normaliser le chemin (remplacer / par le séparateur du système)
                string normalizedFile = smileyFile.Replace('/', System.IO.Path.DirectorySeparatorChar);
                
                string smileyPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Smiley", normalizedFile);

                if (!System.IO.File.Exists(smileyPath))
                    return null;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(smileyPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelHeight = 48;
                bitmap.EndInit();
                bitmap.Freeze();

                var image = new Image
                {
                    Source = bitmap,
                    Width = 48,
                    Height = 48,
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(1, 0, 1, 0)
                };

                return new InlineUIContainer(image) { BaselineAlignment = BaselineAlignment.Center };
            }
            catch
            {
                return null;
            }
        }
    }
}
