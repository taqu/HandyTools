using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandyTools.Commands
{
    [Command(PackageIds.CommandShowNextSuggestion)]
    internal sealed class CommandShowNextSuggestion : CommandBaseCompletion<CommandShowNextSuggestion>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            try
            {
                if(null == completionHandler_)
                {
                    return;
                }
                await completionHandler_.ShowNextSuggestionAsync();
            }
            catch (Exception ex)
            {
                await Log.OutputAsync("Exception: " + ex);
            }
        }
    }
}
