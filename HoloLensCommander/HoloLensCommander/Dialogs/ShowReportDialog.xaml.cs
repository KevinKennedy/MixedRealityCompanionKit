using Windows.UI.Xaml.Controls;

namespace HoloLensCommander
{
    public sealed partial class ShowReportDialog : ContentDialog
    {
        public ShowReportDialog(string title, string contents)
        {
            this.InitializeComponent();
            this.Title = title;
            this.reportDisplay.Text = contents;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
