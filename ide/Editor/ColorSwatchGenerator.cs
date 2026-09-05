using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace OCIDE.Editor
{
    public class ColorSwatchGenerator : VisualLineElementGenerator
    {
        private static readonly Regex _colorRegex = new Regex(
            @"\b(red|blue|green|black|white|yellow|orange|purple|pink|brown|gray|cyan|magenta|transparent)\b|#([A-Fa-f0-9]{8}|[A-Fa-f0-9]{6}|[A-Fa-f0-9]{4}|[A-Fa-f0-9]{3})\b|rgba?\([^)]+\)|hsla?\([^)]+\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public override int GetFirstInterestedOffset(int startOffset)
        {
            var document = CurrentContext.Document;
            var endOffset = CurrentContext.VisualLine.LastDocumentLine.EndOffset;
            string text = document.GetText(startOffset, endOffset - startOffset);

            var match = _colorRegex.Match(text);
            if (match.Success)
            {
                return startOffset + match.Index;
            }

            return -1;
        }

        public override VisualLineElement ConstructElement(int offset)
        {
            var document = CurrentContext.Document;
            var endOffset = CurrentContext.VisualLine.LastDocumentLine.EndOffset;
            string text = document.GetText(offset, endOffset - offset);

            var match = _colorRegex.Match(text);
            if (match.Success && match.Index == 0)
            {
                string colorStr = match.Value;
                Color? parsedColor = ParseColor(colorStr);

                if (parsedColor != null)
                {
                    // Consume only the FIRST character of the color string
                    return new ColorSwatchElement(1, parsedColor.Value, colorStr[0].ToString());
                }
            }

            return null;
        }

        private Color? ParseColor(string colorStr)
        {
            try
            {
                if (colorStr.Equals("transparent", StringComparison.OrdinalIgnoreCase)) return Colors.Transparent;
                if (colorStr.Equals("red", StringComparison.OrdinalIgnoreCase)) return Colors.Red;
                if (colorStr.Equals("blue", StringComparison.OrdinalIgnoreCase)) return Colors.Blue;
                if (colorStr.Equals("green", StringComparison.OrdinalIgnoreCase)) return Colors.Green;
                if (colorStr.Equals("black", StringComparison.OrdinalIgnoreCase)) return Colors.Black;
                if (colorStr.Equals("white", StringComparison.OrdinalIgnoreCase)) return Colors.White;
                if (colorStr.Equals("yellow", StringComparison.OrdinalIgnoreCase)) return Colors.Yellow;
                if (colorStr.Equals("orange", StringComparison.OrdinalIgnoreCase)) return Colors.Orange;
                if (colorStr.Equals("purple", StringComparison.OrdinalIgnoreCase)) return Colors.Purple;
                if (colorStr.Equals("pink", StringComparison.OrdinalIgnoreCase)) return Colors.Pink;
                if (colorStr.Equals("brown", StringComparison.OrdinalIgnoreCase)) return Colors.Brown;
                if (colorStr.Equals("gray", StringComparison.OrdinalIgnoreCase)) return Colors.Gray;
                if (colorStr.Equals("cyan", StringComparison.OrdinalIgnoreCase)) return Colors.Cyan;
                if (colorStr.Equals("magenta", StringComparison.OrdinalIgnoreCase)) return Colors.Magenta;
                
                var converted = ColorConverter.ConvertFromString(colorStr);
                if (converted is Color c) return c;
            }
            catch { }
            return null;
        }
    }

    public class ColorSwatchElement : VisualLineElement
    {
        private readonly Color _color;
        private readonly string _firstChar;

        public ColorSwatchElement(int documentLength, Color color, string firstChar)
            : base(1, documentLength)
        {
            _color = color;
            _firstChar = firstChar;
        }

        public override System.Windows.Media.TextFormatting.TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
        {
            return new ColorSwatchTextRun(this, this.TextRunProperties, _color, _firstChar);
        }
    }

    public class ColorSwatchTextRun : InlineObjectRun
    {
        public ColorSwatchTextRun(VisualLineElement element, System.Windows.Media.TextFormatting.TextRunProperties properties, Color color, string firstChar)
            : base(1, properties, CreateElement(color, firstChar, properties))
        {
        }

        private static UIElement CreateElement(Color color, string firstChar, System.Windows.Media.TextFormatting.TextRunProperties properties)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            
            // The swatch
            var border = new Border
            {
                Width = 10,
                Height = 10,
                Background = new SolidColorBrush(color),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(1, 0, 3, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(border);

            // The original first character
            var textBlock = new TextBlock
            {
                Text = firstChar,
                FontFamily = properties.Typeface.FontFamily,
                FontSize = properties.FontRenderingEmSize,
                Foreground = properties.ForegroundBrush,
                FontWeight = properties.Typeface.Weight,
                FontStyle = properties.Typeface.Style,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(textBlock);

            return panel;
        }
    }
}
