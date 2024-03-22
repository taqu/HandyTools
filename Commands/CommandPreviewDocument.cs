using EnvDTE;
using HandyTools.ToolWindows;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TaskStatusCenter;

namespace HandyTools.Commands
{
	[Command(PackageGuids.HandyToolsString, PackageIds.CommandPreviewDocument)]
	internal class CommandPreviewDocument : CommandAIBase<CommandPreviewDocument>
	{
		protected override void Initialize()
		{
			OllamaModel = Types.TypeOllamaModel.General;
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
				await VS.StatusBar.ShowMessageAsync("Documentation needs definition codes.");
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
				await VS.StatusBar.ShowMessageAsync(ex.Message);
				throw ex;
			}
			response = CodeUtil.ExtractDoxygenComment(response);
			if (string.IsNullOrEmpty(response))
			{
				await VS.StatusBar.ShowMessageAsync("AI response is not appropriate.");
				throw new Exception("AI response is not appropriate.");
			}
			ToolWindowPane windowPane = await ToolWindowChat.ShowAsync();
			ToolWindowChatControl windowControl = windowPane.Content as ToolWindowChatControl;
			if (null != windowControl)
			{
				windowControl.Output = response;
			}
		}
	}
}
