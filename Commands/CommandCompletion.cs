using EnvDTE;
using HandyTools.Models;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using static HandyTools.SettingFile;

namespace HandyTools.Commands
{
	[Command(PackageGuids.HandyToolsString, PackageIds.CommandCompletion)]
	internal class CommandCompletion : CommandAIBase<CommandCompletion>
	{
		protected override void Initialize()
		{
			Model = Types.TypeModel.Generation;
			Temperature = 0.0f;
		}

		protected override void BeforeRun(AIModelSettings settingFile)
		{
			Response = Types.TypeResponse.Append;
			FormatResponse = settingFile.FormatResponse;
			MaxTextLength = settingFile.MaxTextLength;
			maxCompletionInputSize_ = settingFile.MaxCompletionInputSize;
			maxCompletionOutputSize_ = settingFile.MaxCompletionOutputSize;
			completionPrompt_ = settingFile.PromptCompletion;
		}

		protected override async Task RunTaskAsync(ModelOpenAI model, DocumentView documentView, SnapshotSpan selection)
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

			(string prefix, string suffix) = CodeUtil.GetCodeAround(documentView, selection.Start, maxCompletionInputSize_);
			await Log.OutputAsync("prefix: " + prefix + "\n");
			await Log.OutputAsync("suffix: " + suffix + "\n");

			string response = string.Empty;
			try
			{
                await VS.StatusBar.ShowProgressAsync("Handy Tools: Step 1/3", 1, 3);
                string prompt = CodeUtil.FormatFillInTheMiddle(completionPrompt_, prefix, suffix);
				response = await model.CompletionAsync(prompt, Temperature, default, maxCompletionOutputSize_);
				await Log.OutputAsync("response: " + response + "\n");
				response = PostProcessResponse(response);
                await VS.StatusBar.ShowProgressAsync("Handy Tools: Step 2/3", 2, 3);
			}
			catch (Exception ex)
			{
				HandyToolsPackage.Release(model);
				await VS.MessageBox.ShowAsync(ex.Message, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				return;
			}

			documentView.TextBuffer.Insert(selection.Start.Position, response);

			HandyToolsPackage.Release(model);
            await VS.StatusBar.ShowProgressAsync("Handy Tools: Step 3/3", 3, 3);
		}

		private string completionPrompt_ = DefaultPrompts.PromptCompletion;
		private int maxCompletionInputSize_ = 4096;
		private int maxCompletionOutputSize_ = 64;
	}
}

