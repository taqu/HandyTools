using EnvDTE;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TaskStatusCenter;
using Microsoft.VisualStudio.Text.Editor;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;
using System.Windows.Media.TextFormatting;
using Microsoft.VisualStudio.VCCodeModel;
using System.Net;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.OLE.Interop;
using HandyTools.ToolWindows;
using static HandyTools.Types;
using static System.Net.Mime.MediaTypeNames;

namespace HandyTools.Commands
{
	[Command(PackageGuids.HandyToolsString, PackageIds.CommandPreviewDocument)]
	internal class CommandPreviewDocument : CommandAIBase<CommandPreviewDocument>
	{
		protected override void Initialize()
		{
			OllamaModel = Types.TypeOllamaModel.General;
			ExtractOneLine = false;
		}

		protected override void BeforeRun(SettingFile settingFile)
		{
			PromptTemplate = settingFile.PromptDocumentation;
			Response = Types.TypeResponse.Message;
			FormatResponse = false;
		}

		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			HandyToolsPackage package;
			if (!HandyToolsPackage.Package.TryGetTarget(out package))
			{
				return;
			}
			Initialize();
			(Models.ModelBase model, SettingFile settingFile) = package.GetAIModel(OllamaModel);
			if (null == model)
			{
				await VS.MessageBox.ShowAsync("Failed to load AI model. Please check settings.", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
				return;
			}
			MaxTextLength = settingFile.MaxTextLength;
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
		}

		protected override async Task RunTaskAsync(TaskProgressData data, ITaskHandler handler, Models.ModelBase model)
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			data.PercentComplete = 0;
			data.ProgressText = "Step 0 of 3 completed";
			handler.Progress.Report(data);

			string definitionCode = await CodeUtil.GetDefinitionCodeAsync();
			if (string.IsNullOrEmpty(definitionCode))
			{
				throw new Exception("Documentation needs definition codes.");
			}
			DocumentView documentView = await VS.Documents.GetActiveDocumentViewAsync();
			string contentTypePrefix = documentView.TextView.TextDataModel.ContentType.DisplayName;
			string prompt = PromptTemplate.Replace("{filetype}", contentTypePrefix);
			prompt = PromptTemplate.Replace("{content}", definitionCode);

			if (MaxTextLength < prompt.Length)
			{
				prompt = prompt.Substring(0, MaxTextLength);
			}

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
				throw ex;
			}
			response = StripResponseMarkdownCode(response);
			ToolWindowPane windowPane = await ToolWindowChat.ShowAsync();
			ToolWindowChatControl windowControl = windowPane.Content as ToolWindowChatControl;
			if (null != windowControl)
			{
				windowControl.Output = response;
			}
		}
	}
}
