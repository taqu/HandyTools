global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using HandyTools.Models;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
	[ProvideAutoLoad(UIContextGuids.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
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

		#region Package Members

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

		public SettingFile LoadFileSettings(string documentPath)
		{
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
			fileSettings_.Load(package_, documentPath);
			return fileSettings_;
		}

		public SettingFile LoadFileSettings()
		{
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
			if (null == DTE || null == DTE.Solution)
			{
				return LoadFileSettings(string.Empty);
			}
			return LoadFileSettings(DTE.Solution.FullName);
		}

		public (RefCount<ModelBase>, SettingFile) GetAIModel(TypeModel type)
		{
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
			SettingFile settingFile = LoadFileSettings();
            if (null != aiModel_)
            {
                switch (aiModel_.Get().APIType)
                {
                case Types.TypeAIAPI.OpenAI:
                    if (settingFile.APIType == Types.TypeAIAPI.OpenAI)
                    {
					if (aiModel_.Count <= 0)
					{
							(aiModel_.Get() as ModelOpenAI).Model = settingFile.GetModelName(type);
					}
						aiModel_.AddRef();
                        return (aiModel_, settingFile);
                    }
                    break;
                case Types.TypeAIAPI.Ollama:
                    if (settingFile.APIType == Types.TypeAIAPI.Ollama)
                    {
						if (aiModel_.Count <= 0)
					{
							(aiModel_.Get() as ModelOllama).Model = settingFile.GetModelName(type);
					}
						aiModel_.AddRef();
                        return (aiModel_, settingFile);
                    }
                    break;
                default:
                    return (null, settingFile);
                }
            }
			switch (settingFile.APIType)
			{
				case Types.TypeAIAPI.OpenAI:
					if (string.IsNullOrEmpty(settingFile.ApiKey))
					{
						return (null, settingFile);
					}
					aiModel_ = new RefCount<ModelBase>(new ModelOpenAI(settingFile, type));
					break;
				case Types.TypeAIAPI.Ollama:
					aiModel_ = new RefCount<ModelBase>(new ModelOllama(settingFile, type));
					break;
				default:
					return (null, settingFile);
			}
			aiModel_.AddRef();
			return (aiModel_, settingFile);
		}

		public async Task<SVsServiceProvider> GetServiceProviderAsync()
		{
			return await GetServiceAsync(typeof(SVsServiceProvider)) as SVsServiceProvider;
		}

		public IVsTextManager GetTextManager() {
			return ToolkitPackage.GetGlobalService(typeof(SVsTextManager)) as IVsTextManager;
				}

		static private WeakReference<HandyToolsPackage> package_;
		private EnvDTE80.DTE2 dte2_;
		private SVsRunningDocumentTable runningDocumentTable_;
		private RunningDocTableEvents runningDocTableEvents_;
		private EnvDTE.SolutionEvents solutionEvents_;
		private EnvDTE.ProjectItemsEvents projectItemsEvents_;
		private SettingFile fileSettings_ = new SettingFile();
		private RefCount<ModelBase> aiModel_;

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
