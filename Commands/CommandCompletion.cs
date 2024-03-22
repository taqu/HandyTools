namespace HandyTools.Commands
{
	[Command(PackageGuids.HandyToolsString, PackageIds.CommandCompletion)]
	internal sealed class CommandCompletion : CommandAIBase<CommandCompletion>
	{
		protected override void Initialize()
		{
			OllamaModel = Types.TypeOllamaModel.Generation;
		}

		protected override void BeforeRun(SettingFile settingFile)
		{
			PromptTemplate = settingFile.PromptCompletion;
			Response = Types.TypeResponse.Append;
			FormatResponse = settingFile.FormatResponse;
		}

		protected override string PostProcessResponse(string response)
		{
			return StripResponseMarkdownCode(response);
		}
	}
}
