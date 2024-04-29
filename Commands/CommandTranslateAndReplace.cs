using EnvDTE;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio;
using System.Configuration;
using System.Windows;
using static HandyTools.SettingFile;

namespace HandyTools.Commands
{
	[Command(PackageGuids.HandyToolsString, PackageIds.CommandTranslateAndReplace)]
	internal sealed class CommandTranslateAndReplace : CommandAIBase<CommandTranslateAndReplace>
	{
		protected override void Initialize()
		{
			Model = Types.TypeModel.Translation;
		}

		protected override void BeforeRun(AIModelSettings settingFile)
		{
			PromptTemplate = settingFile.PromptTranslation;
			Response = Types.TypeResponse.Replace;
			FormatResponse = settingFile.FormatResponse;
		}

		protected override string PostProcessResponse(string response)
		{
			return StripResponseMarkdownCode(response);
		}
	}
}
