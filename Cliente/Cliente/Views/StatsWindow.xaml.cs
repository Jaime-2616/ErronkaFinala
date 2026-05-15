using System;
using System.Windows;
using Microsoft.Win32;
using Cliente.Services;
using Cliente.ViewModels;

namespace Cliente.Views
{
    public partial class StatsWindow : Window
    {
        private readonly StatsReportViewModel _vm = new();
        private readonly string _username;

        public StatsWindow(string username)
        {
            InitializeComponent();
            _username = username;
            DataContext = _vm;
            Loaded += async (_, _) => await _vm.LoadAsync(username);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = $"estadisticas_{_username}_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
            };

            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                StatsReportPdfExporter.Export(_vm, dialog.FileName);
                MessageBox.Show("PDF guardado correctamente.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo generar el PDF: " + ex.Message);
            }
        }
    }
}