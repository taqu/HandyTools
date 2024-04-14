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

			(string definitionCode, string indent, int declStartLine) = await CodeUtil.GetDefinitionCodeAsync(documentView, selection);
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
				waitDialog.EndWaitDialog();
				return;
			}
			string response = string.Empty;
			try
			{
				response = await model.Get().CompletionAsync(prompt, Temperature);
				await Log.OutputAsync(response);
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
			response = CodeUtil.ExtractDoxygenComment(response, indent, LineFeed);
			if (string.IsNullOrEmpty(response))
			{
				model.Release();
				waitDialog.EndWaitDialog();
				await VS.MessageBox.ShowAsync("Handy Tools: AI response is not appropriate.", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				throw new Exception("AI response is not appropriate.");
			}

			ITextBuffer textBuffer = documentView.TextView.TextBuffer;
			ITextSnapshotLine declLine = textBuffer.CurrentSnapshot.GetLineFromLineNumber(declStartLine);
			string declLineText = declLine.GetText();
			ITextSnapshotLine line = textBuffer.CurrentSnapshot.GetLineFromLineNumber(declStartLine);
			await Log.OutputAsync(line.GetText());
			string lineText = line.GetText();
			string linefeed = string.Empty;
			switch (LineFeed)
			{
				case Types.TypeLineFeed.LF:
					linefeed = "\n";
					break;
				case Types.TypeLineFeed.CR:
					linefeed = "\r";
					break;
				case Types.TypeLineFeed.CRLF:
					linefeed = "\r\n";
					break;
			}
			documentView.TextBuffer.Insert(line.Start, response + linefeed);

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
