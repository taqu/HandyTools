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

		protected override async Task RunTaskAsync(IVsThreadedWaitDialog4 waitDialog, RefCount<ModelBase> model, DocumentView documentView, SnapshotSpan selection)
		{
			using IDisposable disposable = waitDialog as IDisposable;
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

			(string definitionCode, string indent, int _) = await CodeUtil.GetDefinitionCodeAsync(documentView, selection);
			if (string.IsNullOrEmpty(definitionCode))
			{
				model.Release();
				waitDialog.EndWaitDialog();
				await VS.MessageBox.ShowAsync("Documentation needs definition codes.", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				throw new Exception("Documentation needs definition codes.");
			}
			string contentTypePrefix = documentView.TextView.TextDataModel.ContentType.DisplayName;
			string prompt = PromptTemplate.Replace("{filetype}", contentTypePrefix);
			prompt = PromptTemplate.Replace("{content}", definitionCode);

			if (MaxTextLength < prompt.Length)
			{
				prompt = prompt.Substring(0, MaxTextLength);
			}

			bool canceled = false;
			waitDialog.UpdateProgress("In progress", "Handy Tools: 1/3 steps", "Handy Tools: 1/3 steps", 1, 3, true, out canceled);
			if (canceled)
			{
				waitDialog.EndWaitDialog();
				return;
			}
			string response = string.Empty;
			try
			{
				response = await model.Get().CompletionAsync(prompt, Temperature);
				response = PostProcessResponse(response);

				waitDialog.UpdateProgress("In progress", "Handy Tools: 2/3 steps", "Handy Tools: 2/3 steps", 2, 3, true, out canceled);
				if (canceled)
				{
					waitDialog.EndWaitDialog();
					return;
				}
			}
			catch (Exception ex)
			{
				model.Release();
				waitDialog.EndWaitDialog();
				await VS.MessageBox.ShowAsync(ex.Message, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				throw ex;
			}
			string doxygenComment = CodeUtil.ExtractDoxygenComment(response, indent, LineFeed);
			//if (string.IsNullOrEmpty(doxygenComment))
			//{
			//	model.Release();
			//	waitDialog.EndWaitDialog();
			//	await VS.MessageBox.ShowAsync("AI response is not appropriate.", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
			//	throw new Exception("AI response is not appropriate.");
			//}
			ToolWindowPane windowPane = await ToolWindowChat.ShowAsync();
			ToolWindowChatControl windowControl = windowPane.Content as ToolWindowChatControl;
			if (null != windowControl)
			{
				windowControl.Output = string.IsNullOrEmpty(doxygenComment)? response : doxygenComment;
			}
			model.Release();
			waitDialog.UpdateProgress("In progress", "Handy Tools: 3/3 steps", "Handy Tools: 3/3 steps", 3, 3, true, out _);
			waitDialog.EndWaitDialog();
		}
	}
}
