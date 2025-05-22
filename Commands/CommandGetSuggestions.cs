using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandyTools.Commands
{
    [Command(PackageIds.CommandGetSuggestions)]
    internal sealed class CommandGetSuggestions : CommandBaseCompletion<CommandGetSuggestions>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
        }
    }
}
