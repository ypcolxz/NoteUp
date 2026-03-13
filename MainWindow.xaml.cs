using Microsoft.Win32;
using ModernWpf;
using ModernWpf.Controls;
using NoteUp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace Noteup
{
    public partial class MainWindow : Window
    {
        private Border? selectedTab = null;
        private string? currentNoteFile = null;
        private string notesFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NoteUpNotes");
        private bool isSidebarOpen = false;
        
        // Debug menu variables
        private int versionClickCount = 0;
        private DateTime lastVersionClick = DateTime.MinValue;
        private MessageBoxImage debugSelectedIcon = MessageBoxImage.Information;
        private MessageBoxButton debugSelectedButtons = MessageBoxButton.OK;

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                // Safely initialize ModernWpf resources if not present
                if (Application.Current.Resources.MergedDictionaries.Count == 0)
                {
                    try
                    {
                        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/ModernWpf;component/ThemeResources.xaml") });
                        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/ModernWpf;component/ControlsResources.xaml") });
                    }
                    catch { /* Ignore if already failed/loaded */ }
                }

                // Initialize theme based on toggle state (will happen when loaded)
                // ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark; // Removed hardcoded theme

                if (!Directory.Exists(notesFolder))
                    Directory.CreateDirectory(notesFolder);

                // Add keyboard shortcut for sidebar: Ctrl+B
                this.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key == Key.B && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        ToggleSidebar();
                        e.Handled = true;
                    }
                };
                
                // Auto-close sidebar when clicking outside
                this.PreviewMouseDown += MainWindow_PreviewMouseDown;

                this.Closing += MainWindow_Closing;
                this.Loaded += (s, e) => ApplyTheme();

                // Load settings after initialization
                LoadSettings();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error during startup:\n\n{ex.Message}\n\nStack:\n{ex.StackTrace}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }
        
        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Only check if sidebar is open
            if (isSidebarOpen)
            {
                // Get the clicked point
                Point clickPoint = e.GetPosition(this);
                
                // Get sidebar bounds
                Point sidebarPosition = Sidebar.TransformToAncestor(this).Transform(new Point(0, 0));
                Rect sidebarBounds = new Rect(sidebarPosition, new Size(Sidebar.ActualWidth, Sidebar.ActualHeight));
                
                // If click is outside sidebar, close it
                if (!sidebarBounds.Contains(clickPoint))
                {
                    ToggleSidebar();
                }
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Simple fade-in animation (Windows don't support RenderTransform)
                this.Opacity = 0;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                this.BeginAnimation(Window.OpacityProperty, fadeIn);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error in MainWindow_Loaded:\n\n{ex.Message}\n\nStack:\n{ex.StackTrace}",
                    "Loaded Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            // Smooth animation on state changes
            if (WindowState == WindowState.Minimized)
            {
                // Minimize animation - scale down and fade out
                var scaleAnim = new DoubleAnimation(1.0, 0.9, TimeSpan.FromMilliseconds(150))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                
                if (this.RenderTransform is ScaleTransform scale)
                {
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                }
                this.BeginAnimation(Window.OpacityProperty, fadeOut);
            }
            else if (WindowState == WindowState.Normal || WindowState == WindowState.Maximized)
            {
                // Restore/maximize - enlarge and fade in
                var scaleAnim = new DoubleAnimation(0.95, 1.0, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var fadeIn = new DoubleAnimation(this.Opacity, 1, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                
                if (this.RenderTransform is ScaleTransform scale)
                {
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                }
                this.BeginAnimation(Window.OpacityProperty, fadeIn);
            }
        }

        // MainWindow.xaml.cs
        public void SetTrafficLights(bool enabled)
        {
            if (enabled)
            {
                MinBtn.Content = ""; MinBtn.Background = Brushes.Green;
                MaxBtn.Content = ""; MaxBtn.Background = Brushes.Yellow;
                CloseBtn.Content = ""; CloseBtn.Background = Brushes.Red;
            }
            else
            {
                MinBtn.Content = ""; MinBtn.Background = Brushes.Transparent;
                MaxBtn.Content = ""; MaxBtn.Background = Brushes.Transparent;
                CloseBtn.Content = ""; CloseBtn.Background = Brushes.Transparent;
            }
        }


        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var dirtyTabs = TabBar.Children
                .OfType<Border>()
                .Where(tab => tab != AddTabButton.Parent && (tab.Child as Grid)?.Children.OfType<TextBlock>().FirstOrDefault()?.Text.EndsWith("*") == true)
                .Select(tab => (tab.Child as Grid).Children.OfType<TextBlock>().FirstOrDefault()?.Text.Replace("*", "").Trim())
                .Where(name => name != null)
                .ToList();

            if (dirtyTabs.Count > 0)
            {
                string message = "You have (an) unsaved tab(s):\n\n" + string.Join("\n", dirtyTabs) + "\n\nAre you sure you want to close?";
                var result = CustomMessageBox.Show(message, "Unsaved Tabs", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            // Fade out animation before closing
            // Fade out animation before closing
            if (!e.Cancel)
            {
                // Save settings before closing
                SaveSettings();
                
                e.Cancel = true; // Cancel the close temporarily
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                fadeOut.Completed += (s, args) =>
                {
                    // Use Dispatcher to ensure we are out of the immediate event cycle
                    Dispatcher.BeginInvoke(new Action(() => 
                    {
                        // Force exit to avoid "VerifyNotClosing" issues with WPF window state
                        Environment.Exit(0);
                    }), System.Windows.Threading.DispatcherPriority.Background);
                };
                this.BeginAnimation(Window.OpacityProperty, fadeOut);
            }
        }

        private void AddTabButton_Click(object sender, RoutedEventArgs e)
        {
            var border = new Border
            {
                // Background set by UpdateTabSelection later
                Background = Brushes.Transparent, 
                CornerRadius = new CornerRadius(6, 6, 0, 0),
                Margin = new Thickness(0, 0, 6, 0),
                Width = 180,
                Height = 33,
                Padding = new Thickness(6, 0, 6, 0),
                SnapsToDevicePixels = true
            };

            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var text = new TextBlock
            {
                Text = $"Note {Math.Max(1, TabBar.Children.Count)}",
                Foreground = isDarkTheme ? Brushes.White : Brushes.Black,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(4, 0, 8, 0)
            };
            Grid.SetColumn(text, 0);

            var closeBtn = new Button
            {
                Content = "",
                FontSize = 12,
                Style = (Style)FindResource("TabIconButton"), // This style might need Foreground update or override
                Foreground = isDarkTheme ? Brushes.White : Brushes.Black,
                ToolTip = "Close tab",
                VerticalAlignment = VerticalAlignment.Center,
                Width = 26,
                Height = 26
            };
            Grid.SetColumn(closeBtn, 1);

            grid.Children.Add(text);
            grid.Children.Add(closeBtn);
            border.Child = grid;

            var container = new Grid();
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lineScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var lineNumbers = new TextBlock
            {
                Foreground = isDarkTheme ? Brushes.Gray : Brushes.DarkGray,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(4, 2, 6, 0),
                FontFamily = new FontFamily("Consolas"),
                Text = "1\n" // Initialize with line 1
            };
            lineScroll.Content = lineNumbers;
            Grid.SetColumn(lineScroll, 0);

            var noteBox = new TextBox
            {
                Background = isDarkTheme ? new SolidColorBrush(Color.FromRgb(28, 28, 28)) : Brushes.White,
                Foreground = isDarkTheme ? Brushes.White : Brushes.Black,
                CaretBrush = isDarkTheme ? Brushes.White : Brushes.Black,
                FontSize = 14,
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0),
                FontFamily = new FontFamily("Cascadia Code")
            };
            Grid.SetColumn(noteBox, 1);

            noteBox.Tag = new TabData { Zoom = 14, FilePath = null };

            noteBox.Loaded += (s, ev) =>
            {
                var sv = GetScrollViewer(noteBox);
                if (sv != null)
                    sv.ScrollChanged += (sender, args) => lineScroll.ScrollToVerticalOffset(args.VerticalOffset);
            };

            noteBox.TextChanged += (s, ev) =>
            {
                int lines = noteBox.LineCount;
                if (lines == 0) lines = 1;
                string nums = "";
                for (int i = 1; i <= lines; i++) nums += i + "\n";
                lineNumbers.Text = nums;

                if (!text.Text.EndsWith("*")) text.Text += "*";
            };

            noteBox.PreviewMouseRightButtonDown += (s, args) =>
            {
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    args.Handled = true;
                    // Not used anymore - sidebar is integrated
                }
            };

            noteBox.PreviewMouseWheel += (s, args) =>
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    var tabData = noteBox.Tag as TabData;
                    tabData.Zoom = Math.Max(6, Math.Min(48, tabData.Zoom + (args.Delta > 0 ? 1 : -1)));
                    noteBox.FontSize = tabData.Zoom;
                    lineNumbers.FontSize = tabData.Zoom;
                    args.Handled = true;
                }
            };

            container.Children.Add(lineScroll);
            container.Children.Add(noteBox);

            border.MouseLeftButtonDown += (s, args) =>
            {
                selectedTab = border;
                
                // Remove any existing tab content (but not the TextBlock)
                var itemsToRemove = TabContentArea.Children.OfType<Grid>().ToList();
                foreach (var item in itemsToRemove)
                {
                    TabContentArea.Children.Remove(item);
                }
                
                // Hide the "no tabs open" text
                NoTabsText.Visibility = Visibility.Collapsed;
                
                // Add the tab content
                TabContentArea.Children.Add(container);
                AnimateTabSelection(border);
                UpdateTabSelection();
            };
            
            // Double-click to rename tab using PreviewMouseDown to detect double-clicks
            border.PreviewMouseDown += (s, args) =>
            {
                if (args.ClickCount == 2)
                {
                    Tab_MouseDoubleClick(border, args);
                    args.Handled = true;
                }
            };

            closeBtn.Click += (s, args) =>
            {
                args.Handled = true;
                CloseTabWithAnimation(border);
            };

            TabBar.Children.Insert(Math.Max(0, TabBar.Children.Count - 1), border);

            var translate = new TranslateTransform { Y = 28 };
            border.RenderTransform = translate;
            border.RenderTransformOrigin = new Point(0.5, 0.5);
            border.Opacity = 0;
            
            var slideAnim = new DoubleAnimation(28, 0, TimeSpan.FromMilliseconds(450))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            translate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            border.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            selectedTab = border;
            TabContentArea.Children.Clear();
            TabContentArea.Children.Add(container);
            AnimateTabSelection(border);
        }

        private ScrollViewer GetScrollViewer(DependencyObject dep)
        {
            if (dep is ScrollViewer sv) return sv;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(dep); i++)
            {
                var child = VisualTreeHelper.GetChild(dep, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        public void DeleteSelectedTab()
        {
            if (selectedTab == null) return;

            var headerGrid = selectedTab.Child as Grid;
            var titleBlock = headerGrid?.Children.OfType<TextBlock>().FirstOrDefault();
            if (titleBlock == null) return;

            var contentGrid = TabContentArea.Children.OfType<Grid>().FirstOrDefault();
            var noteBox = contentGrid?.Children.OfType<TextBox>().FirstOrDefault();
            var tabData = noteBox?.Tag as TabData;

            var tabName = titleBlock.Text.Replace("*", "").Trim();

            var result = CustomMessageBox.Show($"Are you sure you want to delete '{tabName}'?",
                                         "Delete Note", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            if (tabData != null && !string.IsNullOrEmpty(tabData.FilePath) && File.Exists(tabData.FilePath))
                File.Delete(tabData.FilePath);

            TabBar.Children.Remove(selectedTab);
            TabContentArea.Children.Clear();
            selectedTab = null;
            UpdateTabSelection();

            if (TabBar.Children.Count <= 1)
            {
                TabContentArea.Children.Add(new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = Brushes.Gray,
                    FontSize = 16,
                    FontStyle = FontStyles.Italic,
                    Text = "no tabs open"
                });
            }
        }

        public void SaveSelectedNote()
        {
            if (selectedTab == null) return;

            var contentGrid = TabContentArea.Children.OfType<Grid>().FirstOrDefault();
            if (contentGrid == null) return;

            var textBox = contentGrid.Children.OfType<TextBox>().FirstOrDefault();
            if (textBox == null) return;

            var headerGrid = selectedTab.Child as Grid;
            var titleBlock = headerGrid?.Children.OfType<TextBlock>().FirstOrDefault();
            if (titleBlock == null) return;

            string defaultName = titleBlock.Text.Replace("*", "").Trim();

            var saveDialog = new SaveFileDialog
            {
                FileName = defaultName,
                DefaultExt = ".txt",
                Filter = "Text documents (.txt)|*.txt"
            };

            if (saveDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveDialog.FileName, textBox.Text);

                titleBlock.Text = Path.GetFileNameWithoutExtension(saveDialog.FileName);

                var tabData = textBox.Tag as TabData;
                tabData.FilePath = saveDialog.FileName;

                CustomMessageBox.Show($"Saved '{titleBlock.Text}' to {saveDialog.FileName}", "NoteUp", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void UpdateTabSelection()
        {
            // Count actual tabs (excluding the Add button)
            int tabCount = TabBar.Children.OfType<Border>().Count();
            
            if (tabCount == 0)
            {
                // Show "no tabs open" text with delayed fade-in
                NoTabsText.Opacity = 0;
                NoTabsText.Visibility = Visibility.Visible;
                
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600))
                {
                    BeginTime = TimeSpan.FromSeconds(2), // 2 second delay
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                NoTabsText.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
            else
            {
                // Hide immediately when tabs exist
                NoTabsText.BeginAnimation(UIElement.OpacityProperty, null); // Stop any animation
                NoTabsText.Visibility = Visibility.Collapsed;
            }
            
            // Update tab backgrounds
            var activeBg = isDarkTheme ? new SolidColorBrush(Color.FromRgb(42, 42, 42)) : Brushes.White;
            var inactiveBg = isDarkTheme ? new SolidColorBrush(Color.FromRgb(30, 30, 30)) : new SolidColorBrush(Color.FromRgb(229, 229, 229));

            foreach (var child in TabBar.Children)
            {
                if (child is Border tab && tab != AddTabButton.Parent)
                {
                    tab.Background = tab == selectedTab ? activeBg : inactiveBg;
                    
                    // Also ensure text color is correct for existing tabs
                    if (tab.Child is Grid grid)
                    {
                         foreach (var textBlock in grid.Children.OfType<TextBlock>())
                            textBlock.Foreground = isDarkTheme ? Brushes.White : Brushes.Black;
                         foreach (var btn in grid.Children.OfType<Button>())
                            btn.Foreground = isDarkTheme ? Brushes.White : Brushes.Black;
                    }
                }
            }
        }

        private void AnimateTabSelection(Border tab)
        {
            UpdateTabSelection();
            
            var activeColor = isDarkTheme ? Color.FromRgb(42, 42, 42) : Colors.White;
            var pulseColor = isDarkTheme ? Color.FromRgb(70, 70, 70) : Color.FromRgb(240, 240, 240);
            
            var brush = new SolidColorBrush(activeColor);
            tab.Background = brush;
            var pulseAnim = new ColorAnimation(activeColor, pulseColor,
                TimeSpan.FromMilliseconds(150))
            {
                AutoReverse = true,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            brush.BeginAnimation(SolidColorBrush.ColorProperty, pulseAnim);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
            else
                DragMove();
        }

        private void MinBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void MaxBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            // Toggle sidebar with Shift+S or Ctrl+B
            if ((Keyboard.Modifiers == ModifierKeys.Shift && e.Key == Key.S) ||
                (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.B))
            {
                ToggleSidebar();
                e.Handled = true;
            }
        }

        private void ToggleSidebar()
        {
            // Don't allow sidebar if no tabs are open
            int tabCount = TabBar.Children.OfType<Border>().Count();
            if (tabCount == 0 && !isSidebarOpen)
            {
                return; // Don't open sidebar when no tabs
            }
            
            isSidebarOpen = !isSidebarOpen;
            double targetWidth = isSidebarOpen ? 60 : 0;
            
            // Animate column width to push content
            AnimateGridColumnWidth(SidebarColumn, targetWidth, TimeSpan.FromMilliseconds(250));
            
            // Animate blur effect
            AnimateBlurEffect(isSidebarOpen);
        }
        
        private void AnimateGridColumnWidth(ColumnDefinition column, double targetWidth, TimeSpan duration)
        {
            // Simple approach: use a timer-based update
            var startWidth = column.Width.Value;
            var startTime = DateTime.Now;
            
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            
            timer.Tick += (s, e) =>
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                var progress = Math.Min(elapsed / duration.TotalMilliseconds, 1.0);
                
                // Apply easing
                var easedProgress = EaseOut(progress);
                var currentWidth = startWidth + (targetWidth - startWidth) * easedProgress;
                
                column.Width = new GridLength(currentWidth);
                
                if (progress >= 1.0)
                {
                    timer.Stop();
                    column.Width = new GridLength(targetWidth);
                }
            };
            
            timer.Start();
        }
        
        private double EaseOut(double t)
        {
            // Cubic ease out
            return 1 - Math.Pow(1 - t, 3);
        }
        
        private void AnimateBlurEffect(bool shouldBlur)
        {
            var blurEffect = TabContentArea.Effect as BlurEffect;
            
            if (shouldBlur)
            {
                if (blurEffect == null)
                {
                    blurEffect = new BlurEffect { Radius = 0 };
                    TabContentArea.Effect = blurEffect;
                }
                
                var blurAnimation = new DoubleAnimation(0, 10, TimeSpan.FromMilliseconds(250))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                blurEffect.BeginAnimation(BlurEffect.RadiusProperty, blurAnimation);
            }
            else
            {
                if (blurEffect != null)
                {
                    var unblurAnimation = new DoubleAnimation(blurEffect.Radius, 0, TimeSpan.FromMilliseconds(250))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    unblurAnimation.Completed += (s, e) => TabContentArea.Effect = null;
                    blurEffect.BeginAnimation(BlurEffect.RadiusProperty, unblurAnimation);
                }
            }
        }

        private void SidebarSave_Click(object sender, RoutedEventArgs e)
        {
            SaveSelectedNote();
            ToggleSidebar();
        }

        private void SidebarDelete_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedTab();
            ToggleSidebar();
        }

        private void SidebarSettings_Click(object sender, RoutedEventArgs e)
        {
            // Show integrated settings page
            SettingsPage.Visibility = Visibility.Visible;
            
            // Slide-in animation
            var slideAnim = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            SettingsPage.RenderTransform = new TranslateTransform(20, 0);
            ((TranslateTransform)SettingsPage.RenderTransform).BeginAnimation(TranslateTransform.XProperty, slideAnim);
            SettingsPage.BeginAnimation(OpacityProperty, fadeAnim);
            
            ToggleSidebar();
        }

        private bool isDarkTheme = true;

        private void ThemeToggle_Checked(object sender, RoutedEventArgs e)
        {
            isDarkTheme = true;
            ApplyTheme();
            SaveSettings();
        }

        private void ThemeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            isDarkTheme = false;
            ApplyTheme();
            SaveSettings();
        }

        private void ApplyTheme()
        {
            // Sync Message Box Theme
            CustomMessageBox.IsDarkTheme = isDarkTheme;

            // Update ThemeManager if possible
            try 
            { 
                ThemeManager.Current.ApplicationTheme = isDarkTheme ? ApplicationTheme.Dark : ApplicationTheme.Light; 
            } 
            catch { }

            // Define Colors
            // Dark Mode
            var darkWindowBg = new SolidColorBrush(Color.FromRgb(28, 28, 28));
            var darkSidebarBg = new SolidColorBrush(Color.FromRgb(37, 37, 37));
            var darkTabAreaBg = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            var darkTitleBarBg = new SolidColorBrush(Color.FromRgb(34, 34, 34));
            var darkMenuBg = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            
            // Light Mode (Refined for distinct shades)
            var lightWindowBg = Brushes.White;
            var lightSidebarBg = new SolidColorBrush(Color.FromRgb(243, 243, 243)); // Lighter grey for sidebar
            var lightTabAreaBg = new SolidColorBrush(Color.FromRgb(232, 232, 232)); // Distinct tab well
            var lightTitleBarBg = new SolidColorBrush(Color.FromRgb(220, 220, 220)); // Distinct titlebar
            var lightMenuBg = new SolidColorBrush(Color.FromRgb(232, 232, 232));

            var windowBg = isDarkTheme ? darkWindowBg : lightWindowBg;
            var sidebarBg = isDarkTheme ? darkSidebarBg : lightSidebarBg;
            var tabAreaBg = isDarkTheme ? darkTabAreaBg : lightTabAreaBg;
            var titleBarBg = isDarkTheme ? darkTitleBarBg : lightTitleBarBg;
            var menuBg = isDarkTheme ? darkMenuBg : lightMenuBg;
            
            var textPrimary = isDarkTheme ? Brushes.White : Brushes.Black;
            var textSecondary = isDarkTheme ? Brushes.Gray : Brushes.DarkGray;
            
            // Apply to Main Areas
             // Apply to Main Areas
            if (this.Content is Border rootBorder)
            {
                rootBorder.Background = windowBg;
                // Soften window border in light mode
                rootBorder.BorderBrush = isDarkTheme ? new SolidColorBrush(Color.FromRgb(44, 44, 44)) : new SolidColorBrush(Color.FromRgb(200, 200, 200));
            }

            if (SettingsPage != null) SettingsPage.Background = windowBg;
            if (Sidebar != null) 
            {
                Sidebar.Background = sidebarBg;
                // Soften Sidebar border
                Sidebar.BorderBrush = isDarkTheme ? new SolidColorBrush(Color.FromRgb(44, 44, 44)) : new SolidColorBrush(Color.FromRgb(220, 220, 220));
            }
            if (TabScrollViewer != null) TabScrollViewer.Background = tabAreaBg;
            
            // Update Menu Bar (Parent Border)
            var menu = FindVisualChildren<Menu>(this).FirstOrDefault();
            if (menu != null)
            {
                if (menu.Parent is Border menuBorder)
                {
                    menuBorder.Background = menuBg;
                     // Soften Menu border
                    menuBorder.BorderBrush = isDarkTheme ? new SolidColorBrush(Color.FromRgb(44, 44, 44)) : new SolidColorBrush(Color.FromRgb(220, 220, 220));
                }
                
                foreach (var item in menu.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                         menuItem.Foreground = textPrimary;
                         foreach (var subItem in menuItem.Items)
                         {
                             if (subItem is MenuItem subMenuItem) subMenuItem.Foreground = textPrimary;
                         }
                    }
                }
            }

            // Update TitleBar
            if (TitleBar != null)
            {
                if (TitleBar.Parent is Border titleBorder) titleBorder.Background = titleBarBg;
                foreach (var textBlock in FindVisualChildren<TextBlock>(TitleBar))
                {
                    textBlock.Foreground = textPrimary;
                }
            }

            // Update Tab Content (Active Tab)
            foreach (var grid in TabContentArea.Children.OfType<Grid>())
            {
                ApplyThemeToTabContent(grid);
            }

            // Update Tabs (Headers) & AddTabButton
            UpdateTabSelection(); 
            if (AddTabButton != null)
            {
                 AddTabButton.Foreground = textPrimary;
            }

            foreach (var border in TabBar.Children.OfType<Border>())
            {
                if (border.Child is Grid grid)
                {
                    foreach (var textBlock in grid.Children.OfType<TextBlock>())
                    {
                        textBlock.Foreground = textPrimary;
                    }
                    foreach (var btn in grid.Children.OfType<Button>())
                    {
                        btn.Foreground = textPrimary;
                    }
                }
            }
            
            // Update Settings Text
             if (SettingsPage != null)
            {
                foreach (var textBlock in FindVisualChildren<TextBlock>(SettingsPage))
                {
                    if (textBlock.Foreground != Brushes.Red && textBlock.Text != "This application is currently in a highly unstable state. Do not use as your main note-taking app.")
                    {
                         if (textBlock.Text.Contains("placeholder")) textBlock.Foreground = textSecondary;
                         else textBlock.Foreground = textPrimary;
                    }
                }
                
                // Version text
                if (VersionText != null) VersionText.Foreground = textSecondary;
                
                // Toggle Switch Text
                if (ThemeToggle != null) ThemeToggle.Foreground = textPrimary;
            }
            
             // Update Window Buttons (Min/Max/Close)
             if (MinBtn != null) MinBtn.Foreground = textPrimary;
             if (MaxBtn != null) MaxBtn.Foreground = textPrimary;
             if (CloseBtn != null) CloseBtn.Foreground = textPrimary;
             
             // Sidebar Button Resources
             if (isDarkTheme)
             {
                 this.Resources["SidebarButtonBg"] = new SolidColorBrush(Color.FromRgb(51, 51, 51)); // #333
                 this.Resources["SidebarButtonFg"] = Brushes.White;
                 this.Resources["SidebarButtonHoverBg"] = new SolidColorBrush(Color.FromRgb(53, 126, 199)); // #357EC7
                 this.Resources["SidebarButtonPressBg"] = new SolidColorBrush(Color.FromRgb(42, 108, 167)); // #2a6ca7
                 
                 // Neutral Button Resources (for Tab Add Button)
                 this.Resources["NeutralHoverBg"] = new SolidColorBrush(Color.FromRgb(64, 64, 64)); 
                 this.Resources["NeutralPressedBg"] = new SolidColorBrush(Color.FromRgb(50, 50, 50));
             }
             else
             {
                 this.Resources["SidebarButtonBg"] = new SolidColorBrush(Color.FromRgb(235, 235, 235)); // #EBEBEB
                 this.Resources["SidebarButtonFg"] = Brushes.Black;
                 this.Resources["SidebarButtonHoverBg"] = new SolidColorBrush(Color.FromRgb(215, 215, 215)); // Darker on hover
                 this.Resources["SidebarButtonPressBg"] = new SolidColorBrush(Color.FromRgb(200, 200, 200));
                 
                 // Neutral Button Resources (for Tab Add Button)
                 this.Resources["NeutralHoverBg"] = new SolidColorBrush(Color.FromRgb(224, 224, 224)); 
                 this.Resources["NeutralPressedBg"] = new SolidColorBrush(Color.FromRgb(208, 208, 208));
             }
        }

        private void ApplyThemeToTabContent(Grid container)
        {
            var noteBox = container.Children.OfType<TextBox>().FirstOrDefault();
            if (noteBox != null)
            {
                noteBox.Background = isDarkTheme ? new SolidColorBrush(Color.FromRgb(28, 28, 28)) : Brushes.White;
                noteBox.Foreground = isDarkTheme ? Brushes.White : Brushes.Black;
                noteBox.CaretBrush = isDarkTheme ? Brushes.White : Brushes.Black;
            }
            
            var sv = container.Children.OfType<ScrollViewer>().FirstOrDefault();
            if (sv != null && sv.Content is TextBlock lineNums)
            {
                 lineNums.Foreground = isDarkTheme ? Brushes.Gray : Brushes.DarkGray;
            }
        }
        
        // Helper to find children
        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        private void BackToNotes_Click(object sender, RoutedEventArgs e)
        {
            // Hide settings page with animation
            var slideAnim = new DoubleAnimation(0, 20, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var fadeAnim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            
            fadeAnim.Completed += (s, args) =>
            {
                SettingsPage.Visibility = Visibility.Collapsed;
            };
            
            if (SettingsPage.RenderTransform is TranslateTransform trans)
            {
                trans.BeginAnimation(TranslateTransform.XProperty, slideAnim);
            }
            SettingsPage.BeginAnimation(OpacityProperty, fadeAnim);
        }
        
        private void CloseTabWithAnimation(Border tab)
        {
            var slideDown = new DoubleAnimation(0, 40, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            
            tab.RenderTransform = new TranslateTransform(0, 0);
            
            fadeOut.Completed += (s, args) =>
            {
                TabBar.Children.Remove(tab);
                if (selectedTab == tab)
                {
                    selectedTab = null;
                    
                    // Remove only the Grid content (tab containers), never the TextBlock
                    var itemsToRemove = TabContentArea.Children.OfType<Grid>().ToList();
                    
                    foreach (var item in itemsToRemove)
                    {
                        TabContentArea.Children.Remove(item);
                    }
                    
                    // Ensure NoTabsText is in TabContentArea
                    if (!TabContentArea.Children.Contains(NoTabsText))
                    {
                        TabContentArea.Children.Add(NoTabsText);
                    }
                }
                UpdateTabSelection();
            };
            
            ((TranslateTransform)tab.RenderTransform).BeginAnimation(TranslateTransform.YProperty, slideDown);
            tab.BeginAnimation(OpacityProperty, fadeOut);
        }


        
        private void Tab_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border tab) return;

            var tabGrid = tab.Child as Grid;
            var tabLabel = tabGrid?.Children.OfType<TextBlock>().FirstOrDefault();
            if (tabLabel == null) return;

            // Theme Colors
            var popupBg = isDarkTheme ? new SolidColorBrush(Color.FromRgb(40, 40, 40)) : new SolidColorBrush(Color.FromRgb(240, 240, 240));
            var popupBorderBrush = isDarkTheme ? new SolidColorBrush(Color.FromRgb(60, 60, 60)) : new SolidColorBrush(Color.FromRgb(200, 200, 200));
            var textBoxBg = isDarkTheme ? new SolidColorBrush(Color.FromRgb(30, 30, 30)) : Brushes.White;
            var textBoxFg = isDarkTheme ? Brushes.White : Brushes.Black;
            var textBoxBorder = isDarkTheme ? new SolidColorBrush(Color.FromRgb(70, 70, 70)) : new SolidColorBrush(Color.FromRgb(210, 210, 210));
            var btnBg = isDarkTheme ? new SolidColorBrush(Color.FromRgb(50, 50, 50)) : new SolidColorBrush(Color.FromRgb(225, 225, 225));
            var btnFg = isDarkTheme ? Brushes.White : Brushes.Black;

            // Create popup for renaming
            var popup = new Popup
            {
                PlacementTarget = tab,
                Placement = PlacementMode.Bottom,
                StaysOpen = true,  // Keep open so double-click works
                AllowsTransparency = true
            };

            var popupBorder = new Border
            {
                Background = popupBg,
                BorderBrush = popupBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10)
            };

            var stack = new StackPanel();

            var renameBox = new TextBox
            {
                Text = tabLabel.Text,
                MinWidth = 150,
                Background = textBoxBg,
                Foreground = textBoxFg,
                BorderBrush = textBoxBorder,
                Padding = new Thickness(6),
                Margin = new Thickness(0, 0, 0, 8),
                CaretBrush = textBoxFg
            };

            var emojiPanel = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };

            // Don't close popup when clicking in the textbox or buttons
            renameBox.PreviewMouseDown += (s, args) => args.Handled = true;
            
            // Close popup when clicking outside
            popup.LostFocus += (s, args) =>
            {
                // Small delay to allow button clicks to process
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!renameBox.IsKeyboardFocusWithin && !emojiPanel.IsMouseOver)
                        popup.IsOpen = false;
                }), System.Windows.Threading.DispatcherPriority.Background);
            };

            var emojis = new[] { "📝", "💡", "⭐", "🔥", "📌", "✅", "❤️", "🎯" };

            foreach (var emoji in emojis)
            {
                var btn = new Button
                {
                    Content = emoji,
                    Width = 32,
                    Height = 32,
                    Margin = new Thickness(2),
                    Background = btnBg,
                    Foreground = btnFg,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };

                btn.Click += (s, args) =>
                {
                    int caretIndex = renameBox.CaretIndex;
                    renameBox.Text = renameBox.Text.Insert(caretIndex, emoji);
                    renameBox.CaretIndex = caretIndex + emoji.Length;
                    renameBox.Focus();
                    args.Handled = true;  // Prevent popup from closing
                };

                emojiPanel.Children.Add(btn);
            }

            stack.Children.Add(renameBox);
            stack.Children.Add(emojiPanel);
            popupBorder.Child = stack;
            popup.Child = popupBorder;

            renameBox.KeyDown += (s, args) =>
            {
                if (args.Key == Key.Enter)
                {
                    tabLabel.Text = renameBox.Text;
                    popup.IsOpen = false;
                }
                else if (args.Key == Key.Escape)
                {
                    popup.IsOpen = false;
                }
            };

            popup.IsOpen = true;
            renameBox.Focus();
            renameBox.SelectAll();
        }
        
        // Debug Menu Functions
        private void VersionText_Click(object sender, MouseButtonEventArgs e)
        {
            var now = DateTime.Now;
            
            // Reset count if more than 2 seconds elapsed
            if ((now - lastVersionClick).TotalSeconds > 2)
            {
                versionClickCount = 0;
            }
            
            versionClickCount++;
            lastVersionClick = now;
            
            if (versionClickCount >= 5)
            {
                DebugPanel.Visibility = DebugPanel.Visibility == Visibility.Visible 
                    ? Visibility.Collapsed 
                    : Visibility.Visible;
                
                // Show confirmation when debug menu is activated
                if (DebugPanel.Visibility == Visibility.Visible)
                {
                    CustomMessageBox.Show(
                        "debug menu is now on",
                        "debug menu on",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                
                versionClickCount = 0;
            }
        }
        
        private void DebugIcon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string iconType)
            {
                debugSelectedIcon = iconType switch
                {
                    "Information" => MessageBoxImage.Information,
                    "Warning" => MessageBoxImage.Warning,
                    "Error" => MessageBoxImage.Error,
                    _ => MessageBoxImage.None
                };
            }
        }
        
        private void DebugButtons_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string buttonType)
            {
                debugSelectedButtons = buttonType switch
                {
                    "OK" => MessageBoxButton.OK,
                    "OKCancel" => MessageBoxButton.OKCancel,
                    "YesNo" => MessageBoxButton.YesNo,
                    "YesNoCancel" => MessageBoxButton.YesNoCancel,
                    _ => MessageBoxButton.OK
                };
            }
        }
        
        private void ShowDebugMessageBox_Click(object sender, RoutedEventArgs e)
        {
            var result = CustomMessageBox.Show(
                DebugMessage.Text,
                DebugTitle.Text,
                debugSelectedButtons,
                debugSelectedIcon
            );
        }

        #region Persistence
        private void LoadSettings()
        {
            try
            {
                var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NoteUp", "settings.txt");
                if (File.Exists(settingsPath))
                {
                    var content = File.ReadAllText(settingsPath);
                    if (content.Contains("Theme=Light"))
                    {
                        // Update UI, this will trigger Unchecked event -> ApplyTheme -> SaveSettings
                        ThemeToggle.IsChecked = false;
                    }
                    else
                    {
                        // Default is Dark (Checked=True in XAML). 
                        // If we are here, it means either Theme=Dark or invalid. Keep default.
                        if (ThemeToggle.IsChecked == false) ThemeToggle.IsChecked = true;
                    }
                }
            }
            catch { /* Ignore errors during load */ }
        }

        private void SaveSettings()
        {
            try
            {
                var settingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NoteUp");
                if (!Directory.Exists(settingsDir)) Directory.CreateDirectory(settingsDir);

                var settingsPath = Path.Combine(settingsDir, "settings.txt");
                File.WriteAllText(settingsPath, $"Theme={(isDarkTheme ? "Dark" : "Light")}");
            }
            catch { /* Ignore errors during save */ }
        }
        #endregion
    }

    public class TabData
    {
        public double Zoom { get; set; }
        public string FilePath { get; set; }
    }
}
