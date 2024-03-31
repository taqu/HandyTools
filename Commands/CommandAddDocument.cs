using EnvDTE;
using HandyTools.Models;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;

namespace HandyTools.Commands
{
	[Command(PackageGuids.HandyToolsString, PackageIds.CommandAddDocument)]
	internal sealed class CommandAddDocument : CommandAIBase<CommandAddDocument>
    {
		protected override void Initialize()
		{
			Model = Types.TypeModel.General;
		}

		protected override void BeforeRun(SettingFile settingFile)
		{
			PromptTemplate = settingFile.PromptDocumentation;
			Response = Types.TypeResponse.Append;
			FormatResponse = settingFile.FormatResponse;
		}

		protected override async Task RunTaskAsync(IVsThreadedWaitDialog4 waitDialog, RefCount<ModelBase> model, DocumentView documentView, SnapshotSpan selection)
		{
			using IDisposable disposable = waitDialog as IDisposable;
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

			(string definitionCode, string indent) = await CodeUtil.GetDefinitionCodeAsync();
			if (string.IsNullOrEmpty(definitionCode))
			{
				model.Release();
				waitDialog.EndWaitDialog();
				await VS.MessageBox.ShowAsync("Handy Tools: Documentation needs definition codes.", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				throw new Exception("Documentation needs definition codes.");
			}
			string contentTypePrefix = documentView.TextView.TextDataModel.ContentType.DisplayName;
			string prompt = PromptTemplate.Replace("{filetype}", contentTypePrefix);
			prompt = PromptTemplate.Replace("{content}", definitionCode);

			bool canceled = false;
			waitDialog.UpdateProgress("In progress", "Handy Tools: 1/3 steps", "Handy Tools: 1/3 steps", 1, 3, true, out canceled);
			if (canceled)
			{
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
			response = CodeUtil.ExtractDoxygenComment(response, indent, LineFeed);
			if(string.IsNullOrEmpty(response))
			{
				model.Release();
				waitDialog.EndWaitDialog();
				await VS.MessageBox.ShowAsync("Handy Tools: AI response is not appropriate.", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				throw new Exception("AI response is not appropriate.");
			}

			ITextBuffer textBuffer = documentView.TextView.TextBuffer;
			ITextSnapshotLine line = textBuffer.CurrentSnapshot.GetLineFromPosition(selection.Start.Position);
			documentView.TextBuffer.Insert(line.Start, response + Environment.NewLine);

			if (FormatResponse)
			{
				ITextSnapshotLine endLine = documentView.TextView.TextBuffer.CurrentSnapshot.GetLineFromPosition(selection.End);
				SnapshotSpan snapshotSpan = new SnapshotSpan(line.Start, endLine.End);
				documentView.TextView.Selection.Select(snapshotSpan, false);
				(await VS.GetServiceAsync<DTE, DTE>()).ExecuteCommand("Edit.FormatSelection");
			}
			model.Release();
			waitDialog.UpdateProgress("In progress", "Handy Tools: 3/3 steps", "Handy Tools: 3/3 steps", 3, 3, true, out _);
			waitDialog.EndWaitDialog();
		}
	}
}
