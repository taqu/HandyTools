using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO.Packaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

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
    [ProvideService(typeof(SSearchService), IsAsyncQueryable = true)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(HandyToolsPackage.PackageGuidString)]
    [ProvideAutoLoad(UIContextGuids.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideOptionPage(typeof(OptionPageHandyTools), "HandyTools", "General", 0, 0, true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(SearchWindow), Style = VsDockStyle.Tabbed, DockedWidth = 300, Window = "DocumentWell", Orientation = ToolWindowOrientation.Left)]
    //[ProvideToolWindowVisibility(typeof(SearchWindow), /*UICONTEXT_SolutionExists*/"f1536ef8-92ec-443c-9ed7-fdadf150da82")]
    public sealed class HandyToolsPackage : AsyncPackage
    {
        /// <summary>
        /// HandyToolsPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "02e1bb82-aa38-4e20-9504-aa1dac5c5dcb";

        #region Package Members

        public static WeakReference<HandyToolsPackage> Package { get =>package_; }

        public EnvDTE80.DTE2 DTE { get { return dte2_; } }
        public SVsRunningDocumentTable RDT { get { return runningDocumentTable_; } }

        public OptionPageHandyTools Options
        {
            get
            {
                return GetDialogPage(typeof(OptionPageHandyTools)) as OptionPageHandyTools;
            }
        }

        public SettingFile LoadFileSettings(string documentPath)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            return fileSettings_.Load(DTE, documentPath) ? fileSettings_ : null;
        }

        static private WeakReference<HandyToolsPackage> package_;
        private EnvDTE80.DTE2 dte2_;
        private SVsRunningDocumentTable runningDocumentTable_;
        private Microsoft.VisualStudio.OLE.Interop.IServiceProvider servicePorvider_;
        private RunningDocTableEvents runningDocTableEvents_;
        private SolutionEvents solutionEvents_;
        private ProjectItemsEvents projectItemsEvents_;
        private SettingFile fileSettings_ = new SettingFile();

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
            package_ = new WeakReference<HandyToolsPackage>(this);
            dte2_ = await GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
            servicePorvider_ = await GetServiceAsync(typeof(Microsoft.VisualStudio.OLE.Interop.IServiceProvider)) as Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

            runningDocumentTable_ = await GetServiceAsync(typeof(SVsRunningDocumentTable)) as SVsRunningDocumentTable;
            runningDocTableEvents_ = new RunningDocTableEvents(this);

            solutionEvents_ = dte2_.Events.SolutionEvents;
            solutionEvents_.Opened += OnSolutionOpened;

            projectItemsEvents_ = dte2_.Events.SolutionItemsEvents;
            projectItemsEvents_.ItemAdded += OnProjectItemChanged;
            projectItemsEvents_.ItemRemoved += OnProjectItemChanged;
            projectItemsEvents_.ItemRenamed += OnProjectItemRenamed;
            AddService(typeof(SSearchService), CreateSearchServiceAsync);

            ISearchService service = await GetServiceAsync(typeof(SSearchService)) as ISearchService;
            OptionPageHandyTools options = Options;
            if(null != service && null != options && options.EnableSearch) {
                var _ = service.UpdateAsync();
            }
            await SearchWindowCommand.InitializeAsync(this);
        }

        private void OnSolutionOpened()
        {
            OptionPageHandyTools options = Options;
            if(null == options || !options.EnableSearch) {
                return;
            }
            JoinableTaskFactory.Run(async () => {
                ISearchService service = await GetServiceAsync(typeof(SSearchService)) as ISearchService;
                if(null != service) {
                    var _ = service.UpdateAsync();
                }
            });
        }

        private void OnProjectItemChanged(ProjectItem projectItem)
        {
        }

        private void OnProjectItemRenamed(ProjectItem projectItem, string oldName)
        {

        }

        private async Task<object> CreateSearchServiceAsync(IAsyncServiceContainer container, CancellationToken cancellationToken, Type serviceType)
        {
            if(typeof(SSearchService) != serviceType) {
                return null;
            }
            SearchService service = new SearchService(this);
            await service.InitializeAsync(cancellationToken);
            return service;
        }
        #endregion
    }
}
