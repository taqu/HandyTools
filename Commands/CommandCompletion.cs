using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandyTools.Commands
{
	[Command(PackageGuids.HandyToolsString, PackageIds.CommandCompletion)]
	internal class CommandCompletion : BaseCommand<CommandCompletion>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{

		}
	}
}

