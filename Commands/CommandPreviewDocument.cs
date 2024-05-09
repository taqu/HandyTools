using EnvDTE;
using HandyTools.Models;
using HandyTools.ToolWindows;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using static HandyTools.SettingFile;

namespace HandyTools.Commands
{
	[Command(PackageGuids.HandyToolsString, PackageIds.CommandPreviewDocument)]
	internal class CommandPreviewDocument : CommandAIBase<CommandPreviewDocument>
	{
		protected override void Initialize()
		{
			Model = Types.TypeModel.General;
		}

		protected override void BeforeRun(AIModelSettings settingFile)
		{
			PromptTemplate = settingFile.PromptDocumentation;
			Response = Types.TypeResponse.Message;
			FormatResponse = false;
		}

		protected override async Task RunTaskAsync(ModelOpenAI model, DocumentView documentView, SnapshotSpan selection)
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

			(string definitionCode, string indent, int _) = await CodeUtil.GetDefinitionCodeAsync(documentView, selection);
			if (string.IsNullOrEmpty(definitionCode))
			{
				HandyToolsPackage.Release(model);
				await VS.MessageBox.ShowAsync("Documentation needs definition codes.", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				return;
            }
			string contentTypePrefix = documentView.TextView.TextDataModel.ContentType.DisplayName;
			string prompt = PromptTemplate.Replace("{filetype}", contentTypePrefix);
			prompt = prompt.Replace("{content}", definitionCode);

			if (MaxTextLength < prompt.Length)
			{
				prompt = prompt.Substring(0, MaxTextLength);
			}

            await VS.StatusBar.ShowProgressAsync("Handy Tools: Step 1/3", 1, 3);
			string response = string.Empty;
			try
			{
				response = await model.CompletionAsync(prompt, Temperature);
				response = PostProcessResponse(response);

                await VS.StatusBar.ShowProgressAsync("Handy Tools: Step 2/3", 2, 3);
			}
			catch (Exception ex)
			{
				HandyToolsPackage.Release(model);
				await VS.MessageBox.ShowAsync(ex.Message, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				return;
			}
			string doxygenComment = CodeUtil.ExtractDoxygenComment(response, indent, LineFeed);
			await ShowOnChatWindowAsync(string.IsNullOrEmpty(doxygenComment) ? response : doxygenComment);

			HandyToolsPackage.Release(model);
            await VS.StatusBar.ShowProgressAsync("Handy Tools: Step 3/3", 3, 3);
		}
	}
}
