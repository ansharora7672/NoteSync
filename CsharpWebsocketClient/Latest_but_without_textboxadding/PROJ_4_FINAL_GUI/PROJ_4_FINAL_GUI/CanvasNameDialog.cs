using System.Windows;
using System.Windows.Controls.Primitives;


namespace PROJ_4_FINAL_GUI
{
    public partial class CanvasNameDialog : Window
    {
        public string CanvasName { get; private set; }

        public CanvasNameDialog()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            CanvasName = TextBoxName.Text.Trim();
            DialogResult = true; // DialogResult will be true when OK button is clicked
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false; // DialogResult will be false when Cancel button is clicked
        }
    }
}
