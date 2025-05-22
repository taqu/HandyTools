using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandyTools.Commands
{
    internal class CommandBaseCompletion<T> : BaseCommand<T> where T : class, new()
    {
        protected DocumentView? documentView_;
        protected CompletionCommandHandler? completionHandler_;

        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                try
                {
                    documentView_ = await VS.Documents.GetActiveDocumentViewAsync();
                    if(null == documentView_ || null == documentView_.TextView)
                    {
                        return false;
                    }
                    var properties = documentView_.TextView.Properties;
                    bool exists = properties.ContainsProperty(typeof(CompletionCommandHandler));
                    if (properties.TryGetProperty< CompletionCommandHandler>(typeof(CompletionCommandHandler), out completionHandler_))
                    {
                        return true;
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    await Log.OutputAsync("Exception: " + ex);
                    return false;
                }
            });
        }
    }
}
