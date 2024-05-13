using EnvDTE;
using HandyTools.Models;
using HandyTools.ToolWindows;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using System.Linq;
using System.Text.RegularExpressions;
using static HandyTools.SettingFile;
using static HandyTools.Types;
using static Microsoft.VisualStudio.Threading.AsyncReaderWriterLock;

namespace HandyTools.Commands
{
    internal class CommandAIBase<T> : BaseCommand<T> where T : class, new()
	{
		public static async Task ShowOnChatWindowAsync(string Text)
		{
            ToolWindowPane windowPane = await ToolWindowChat.ShowAsync();
            ToolWindowChatControl windowControl = windowPane.Content as ToolWindowChatControl;
            if (null != windowControl)
            {
                windowControl.Output = Text;
            }
        }

        internal class CancelCallback : IVsThreadedWaitDialogCallback
		{
			public CancelCallback(ModelOpenAI model)
			{
				model_ = model;
			}

			public void OnCanceled()
			{
				HandyToolsPackage.Release(model_);
			}
			private ModelOpenAI model_;
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
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            HandyToolsPackage package = await HandyToolsPackage.GetPackageAsync();
            if (null == package)
            {
                return;
            }
			Initialize();
			DocumentView documentView = await VS.Documents.GetActiveDocumentViewAsync();
            (ModelOpenAI model,  SettingFile.AIModelSettings settingFile) = package.GetAIModel(Model, documentView.FilePath);
			if (null == model)
			{
				await VS.MessageBox.ShowAsync("Failed to load AI model. Please check settings.", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				return;
			}
			MaxTextLength = settingFile.MaxTextLength;
			LineFeed = settingFile.Get(CodeUtil.GetLanguageFromDocument(package.DTE.ActiveDocument));
			Temperature = settingFile.Temperature;
			BeforeRun(settingFile);
			SnapshotSpan selection = documentView.TextView.Selection.SelectedSpans.FirstOrDefault();
			await RunTaskAsync(model, documentView, selection);
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

		protected virtual async Task RunTaskAsync(ModelOpenAI model, DocumentView documentView, SnapshotSpan selection)
		{
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
				HandyToolsPackage.Release(model);
				await VS.MessageBox.ShowAsync("Handy Tools: Please select text or not empty line.", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				return;
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
            await VS.StatusBar.ShowProgressAsync("Handy Tools: Step 1/3", 1, 3);
			if (canceled)
			{
				HandyToolsPackage.Release(model);
				return;
			}
			string response = string.Empty;
			try
			{
				response = await model.CompletionAsync(prompt, Temperature);
				response = PostProcessResponse(response);

                await VS.StatusBar.ShowProgressAsync("Handy Tools: Step 2/3", 2, 3);
				if (canceled)
				{
					HandyToolsPackage.Release(model);
					return;
				}
			}
			catch (Exception ex)
			{
				HandyToolsPackage.Release(model);
				await VS.MessageBox.ShowAsync("Handy Tools: " + ex.Message, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				return;
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
			HandyToolsPackage.Release(model);
            await VS.StatusBar.ShowProgressAsync("Handy Tools: Step 3/3", 3, 3);
		}

		private static Regex StripMarkdownCode = new Regex(@"```.*\r?\n?");
		protected static string StripResponseMarkdownCode(string response)
		{
			return StripMarkdownCode.Replace(response, "");
		}

		protected virtual void Initialize()
		{
		}

		protected virtual void BeforeRun(AIModelSettings settingFile)
		{
		}

		protected virtual string PostProcessResponse(string response)
		{
			return response;
		}
	}
}