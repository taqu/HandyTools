namespace HandyTools.Commands
{
	[Command("daafe9b8-3dc3-4cb6-a2ce-3959212fdc7c", 0x0000)]
	internal sealed class CommandCompletion : BaseCommand<CommandCompletion>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			await VS.MessageBox.ShowWarningAsync("CommandCompletion", "Button clicked");
		}
	}
}
