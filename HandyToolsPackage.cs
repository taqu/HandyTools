global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using HandyTools.Completion;
using HandyTools.Models;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static HandyTools.SettingFile;
using static HandyTools.Types;

namespace HandyTools
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
	[Guid(HandyToolsPackage.PackageGuidString)]
	//[ProvideAutoLoad(UIContextGuids.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(UIContextGuids.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(UIContextGuids.CodeWindow, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[ProvideOptionPage(typeof(Options.OptionPageHandyTools), "HandyTools", "General", 0, 0, true)]
	[ProvideOptionPage(typeof(Options.OptionPageHandyToolsAI), "HandyTools", "AI", 1, 1, true)]
	[ProvideToolWindow(typeof(HandyTools.ToolWindows.ToolWindowChat.Pane), Style = VsDockStyle.Linked, Window = WindowGuids.SolutionExplorer)]
	public sealed class HandyToolsPackage : ToolkitPackage
	{
		/// <summary>
		/// HandyToolsPackage GUID string.
		/// </summary>
		public const string PackageGuidString = PackageGuids.HandyToolsString;
		public const int MaxPoolSize = 4;

		#region Package Members

		public static HandyToolsPackage GetPackage()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			HandyToolsPackage package;
			if (package_.TryGetTarget(out package))
			{
				return package;
			}
			IVsShell shell = GetGlobalService(typeof(SVsShell)) as IVsShell;
			if (null == shell)
			{
				return null;
			}
			IVsPackage vsPackage = null;
			Guid PackageToBeLoadedGuid = new Guid(HandyToolsPackage.PackageGuidString);
			shell.LoadPackage(ref PackageToBeLoadedGuid, out vsPackage);
			return vsPackage as HandyToolsPackage;
		}

        public static bool TryGetPackage(out HandyToolsPackage package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (null != package_ && package_.TryGetTarget(out package))
            {
                return true;
            }
			package = null;
            return false;
        }

        public static async Task<HandyToolsPackage> GetPackageAsync()
        {
			HandyToolsPackage package;
            if (null != package_ && package_.TryGetTarget(out package))
            {
                return package;
            }
            package = await Task.Run<HandyToolsPackage>(() =>
			{
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                IVsShell shell = GetGlobalService(typeof(SVsShell)) as IVsShell;
                if (null == shell)
                {
                    return null;
                }
                IVsPackage vsPackage = null;
                Guid PackageToBeLoadedGuid = new Guid(HandyToolsPackage.PackageGuidString);
                shell.LoadPackage(ref PackageToBeLoadedGuid, out vsPackage);
				return vsPackage as HandyToolsPackage;
            });

            return package;
        }

        public static WeakReference<HandyToolsPackage> Package { get => package_; }

        public EnvDTE80.DTE2 DTE { get { return dte2_; } }
		public SVsRunningDocumentTable RDT { get { return runningDocumentTable_; } }

		public Options.OptionPageHandyTools Options
		{
			get
			{
				return GetDialogPage(typeof(Options.OptionPageHandyTools)) as Options.OptionPageHandyTools;
			}
		}

		public Options.OptionPageHandyToolsAI AIOptions
		{
			get
			{
				return GetDialogPage(typeof(Options.OptionPageHandyToolsAI)) as Options.OptionPageHandyToolsAI;
			}
		}

		public TextSettings GetTextSettings(string documentPath)
		{
			lock (lockObject_)
			{
				SettingFile settingFile = SettingFile.Load(package_, documentPath);
				return new TextSettings(settingFile);
			}
		}

		public SettingFile.AIModelSettings GetAISettings(string documentPath)
		{
			lock (lockObject_)
			{
				SettingFile settingFile = SettingFile.Load(package_, documentPath);
				return new AIModelSettings(settingFile);
			}
		}

		public ModelOpenAI GetAIModel(SettingFile.AIModelSettings aiModelSettings, TypeModel type)
		{
			lock (lockObject_)
			{
				ModelOpenAI aiModel;
				if (aiModels_.Count <= 0)
				{
					aiModel = new ModelOpenAI(aiModelSettings, type);
				}
				else
				{
					aiModel = aiModels_[aiModels_.Count - 1];
					aiModels_.RemoveAt(aiModels_.Count - 1);
					aiModel.Model = aiModelSettings.GetModelName(type);
					aiModel.APIEndpoint = aiModelSettings.GetAPIEndpoint(type);
				}
				return aiModel;
			}
		}

		public (ModelOpenAI, SettingFile.AIModelSettings) GetAIModel(TypeModel type, string documentPath)
		{
			AIModelSettings aiModelSettings;
			lock (lockObject_)
			{
				SettingFile settingFile = SettingFile.Load(package_, documentPath);
				aiModelSettings = new AIModelSettings(settingFile);
				ModelOpenAI aiModel;
				if (aiModels_.Count <= 0)
				{
					aiModel = new ModelOpenAI(aiModelSettings, type);
				}
				else
				{
					aiModel = aiModels_[aiModels_.Count - 1];
					aiModels_.RemoveAt(aiModels_.Count - 1);
					aiModel.Model = aiModelSettings.GetModelName(type);
					aiModel.APIEndpoint = aiModelSettings.GetAPIEndpoint(type);
				}
				return (aiModel, aiModelSettings);
			}
		}

		public void ReleaseAIModel(ModelOpenAI aiModel)
		{
			lock (lockObject_)
			{
				if(MaxPoolSize<=aiModels_.Count)
				{
					aiModels_.RemoveAt(0);
				}
				aiModels_.Add(aiModel);
			}
		}

		public static void Release(ModelOpenAI aiModel)
		{
			HandyToolsPackage package;
			if(null == Package || !Package.TryGetTarget(out package))
			{
				return;
			}
            package.ReleaseAIModel(aiModel);
		}

		public async Task<SVsServiceProvider> GetServiceProviderAsync()
		{
			return await GetServiceAsync(typeof(SVsServiceProvider)) as SVsServiceProvider;
		}

		public IVsTextManager GetTextManager()
		{
			return ToolkitPackage.GetGlobalService(typeof(SVsTextManager)) as IVsTextManager;
		}

		public async Task<CompletionModel> GetCompletionModelAsync()
		{
			if(null != completionModel_)
			{
				return completionModel_;
			}
			completionModel_ = await CompletionModel.InitializeAsync();
			return completionModel_;
		}

		static private WeakReference<HandyToolsPackage> package_;
		private EnvDTE80.DTE2 dte2_;
		private SVsRunningDocumentTable runningDocumentTable_;
		private RunningDocTableEvents runningDocTableEvents_;
		private EnvDTE.SolutionEvents solutionEvents_;
		private EnvDTE.ProjectItemsEvents projectItemsEvents_;
		private object lockObject_ = new object();
		private List<ModelOpenAI> aiModels_ = new List<ModelOpenAI>();
		private CompletionModel completionModel_;

		/// <summary>
		/// Initialization of the package; this method is called right after the package is sited, so this is the place
		/// where you can put all the initialization code that rely on services provided by VisualStudio.
		/// </summary>
		/// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
		/// <param name="progress">A provider for progress updates.</param>
		/// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			// When initialized asynchronously, the current thread may be a background thread at this point.
			// Do any initialization that requires the UI thread after switching to the UI thread.
			await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
			await this.RegisterCommandsAsync();
			this.RegisterToolWindows();

			package_ = new WeakReference<HandyToolsPackage>(this);

			runningDocumentTable_ = await GetServiceAsync(typeof(SVsRunningDocumentTable)) as SVsRunningDocumentTable;
			runningDocTableEvents_ = new RunningDocTableEvents(this);

			dte2_ = await GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
			if (null != dte2_)
			{
				solutionEvents_ = dte2_.Events.SolutionEvents;
				//solutionEvents_.Opened += OnSolutionOpened;

				projectItemsEvents_ = dte2_.Events.SolutionItemsEvents;
				//projectItemsEvents_.ItemAdded += OnProjectItemChanged;
				//projectItemsEvents_.ItemRemoved += OnProjectItemChanged;
				//projectItemsEvents_.ItemRenamed += OnProjectItemRenamed;
			}
		}

		//private void OnSolutionOpened()
		//{
		//}

		//private void OnProjectItemChanged(ProjectItem projectItem)
		//{
		//}

		//private void OnProjectItemRenamed(ProjectItem projectItem, string oldName)
		//{
		//}
#endregion
	}
}
