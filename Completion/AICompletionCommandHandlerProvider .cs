using Community.VisualStudio.Toolkit;
using EnvDTE;
using HandyTools.Commands;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HandyTools.Completion
{
	[Export(typeof(IVsTextViewCreationListener))]
	[Name("AICompletionHandler")]
	[ContentType("text")]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	internal class AICompletionCommandHandlerProvider : IVsTextViewCreationListener
	{
		[Import]
		internal IVsEditorAdaptersFactoryService AdapterService = null;
		[Import]
		internal ICompletionBroker CompletionBroker { get; set; }
		[Import]
		internal SVsServiceProvider ServiceProvider { get; set; }

		[Import]
		internal ITextDocumentFactoryService documentFactory = null;

		public void VsTextViewCreated(IVsTextView textViewAdapter)
		{
			ITextView textView = AdapterService.GetWpfTextView(textViewAdapter);
			if(null == textView)
			{
				return;
			}
			Func<AICompletionCommandHandler> createCommandHandler = delegate () { return new AICompletionCommandHandler(textViewAdapter, textView, this); };
			textView.Properties.GetOrCreateSingletonProperty(createCommandHandler);
		}
	}
}
