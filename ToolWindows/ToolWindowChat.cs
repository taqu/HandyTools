using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;

namespace HandyTools.ToolWindows
{
    public class ToolWindowChat : BaseToolWindow<ToolWindowChat>
    {
        public override string GetTitle(int toolWindowId) => "Chat Window";

        public override Type PaneType => typeof(Pane);

        public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
        {
            return new ToolWindowChatControl();
        }

        [Guid("94f9430a-5132-4450-af03-31764ba8ed9e")]
        internal class Pane : ToolWindowPane
        {
            public Pane()
            {
                BitmapImageMoniker = KnownMonikers.ToolWindow;
            }
        }
    }
}
