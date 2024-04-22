using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace HandyTools.Completion
{
	[Export(typeof(ICompletionSourceProvider))]
	[ContentType("code")]
	[Name("AI completion")]
	internal class AICompletionSourceProvider : ICompletionSourceProvider
	{
		public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
		{
			return new AICompletionSource(this, textBuffer);
		}
	}
}
