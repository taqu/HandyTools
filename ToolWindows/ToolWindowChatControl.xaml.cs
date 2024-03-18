using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;

namespace HandyTools.ToolWindows
{
    /// <summary>
    /// Interaction logic for ToolWindowChatControl.
    /// </summary>
    public partial class ToolWindowChatControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindowChatControl"/> class.
        /// </summary>
        public ToolWindowChatControl()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        private async void SendButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(InputTextBox.Text))
            {
                return;
            }
            HandyToolsPackage package;
            if(!HandyToolsPackage.Package.TryGetTarget(out package))
            {
                return;
            }
            (Models.ModelBase model, SettingFile settingFile) = package.GetAIModel();
            if(null == model)
            {
                return;
            }
            try
            {
                string response = await model.CompletionAsync(InputTextBox.Text);
                OutputTextBox.Text = response;
            }catch (Exception ex)
            {
                OutputTextBox.Text = ex.Message;
            }
        }
    }
}