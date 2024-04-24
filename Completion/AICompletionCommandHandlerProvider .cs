using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandyTools.Completion
{
	[Export(typeof(IVsTextViewCreationListener))]
	[Name("AI completion handler")]
	[ContentType("code")]
	[TextViewRole(PredefinedTextViewRoles.Editable)]
	internal class AICompletionCommandHandlerProvider : IVsTextViewCreationListener
	{
		[Import]
		internal IVsEditorAdaptersFactoryService AdapterService = null;
		[Import]
		internal ICompletionBroker CompletionBroker { get; set; }
		[Import]
		internal SVsServiceProvider ServiceProvider { get; set; }

		[Import]
		internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

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
