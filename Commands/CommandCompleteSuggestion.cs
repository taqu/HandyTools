using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandyTools.Commands
{
    [Command(PackageIds.CommandCompleteSuggestion)]
    internal sealed class CommandCompleteSuggestion : CommandBaseCompletion<CommandCompleteSuggestion>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            try
            {
                if (null == completionHandler_)
                {
                    return;
                }
                completionHandler_.CompleteSuggestion();
            }
            catch (Exception ex)
            {
                await Log.OutputAsync("Exception: " + ex);
            }
        }
    }
}
