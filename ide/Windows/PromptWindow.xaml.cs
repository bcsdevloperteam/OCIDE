using System.Windows;
using System.Windows.Input;

namespace OCIDE.Windows
{
    public partial class PromptWindow : Wpf.Ui.Controls.FluentWindow
    {
        public string InputText { get; private set; } = string.Empty;

        public PromptWindow(string title, string defaultText)
        {
            InitializeComponent();
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);
            
            AppTitleBar.Title = title;
            InputBox.Text = defaultText;

            this.Loaded += (s, e) =>
            {
                InputBox.Focus();
                InputBox.SelectAll();
            };
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            InputText = InputBox.Text;
            DialogResult = true;
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                InputText = InputBox.Text;
                DialogResult = true;
            }
        }
    }
}
