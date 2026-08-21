using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OCIDE.Editor
{
    public class CustomTextEditor : RichTextBox
    {
        public CustomTextEditor()
        {
            FontFamily = new FontFamily("Consolas");
            FontSize = 14;
            Background = Brushes.Transparent;
            Foreground = Brushes.LightGray;
            BorderThickness = new Thickness(0);
            AcceptsReturn = true;
            AcceptsTab = true;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
    }
}
