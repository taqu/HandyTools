using HandyTools.ToolWindows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandyTools.Commands
{
    	[Command(PackageGuids.HandyToolsString, PackageIds.CommandShowChatWindow)]
	internal sealed class CommandShowChatWindow : BaseCommand<CommandShowChatWindow>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			 await ToolWindowChat.ShowAsync();
		}
	}
}
