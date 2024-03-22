using HandyTools.ToolWindows;

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
