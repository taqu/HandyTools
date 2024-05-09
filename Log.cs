using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Threading;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HandyTools
{
    public static class Log
    {
        /// <summary>
        /// Print a message to the editor's output
        /// </summary>
        public static void Output(string message)
        {
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                HandyToolsPackage package;
                if (!HandyToolsPackage.TryGetPackage(out package) || !package.Options.OutputDebugLog)
                {
                    return;
                }

                DTE2 dte2 = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(DTE)) as DTE2;
                EnvDTE.OutputWindow outputWindow = dte2.ToolWindows.OutputWindow;
                if (null == outputWindow)
                {
                    return;
                }
                foreach (EnvDTE.OutputWindowPane window in outputWindow.OutputWindowPanes)
                {
                    window.OutputString(message);
                }
                Trace.Write(message);
            });
        }

		/// <summary>
		/// Print a message to the editor's output
		/// </summary>
		public static async Task OutputAsync(string message)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            HandyToolsPackage package;
            if (!HandyToolsPackage.TryGetPackage(out package) || !package.Options.OutputDebugLog)
            {
                return;
            }
            DTE2 dte2 = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(DTE)) as DTE2;
            EnvDTE.OutputWindow outputWindow = dte2.ToolWindows.OutputWindow;
            if(null == outputWindow) {
                return;
            }
            foreach(EnvDTE.OutputWindowPane window in outputWindow.OutputWindowPanes) {
                window.OutputString(message);
            }
            Trace.Write(message);
        }
    }
}
