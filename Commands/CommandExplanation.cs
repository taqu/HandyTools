using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandyTools.Commands
{
	[Command(PackageGuids.HandyToolsString, PackageIds.CommandExplanation)]
	internal sealed class CommandExplanation : CommandAIBase<CommandExplanation>
	{
		protected override void Initialize()
		{
			OllamaModel = Types.TypeOllamaModel.General;
			ExtractOneLine = false;
		}

		protected override void BeforeRun(SettingFile settingFile)
		{
			PromptTemplate = settingFile.PromptExplanation;
			Response = Types.TypeResponse.Message;
			FormatResponse = false;
		}
	}
}
