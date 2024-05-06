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

		protected override async Task RunTaskAsync(IVsThreadedWaitDialog4 waitDialog, ModelOpenAI model, DocumentView documentView, SnapshotSpan selection)
		{
			using IDisposable disposable = waitDialog as IDisposable;
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

			bool canceled = false;
			(string prefix, string suffix) = CodeUtil.GetCodeAround(documentView, selection.Start, maxCompletionInputSize_);
			await Log.OutputAsync("prefix: " + prefix + "\n");
			await Log.OutputAsync("suffix: " + suffix + "\n");

			string response = string.Empty;
			try
			{
				string prompt = CodeUtil.FormatFillInTheMiddle(completionPrompt_, prefix, suffix);
				response = await model.CompletionAsync(prompt, Temperature, default, maxCompletionOutputSize_);
				await Log.OutputAsync("response: " + response + "\n");
				response = PostProcessResponse(response);
				waitDialog.UpdateProgress("In progress", "Handy Tools: 2/3 steps", "Handy Tools: 2/3 steps", 2, 3, false, out canceled);
				if (canceled)
				{
					HandyToolsPackage.Release(model);
					waitDialog.EndWaitDialog();
					return;
				}
			}
			catch (Exception ex)
			{
				HandyToolsPackage.Release(model);
				waitDialog.EndWaitDialog();
				await VS.MessageBox.ShowAsync(ex.Message, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				throw ex;
			}

			documentView.TextBuffer.Insert(selection.Start.Position, response);

			HandyToolsPackage.Release(model);
			waitDialog.UpdateProgress("In progress", "Handy Tools: 3/3 steps", "Handy Tools: 3/3 steps", 3, 3, true, out _);
			waitDialog.EndWaitDialog();
		}

		private string completionPrompt_ = DefaultPrompts.PromptCompletion;
		private int maxCompletionInputSize_ = 4096;
		private int maxCompletionOutputSize_ = 64;
	}
}

