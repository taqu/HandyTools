using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace HandyTools.Completion
{
	internal class AICompletionSource : ICompletionSource
	{
		public AICompletionSource(AICompletionSourceProvider sourceProvider, ITextBuffer textBuffer)
		{
			sourceProvider_ = sourceProvider;
			textBuffer_ = textBuffer;
		}

		public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
		{
		}

		public void Dispose()
		{
			if (!disposed_)
			{
				GC.SuppressFinalize(this);
				disposed_ = true;
			}
		}

		private bool disposed_;
		private AICompletionSourceProvider sourceProvider_;
		private ITextBuffer textBuffer_;
	}
}
