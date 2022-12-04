using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;

namespace HandyTools
{
    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    //[Guid("5a12f964-ce26-4716-bc22-31a1bb7f76f0")]
    [Guid("C35F72A7-2F86-48CC-A833-4F7E2CB1DA92")]
    public class SearchWindow : ToolWindowPane
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SearchWindow"/> class.
        /// </summary>
        public SearchWindow() : base(null)
        {
            this.Caption = "HandyTools SearchWindow";

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            this.Content = new SearchWindowControl();
        }
    }
}
