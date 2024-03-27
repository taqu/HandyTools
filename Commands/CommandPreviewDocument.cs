using EnvDTE;
using HandyTools.Models;
using HandyTools.ToolWindows;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TaskStatusCenter;
using Microsoft.VisualStudio.Text;
using System.Linq;
using static HandyTools.Types;

namespace HandyTools.Commands
{
	[Command(PackageGuids.HandyToolsString, PackageIds.CommandPreviewDocument)]
	internal class CommandPreviewDocument : CommandAIBase<CommandPreviewDocument>
	{
		protected override void Initialize()
		{
			Model = Types.TypeModel.General;
		}

		protected override void BeforeRun(SettingFile settingFile)
		{
			PromptTemplate = settingFile.PromptDocumentation;
			Response = Types.TypeResponse.Message;
			FormatResponse = false;
		}

		protected override async Task RunTaskAsync(IVsThreadedWaitDialog4 waitDialog, RefCount<ModelBase> model, DocumentView documentView, SnapshotSpan selection)
		{
			using IDisposable disposable = waitDialog as IDisposable;
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			waitDialog.UpdateProgress("In progress", "Handy Tools: 0/3 steps", "Handy Tools: 0/3 steps", 0, 3, true, out _);

			(string definitionCode, string indent) = await CodeUtil.GetDefinitionCodeAsync();
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

			waitDialog.UpdateProgress("In progress", "Handy Tools: 1/3 steps", "Handy Tools: 1/3 steps", 1, 3, true, out _);
			string response = string.Empty;
			try
			{
				response = await model.Get().CompletionAsync(prompt, Temperature);
				response = PostProcessResponse(response);

				waitDialog.UpdateProgress("In progress", "Handy Tools: 2/3 steps", "Handy Tools: 2/3 steps", 2, 3, true, out _);
			}
			catch (Exception ex)
			{
				model.Release();
				waitDialog.EndWaitDialog();
				await VS.MessageBox.ShowAsync(ex.Message, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				throw ex;
			}
			response = CodeUtil.ExtractDoxygenComment(response, indent, LineFeed);
			if (string.IsNullOrEmpty(response))
			{
				model.Release();
				waitDialog.EndWaitDialog();
				await VS.MessageBox.ShowAsync("AI response is not appropriate.", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				throw new Exception("AI response is not appropriate.");
			}
			ToolWindowPane windowPane = await ToolWindowChat.ShowAsync();
			ToolWindowChatControl windowControl = windowPane.Content as ToolWindowChatControl;
			if (null != windowControl)
			{
				windowControl.Output = response;
			}
			model.Release();
			waitDialog.UpdateProgress("In progress", "Handy Tools: 3/3 steps", "Handy Tools: 3/3 steps", 3, 3, true, out _);
		}
	}
}
