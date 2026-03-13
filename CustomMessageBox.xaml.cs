using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NoteUp
{
    public partial class CustomMessageBox : Window
    {
        public static bool IsDarkTheme { get; set; } = true;

        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;
        private MessageBoxImage _currentIcon = MessageBoxImage.None; // Store icon for theme updates

        private CustomMessageBox(string message, string title, MessageBoxButton buttons, MessageBoxImage icon)
        {
            InitializeComponent();
            
            ApplyTheme();
            
            this.Title = title;
            TitleText.Text = title;
            MessageText.Text = message;

            MessageText.Text = message;

            _currentIcon = icon;
            SetIcon(icon);
            SetButtons(buttons);

            this.Loaded += CustomMessageBox_Loaded;
        }

        private void CustomMessageBox_Loaded(object sender, RoutedEventArgs e)
        {
            var showAnim = (Storyboard)FindResource("ShowAnimation");
            showAnim.Begin(this.Content as FrameworkElement);
        }

        private void SetIcon(MessageBoxImage icon)
        {
            switch (icon)
            {
                case MessageBoxImage.Information:
                    IconText.Text = "\uE946"; // Info icon
                    // Darker blue for Light Mode
                    IconText.Foreground = IsDarkTheme 
                        ? new SolidColorBrush(Color.FromRgb(96, 205, 255)) 
                        : new SolidColorBrush(Color.FromRgb(0, 103, 192));
                    IconText.Visibility = Visibility.Visible;
                    break;
                case MessageBoxImage.Warning:
                    IconText.Text = "\uE7BA"; // Warning icon
                     // Slightly darker yellow/orange for Light Mode could be better but standard is fine
                    IconText.Foreground = IsDarkTheme
                        ? new SolidColorBrush(Color.FromRgb(255, 185, 0))
                        : new SolidColorBrush(Color.FromRgb(157, 93, 0)); // Darker orange for light mode
                    IconText.Visibility = Visibility.Visible;
                    break;
                case MessageBoxImage.Error:
                    IconText.Text = "\uE783"; // Error icon
                    IconText.Foreground = new SolidColorBrush(Color.FromRgb(232, 17, 35));
                    IconText.Visibility = Visibility.Visible;
                    break;
                case MessageBoxImage.Question:
                    IconText.Text = "\uE897"; // Question icon
                    // Darker blue
                    IconText.Foreground = IsDarkTheme 
                        ? new SolidColorBrush(Color.FromRgb(96, 205, 255)) 
                        : new SolidColorBrush(Color.FromRgb(0, 103, 192));
                    IconText.Visibility = Visibility.Visible;
                    break;
                default:
                    IconText.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void SetButtons(MessageBoxButton buttons)
        {
            ButtonPanel.Children.Clear();

            switch (buttons)
            {
                case MessageBoxButton.OK:
                    AddButton("OK", MessageBoxResult.OK, true);
                    break;
                case MessageBoxButton.OKCancel:
                    AddButton("Cancel", MessageBoxResult.Cancel, false);
                    AddButton("OK", MessageBoxResult.OK, true);
                    break;
                case MessageBoxButton.YesNo:
                    AddButton("No", MessageBoxResult.No, false);
                    AddButton("Yes", MessageBoxResult.Yes, true);
                    break;
                case MessageBoxButton.YesNoCancel:
                    AddButton("Cancel", MessageBoxResult.Cancel, false);
                    AddButton("No", MessageBoxResult.No, false);
                    AddButton("Yes", MessageBoxResult.Yes, true);
                    break;
            }
        }

        private void AddButton(string content, MessageBoxResult result, bool isPrimary)
        {
            var button = new Button
            {
                Content = content,
                Style = (Style)FindResource(isPrimary ? "AccentButton" : "SecondaryButton")
            };

            button.Click += (s, e) =>
            {
                Result = result;
                // Wait for animation to complete before closing
                // this.DialogResult = true; 
                
                // Trigger close animation
                var closeAnim = (Storyboard)FindResource("CloseAnimation");
                closeAnim.Begin(this);
            };

            ButtonPanel.Children.Add(button);
        }
        
        private void CloseAnimation_Completed(object? sender, EventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        public static MessageBoxResult Show(string messageBoxText)
        {
            return Show(messageBoxText, "NoteUp", MessageBoxButton.OK, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string messageBoxText, string caption)
        {
            return Show(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button)
        {
            return Show(messageBoxText, caption, button, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            var msgBox = new CustomMessageBox(messageBoxText, caption, button, icon);
            
            try
            {
                // Try to center on active window
                var activeWindow = Application.Current.MainWindow;
                if (activeWindow != null && activeWindow.IsLoaded)
                {
                    msgBox.Owner = activeWindow;
                    msgBox.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                else
                {
                    msgBox.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
                
                msgBox.ShowDialog();
            }
            catch
            {
                // If anything fails (e.g. Owner closing), show standalone centered on screen
                try
                {
                    msgBox.Owner = null;
                    msgBox.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    msgBox.ShowDialog();
                }
                catch { /* Give up if even this fails */ }
            }
            
            return msgBox.Result;
        }
        private void ApplyTheme()
        {
            if (IsDarkTheme)
            {
                // Dark Mode Colors
                var bg = new SolidColorBrush(Color.FromRgb(32, 32, 32));
                var border = new SolidColorBrush(Color.FromRgb(58, 58, 58));
                var text = Brushes.White;
                var textSec = new SolidColorBrush(Color.FromRgb(208, 208, 208));
                var barBg = new SolidColorBrush(Color.FromRgb(40, 40, 40));

                MainBorder.Background = bg;
                MainBorder.BorderBrush = border;
                
                ButtonContainer.Background = barBg;
                ButtonContainer.BorderBrush = border;
                
                TitleText.Foreground = text;
                MessageText.Foreground = textSec;

                // Button Resources
                this.Resources["AccentButtonBg"] = new SolidColorBrush(Color.FromRgb(96, 205, 255)); // #60CDFF
                this.Resources["AccentButtonHoverBg"] = new SolidColorBrush(Color.FromRgb(128, 224, 255)); // #80E0FF
                this.Resources["AccentButtonPressedBg"] = new SolidColorBrush(Color.FromRgb(64, 189, 255)); // #40BDFF
                this.Resources["AccentButtonFg"] = Brushes.Black;

                this.Resources["SecondaryButtonBg"] = Brushes.Transparent;
                this.Resources["SecondaryButtonFg"] = Brushes.White;
                this.Resources["SecondaryButtonBorder"] = new SolidColorBrush(Color.FromRgb(64, 64, 64)); // #404040
                this.Resources["SecondaryButtonHoverBg"] = new SolidColorBrush(Color.FromRgb(26, 26, 26)); // #1a1a1a
                this.Resources["SecondaryButtonPressedBg"] = new SolidColorBrush(Color.FromRgb(13, 13, 13)); // #0d0d0d
            }
            else
            {
                // Light Mode Colors
                var bg = Brushes.White;
                var border = new SolidColorBrush(Color.FromRgb(200, 200, 200));
                var text = Brushes.Black;
                var textSec = new SolidColorBrush(Color.FromRgb(80, 80, 80));
                var barBg = new SolidColorBrush(Color.FromRgb(243, 243, 243));

                MainBorder.Background = bg;
                MainBorder.BorderBrush = border;
                
                ButtonContainer.Background = barBg;
                ButtonContainer.BorderBrush = border;
                
                TitleText.Foreground = text;
                MessageText.Foreground = textSec;

                // Button Resources
                this.Resources["AccentButtonBg"] = new SolidColorBrush(Color.FromRgb(0, 103, 192)); // #0067C0 (Standard Blue)
                this.Resources["AccentButtonHoverBg"] = new SolidColorBrush(Color.FromRgb(24, 128, 216)); 
                this.Resources["AccentButtonPressedBg"] = new SolidColorBrush(Color.FromRgb(0, 90, 170));
                this.Resources["AccentButtonFg"] = Brushes.White; // White text on dark blue

                this.Resources["SecondaryButtonBg"] = Brushes.Transparent;
                this.Resources["SecondaryButtonFg"] = Brushes.Black;
                this.Resources["SecondaryButtonBorder"] = new SolidColorBrush(Color.FromRgb(200, 200, 200));
                this.Resources["SecondaryButtonHoverBg"] = new SolidColorBrush(Color.FromRgb(230, 230, 230)); // Light Grey
                this.Resources["SecondaryButtonHoverBg"] = new SolidColorBrush(Color.FromRgb(230, 230, 230)); // Light Grey
                this.Resources["SecondaryButtonPressedBg"] = new SolidColorBrush(Color.FromRgb(210, 210, 210));
            }
            
            // Re-apply icon to update its color based on the new theme
            SetIcon(_currentIcon);
        }
    }
}
