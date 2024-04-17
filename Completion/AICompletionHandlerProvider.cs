using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace HandyTools.Completion
{
	[Export(typeof(IVsTextViewCreationListener))]
	[Name("AI completion handler")]
	[ContentType("C/C++")]
	[TextViewRole(PredefinedTextViewRoles.Editable)]
	internal class AICompletionHandlerProvider : IVsTextViewCreationListener
	{
		[Import]
		internal IVsEditorAdaptersFactoryService AdapterService = null;
		[Import]
		internal ICompletionBroker CompletionBroker { get; set; }
		[Import]
		internal SVsServiceProvider ServiceProvider { get; set; }

		public void VsTextViewCreated(IVsTextView textViewAdapter)
		{
			ITextView textView = AdapterService.GetWpfTextView(textViewAdapter);
			if (textView == null)
				return;

			Func<AICompletionCommandHandler> createCommandHandler = delegate () { return new AICompletionCommandHandler(textViewAdapter, textView, this); };
			textView.Properties.GetOrCreateSingletonProperty(createCommandHandler);
		}
	}
}
