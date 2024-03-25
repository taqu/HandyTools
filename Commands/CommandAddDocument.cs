using EnvDTE;
using HandyTools.Models;
using LangChain.Providers;
using LangChain.Sources;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TaskStatusCenter;
using Microsoft.VisualStudio.Text;
using System.Linq;
using static HandyTools.Types;

namespace HandyTools.Commands
{
	[Command(PackageGuids.HandyToolsString, PackageIds.CommandAddDocument)]
	internal sealed class CommandAddDocument : CommandAIBase<CommandAddDocument>
    {
		protected override void Initialize()
		{
			OllamaModel = Types.TypeOllamaModel.General;
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
			waitDialog.UpdateProgress("In progress", "Handy Tools: 0/3 steps", "Handy Tools: 0/3 steps", 0, 3, true, out _);

			(string definitionCode, string indent) = await CodeUtil.GetDefinitionCodeAsync();
			if (string.IsNullOrEmpty(definitionCode))
			{
				model.Release();
				await VS.StatusBar.ShowMessageAsync("Documentation needs definition codes.");
				throw new Exception("Documentation needs definition codes.");
			}
			string contentTypePrefix = documentView.TextView.TextDataModel.ContentType.DisplayName;
			string prompt = PromptTemplate.Replace("{filetype}", contentTypePrefix);
			prompt = PromptTemplate.Replace("{content}", definitionCode);

			waitDialog.UpdateProgress("In progress", "Handy Tools: 1/3 steps", "Handy Tools: 1/3 steps", 1, 3, true, out _);
			string response = string.Empty;
			try
			{
				response = await model.Get().CompletionAsync(prompt);
				response = PostProcessResponse(response);

				waitDialog.UpdateProgress("In progress", "Handy Tools: 2/3 steps", "Handy Tools: 2/3 steps", 2, 3, true, out _);
			}
			catch (Exception ex)
			{
				model.Release();
				await VS.StatusBar.ShowMessageAsync(ex.Message);
				throw ex;
			}
			response = CodeUtil.ExtractDoxygenComment(response, indent, LineFeed);
			if(string.IsNullOrEmpty(response))
			{
				await VS.StatusBar.ShowMessageAsync("AI response is not appropriate.");
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
		}
	}
}
