using EnvDTE;
using HandyTools.Models;
using HandyTools.ToolWindows;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using System.Linq;
using System.Text.RegularExpressions;
using static HandyTools.Types;

namespace HandyTools.Commands
{
    internal class CommandAIBase<T> : BaseCommand<T> where T : class, new()
	{
		internal class CancelCallback : IVsThreadedWaitDialogCallback
		{
			public CancelCallback(RefCount<ModelBase> model)
			{
				model_ = model;
			}

			public void OnCanceled()
			{
				model_.Release();
			}
			private RefCount<ModelBase> model_;
		}

		protected string PromptTemplate { get; set; } = string.Empty; //{filetype}: file type name, {content}: content text
		protected TypeResponse Response { get; set; } = TypeResponse.Append;
		protected int MaxTextLength { get; set; } = 4096;
		protected bool FormatResponse { get; set; } = false;
		protected bool ExtractDefinition { get; set; } = false;
		protected TypeModel Model { get; set; } = TypeModel.General;
		protected TypeLineFeed LineFeed { get; set; }
		protected float Temperature { get;set; } = 0.1f;

		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			HandyToolsPackage package;
			if (!HandyToolsPackage.Package.TryGetTarget(out package))
			{
				return;
			}
			Initialize();
			DocumentView documentView = await VS.Documents.GetActiveDocumentViewAsync();
			(RefCount<ModelBase> model, SettingFile settingFile) = package.GetAIModel(Model, documentView.FilePath);
			if (null == model)
			{
				await VS.MessageBox.ShowAsync("Failed to load AI model. Please check settings.", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				return;
			}
			if (1 < model.Count)
			{
				model.Release();
				await VS.MessageBox.ShowAsync("Handy Tools is already running.", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				return;
			}
			MaxTextLength = settingFile.MaxTextLength;
			LineFeed = settingFile.Get(CodeUtil.GetLanguageFromDocument(package.DTE.ActiveDocument));
			Temperature = settingFile.Temperature;
			BeforeRun(settingFile);
			SnapshotSpan selection = documentView.TextView.Selection.SelectedSpans.FirstOrDefault();

			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			IVsThreadedWaitDialogFactory dialogFactory = (IVsThreadedWaitDialogFactory)await VS.Services.GetThreadedWaitDialogAsync();
			IVsThreadedWaitDialog4 threadedWaitDialog = dialogFactory.CreateInstance();
			CancelCallback cancelCallback = new CancelCallback(model);
			threadedWaitDialog.StartWaitDialogWithCallback(
				"Handy Tools AI",
				"Chat Agent is working on it ...",
				"",
				null,
				"Handy Tools AI working ...",
				true,
				30, true,
				3,
				0,
				cancelCallback);

			await RunTaskAsync(threadedWaitDialog, model, documentView, selection);
		}

		protected async System.Threading.Tasks.Task<(SnapshotSpan selection, string text)> GetCurrentSelectionAsync(DocumentView documentView, SnapshotSpan selection)
		{
			string text = null;
			if (selection.Length <= 0)
			{
				if (ExtractDefinition)
				{
					(string definitionCode, string indent, int declStartLine) = await CodeUtil.GetDefinitionCodeAsync(documentView, selection);
					if (string.IsNullOrEmpty(definitionCode))
					{
						selection = SelectCurrentLine(documentView, selection);
					}
					else
					{
						text = definitionCode;
					}
				}
				else
				{
					selection = SelectCurrentLine(documentView, selection);
				}
			}
			if (string.IsNullOrEmpty(text))
			{
				text = documentView.TextView.Selection.StreamSelectionSpan.GetText();
			}
			return (selection, text);
		}

		protected static SnapshotSpan SelectCurrentLine(DocumentView documentView, SnapshotSpan selection)
		{
			ITextBuffer textBuffer = documentView.TextView.TextBuffer;
			ITextSnapshotLine line = textBuffer.CurrentSnapshot.GetLineFromPosition(selection.Start.Position);
			SnapshotSpan snapshotSpan = new SnapshotSpan(line.Start, line.End);
			documentView.TextView.Selection.Select(snapshotSpan, false);
			return documentView.TextView.Selection.SelectedSpans.FirstOrDefault();
		}

		protected virtual async Task RunTaskAsync(IVsThreadedWaitDialog4 waitDialog, RefCount<ModelBase> model, DocumentView documentView, SnapshotSpan selection)
		{
			using IDisposable disposable = waitDialog as IDisposable;
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

			string text = null;
			if (selection.Length <= 0)
			{
				if (ExtractDefinition)
				{
					(string definitionCode, string indent, int declStartLine) = await CodeUtil.GetDefinitionCodeAsync(documentView, selection);
					if (string.IsNullOrEmpty(definitionCode))
					{
						selection = SelectCurrentLine(documentView, selection);
					}
					else
					{
						text = definitionCode;
					}
				}
				else
				{
					selection = SelectCurrentLine(documentView, selection);
				}
			}
			if (string.IsNullOrEmpty(text))
			{
				text = documentView.TextView.Selection.StreamSelectionSpan.GetText();
			}
			if (string.IsNullOrEmpty(text))
			{
				model.Release();
				waitDialog.EndWaitDialog();
				await VS.MessageBox.ShowAsync("Handy Tools: Please select text or not empty line.", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				throw new Exception("Please select text or not empty line.");
			}
			int selectionStartLineNumber = documentView.TextView.TextBuffer.CurrentSnapshot.GetLineNumberFromPosition(selection.Start.Position);

			string contentTypePrefix = documentView.TextView.TextDataModel.ContentType.DisplayName;
			string prompt = PromptTemplate.Replace("{filetype}", contentTypePrefix);
			prompt = PromptTemplate.Replace("{content}", text);

			bool canceled = false;
			if (MaxTextLength < prompt.Length)
			{
				prompt = prompt.Substring(0, MaxTextLength);
			}
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
				await VS.MessageBox.ShowAsync("Handy Tools: " + ex.Message, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				throw ex;
			}
			switch (Response)
			{
				case TypeResponse.Append:
					documentView.TextBuffer.Insert(selection.End, Environment.NewLine + response);
					break;
				case TypeResponse.Replace:
					documentView.TextBuffer.Replace(selection, response);
					break;
				case TypeResponse.Message:
					{
						ToolWindowPane windowPane = await ToolWindowChat.ShowAsync();
						ToolWindowChatControl windowControl = windowPane.Content as ToolWindowChatControl;
						if (null != windowControl)
						{
							windowControl.Output = response;
						}
					}
					break;
			}

			if (FormatResponse)
			{
				selection = documentView.TextView.Selection.SelectedSpans.FirstOrDefault();
				if (selection.Length == 0)
				{
					ITextSnapshotLine startLine = documentView.TextView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(selectionStartLineNumber);
					ITextSnapshotLine endLine = documentView.TextView.TextBuffer.CurrentSnapshot.GetLineFromPosition(selection.End);
					SnapshotSpan snapshotSpan = new SnapshotSpan(startLine.Start, endLine.End);
					documentView.TextView.Selection.Select(snapshotSpan, false);
				}

				(await VS.GetServiceAsync<DTE, DTE>()).ExecuteCommand("Edit.FormatSelection");
			}
			model.Release();
			waitDialog.UpdateProgress("In progress", "Handy Tools: 3/3 steps", "Handy Tools: 3/3 steps", 3, 3, true, out _);
			waitDialog.EndWaitDialog();
		}

		private static Regex StripMarkdownCode = new Regex(@"```.*\r?\n?");
		protected static string StripResponseMarkdownCode(string response)
		{
			return StripMarkdownCode.Replace(response, "");
		}

		protected virtual void Initialize()
		{
		}

		protected virtual void BeforeRun(SettingFile settingFile)
		{
		}

		protected virtual string PostProcessResponse(string response)
		{
			return response;
		}
	}
}