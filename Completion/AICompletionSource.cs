using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
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
			List<string> strList = new List<string>();
			strList.Add("addition");
			strList.Add("adaptation");
			strList.Add("subtraction");
			strList.Add("summation");
			completions_ = new List<Microsoft.VisualStudio.Language.Intellisense.Completion>();
			foreach (string str in strList)
			{
				completions_.Add(new Microsoft.VisualStudio.Language.Intellisense.Completion(str, str, str, null, null));
			}

			completionSets.Add(new CompletionSet(
				"Tokens",    //the non-localized title of the tab
				"Tokens",    //the display title of the tab
				FindTokenSpanAtPosition(session.GetTriggerPoint(textBuffer_),
					session),
				completions_,
				null));
		}

		private ITrackingSpan FindTokenSpanAtPosition(ITrackingPoint point, ICompletionSession session)
		{
			SnapshotPoint currentPoint = (session.TextView.Caret.Position.BufferPosition) - 1;
			ITextStructureNavigator navigator = sourceProvider_.NavigatorService.GetTextStructureNavigator(textBuffer_);
			TextExtent extent = navigator.GetExtentOfWord(currentPoint);
			return currentPoint.Snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);
		}

		public void Dispose(bool disposing)
		{
			if (disposed_)
			{
				return;
			}
			textBuffer_ = null;
			disposed_ = true;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private bool disposed_;
		private ITextBuffer textBuffer_;
		private List<Microsoft.VisualStudio.Language.Intellisense.Completion> completions_;
		private AICompletionSourceProvider sourceProvider_;
	}
}
