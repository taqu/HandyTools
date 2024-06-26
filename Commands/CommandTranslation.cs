using static HandyTools.SettingFile;

namespace HandyTools.Commands
{
	[Command(PackageGuids.HandyToolsString, PackageIds.CommandTranslation)]
	internal sealed class CommandTranslation : CommandAIBase<CommandTranslation>
	{
		protected override void Initialize()
		{
			Model = Types.TypeModel.Translation;
		}

		protected override void BeforeRun(AIModelSettings settingFile)
		{
			PromptTemplate = settingFile.PromptTranslation;
			Response = Types.TypeResponse.Message;
			FormatResponse = false;
		}
	}
}
