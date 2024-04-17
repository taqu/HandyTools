using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace HandyTools.Completion
{
	[Export(typeof(ICompletionSourceProvider))]
	[ContentType("C/C++")]
	[Name("AI completion")]
	internal class AICompletionSourceProvider : ICompletionSourceProvider
	{
		[Import]
		internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

		public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
		{
			return new AICompletionSource(this, textBuffer);
		}
	}
}
