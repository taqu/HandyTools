using EnvDTE;
using HandyTools.Models;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using static HandyTools.SettingFile;

namespace HandyTools.Commands
{
	[Command(PackageGuids.HandyToolsString, PackageIds.CommandAddDocument)]
	internal sealed class CommandAddDocument : CommandAIBase<CommandAddDocument>
	{
		protected override void Initialize()
		{
			Model = Types.TypeModel.General;
		}

		protected override void BeforeRun(AIModelSettings settingFile)
		{
			PromptTemplate = settingFile.PromptDocumentation;
			Response = Types.TypeResponse.Append;
			FormatResponse = settingFile.FormatResponse;
		}

		protected override async Task RunTaskAsync(ModelOpenAI model, DocumentView documentView, SnapshotSpan selection)
		{
            await VS.StatusBar.ShowProgressAsync("Handy Tools: Step 0/3", 0, 3);
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

			(string definitionCode, string indent, int declStartLine) = await CodeUtil.GetDefinitionCodeAsync(documentView, selection);
			if (string.IsNullOrEmpty(definitionCode))
			{
				HandyToolsPackage.Release(model);
				await VS.MessageBox.ShowAsync("Handy Tools: Documentation needs definition codes.", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				return;
			}
			string contentTypePrefix = documentView.TextView.TextDataModel.ContentType.DisplayName;
			string prompt = PromptTemplate.Replace("{filetype}", contentTypePrefix);
			prompt = PromptTemplate.Replace("{content}", definitionCode);

            await VS.StatusBar.ShowProgressAsync("Handy Tools: Step 1/3", 1, 3);
			string rawResponse = string.Empty;
			string response = string.Empty;
			try
			{
                rawResponse = await model.CompletionAsync(prompt, Temperature);
				response = PostProcessResponse(rawResponse);
                await VS.StatusBar.ShowProgressAsync("Handy Tools: Step 2/3", 2, 3);
			}
			catch (Exception ex)
			{
				HandyToolsPackage.Release(model);
				await VS.MessageBox.ShowAsync(ex.Message, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				return;
			}
			response = CodeUtil.ExtractDoxygenComment(response, indent, LineFeed);
			if (string.IsNullOrEmpty(response))
			{
				HandyToolsPackage.Release(model);
                await ShowOnChatWindowAsync(rawResponse);
                await VS.MessageBox.ShowAsync("Handy Tools: AI response is not appropriate.", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				return;
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
			HandyToolsPackage.Release(model);
            await VS.StatusBar.ShowProgressAsync("Handy Tools: Step 3/3", 3, 3);
		}
	}
}
