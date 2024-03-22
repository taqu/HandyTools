using EnvDTE;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio;
using System.Configuration;
using System.Windows;

namespace HandyTools.Commands
{
	[Command(PackageGuids.HandyToolsString, PackageIds.CommandTranslateAndReplace)]
	internal sealed class CommandTranslateAndReplace : CommandAIBase<CommandTranslateAndReplace>
	{
		protected override void Initialize()
		{
			OllamaModel = Types.TypeOllamaModel.Translation;
		}

		protected override void BeforeRun(SettingFile settingFile)
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