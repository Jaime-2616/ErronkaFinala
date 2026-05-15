using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;

namespace Cliente
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show("Excepción no controlada: " + e.Exception.Message);
            e.Handled = true;
        }
    }
}
