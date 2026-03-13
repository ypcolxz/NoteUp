using ModernWpf; // <- required
using System.Windows;
using System;

namespace Noteup
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            System.Windows.MessageBox.Show("App.OnStartup called!", "Debug", MessageBoxButton.OK);
            
            base.OnStartup(e);
            
            // Global exception handlers
            this.DispatcherUnhandledException += (s, args) =>
            {
                System.Windows.MessageBox.Show($"Unhandled Exception:\n\n{args.Exception.Message}\n\nStack:\n{args.Exception.StackTrace}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
            
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                System.Windows.MessageBox.Show($"Fatal Error:\n\n{ex?.Message}\n\nStack:\n{ex?.StackTrace}", 
                    "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };
        }
    }
}
