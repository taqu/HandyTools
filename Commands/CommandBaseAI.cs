using Azure;
using EnvDTE;
using LangChain.Providers;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TaskStatusCenter;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static HandyTools.Types;

namespace HandyTools.Commands
{
	internal class CommandAIBase<T> : BaseCommand<T> where T :class,new()
	{
        public string PromptTemplate { get;set; } = string.Empty; //{filetype}: file type name, {content}: content text
        public TypeResponse Response {get;set;} = TypeResponse.Append;
        public bool FormatResponse { get; set; } = false;

		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
            HandyToolsPackage package;
            if(!HandyToolsPackage.Package.TryGetTarget(out package))
            {
                return;
            }
            (Models.ModelBase model, SettingFile settingFile) = package.GetAIModel();
            if (null == model)
            {
                await VS.MessageBox.ShowAsync("Failed to load AI model. Please check settings.", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
                return;
            }
            BeforeRun(settingFile);

			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            IVsTaskStatusCenterService taskStatusCenter = await VS.Services.GetTaskStatusCenterAsync();
            TaskHandlerOptions options = default(TaskHandlerOptions);
            options.Title = "Handy Tools AI";
            options.ActionsAfterCompletion = CompletionActions.None;

            TaskProgressData data = default;
            data.ProgressText = "Chat Agent is typing ...";
            data.CanBeCanceled = true;

            ITaskHandler handler = taskStatusCenter.PreRegister(options, data);
            Task task = RunTaskAsync(data, handler, model);
            handler.RegisterTask(task);

            //         string response = await model.CompletionAsync(InputTextBox.Text);
            //         OutputTextBox.Text = response;
            //await VS.MessageBox.ShowWarningAsync("CommandCompletion", "Button clicked");
        }

        protected async Task RunTaskAsync(TaskProgressData data, ITaskHandler handler, Models.ModelBase model)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            data.PercentComplete = 0;
            data.ProgressText = "Step 0 of 3 completed";
            handler.Progress.Report(data);
            DocumentView documentView = await VS.Documents.GetActiveDocumentViewAsync();
            SnapshotSpan selection = documentView.TextView.Selection.SelectedSpans.FirstOrDefault();
            if (selection.Length <= 0)
            {
                ITextBuffer textBuffer = documentView.TextView.TextBuffer;
                ITextSnapshotLine line = textBuffer.CurrentSnapshot.GetLineFromPosition(selection.Start.Position);
                SnapshotSpan snapshotSpan = new SnapshotSpan(line.Start, line.End);
                documentView.TextView.Selection.Select(snapshotSpan, false);
                selection = documentView.TextView.Selection.SelectedSpans.FirstOrDefault();
            }
            string text = documentView.TextView.Selection.StreamSelectionSpan.GetText();
            int selectionStartPosition = selection.Start.Position;
            int selectionStartLineNumber = documentView.TextView.TextBuffer.CurrentSnapshot.GetLineNumberFromPosition(selectionStartPosition);
            if (string.IsNullOrEmpty(text))
            {
                data.PercentComplete = 100;
                data.ProgressText = "Please select text or not empty line.";
                handler.Progress.Report(data);
                //await VS.MessageBox.ShowAsync("Please select text or not empty line.", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
                return;
            }

            string contentTypePrefix = documentView.TextView.TextDataModel.ContentType.DisplayName;
            string prompt = PromptTemplate.Replace("{filetype}", contentTypePrefix);
            prompt = PromptTemplate.Replace("{content}", text);

            data.PercentComplete = 33;
            data.ProgressText = "Step 1 of 3 completed";
            handler.Progress.Report(data);
            string response = string.Empty;
            try
            {
                response = await model.CompletionAsync(prompt, handler.UserCancellation);
                    response = PostProcessResponse(response);

                data.PercentComplete = 66;
                data.ProgressText = "Step 2 of 3 completed";
                handler.Progress.Report(data);
            }
            catch (Exception ex)
            {
                data.PercentComplete = 100;
                data.ProgressText = ex.Message;
                handler.Progress.Report(data);
                //await VS.MessageBox.ShowAsync(ex.Message, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
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
                await VS.MessageBox.ShowAsync(response, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
                return;
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
        }

        private Regex StripMarkdownCode = new Regex(@"```.*\r?\n?");
        protected string StripResponseMarkdownCode(string response)
        {
            return StripMarkdownCode.Replace(response, "");
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