using System.Windows;
using Cliente.ViewModels;

namespace Cliente.Views
{
    public partial class ReportPrototypeWindow : Window
    {
        public ReportPrototypeWindow()
        {
            InitializeComponent();
            DataContext = new ReportPrototypeViewModel();
        }
    }
}