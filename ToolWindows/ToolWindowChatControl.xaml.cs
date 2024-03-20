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
        public string Output
        {
            set
            {
				OutputTextBox.Text = value;
			}
        }
	}
}