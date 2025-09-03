using FileUploader.Client.ViewModel;
using System;
using System.Windows;

namespace FileUploader.Client.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IFileUploaderClientViewModel myViewModel;

        public MainWindow()
        {
            InitializeComponent();
            myViewModel = new FileUploaderClientViewModel();
            DataContext = myViewModel;

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                MessageBox.Show(e.ExceptionObject.ToString(), "Unhandled Exception");
            };
        }


    }
}
