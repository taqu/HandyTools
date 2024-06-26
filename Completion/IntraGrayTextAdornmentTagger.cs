using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;

namespace HandyTools.Completion
{
	internal class IntraGrayTextAdornmentTagger : ITagger<IntraTextAdornmentTag>
	{
		protected readonly IWpfTextView view_;
		protected SnapshotSpan currentSpan_ = default;
		private Brush grayBrush_;
		private StackPanel stackPanel_;
		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public IntraGrayTextAdornmentTagger(IWpfTextView view)
		{
			view_ = view;
			grayBrush_ = new SolidColorBrush(Colors.Gray);
			stackPanel_ = new StackPanel();
		}

		public void UpdateAdornment(UIElement text)
		{
			ClearAdornment();
			stackPanel_.Children.Add(text);
			stackPanel_.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
			stackPanel_.UpdateLayout();
		}

		public void ClearAdornment()
		{
			stackPanel_.Children.Clear();
			stackPanel_ = new StackPanel();
		}

		public void FormatText(TextRunProperties props)
		{
			if (props == null)
			{
				return;
			}
			foreach (TextBlock block in stackPanel_.Children)
			{
				block.FontFamily = props.Typeface.FontFamily;
				block.FontSize = props.FontRenderingEmSize;
			}
		}

		public void MarkDirty()
		{
			SnapshotPoint changeStart = view_.TextViewLines.FirstVisibleLine.Start;
			SnapshotPoint changeEnd = view_.TextViewLines.LastVisibleLine.Start;

			ITextSnapshotLine startLine = view_.TextSnapshot.GetLineFromPosition(changeStart);
			ITextSnapshotLine endLine = view_.TextSnapshot.GetLineFromPosition(changeEnd);

			SnapshotSpan span = new SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak).
				TranslateTo(targetSnapshot: view_.TextBuffer.CurrentSnapshot, SpanTrackingMode.EdgePositive);

			TagsChanged(this, new SnapshotSpanEventArgs(new SnapshotSpan(span.Start, span.End)));
		}

		public virtual IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
		{
			if (stackPanel_.Children.Count <= 0)
			{
				yield break;
			}
			ITextSnapshot requestedSnapshot = spans[0].Snapshot;
			double width = view_.FormattedLineSource.ColumnWidth * ((stackPanel_.Children[0] as TextBlock).Inlines.First() as Run).Text.Length;
			stackPanel_.Measure(new Size(width, double.PositiveInfinity));
			stackPanel_.MinWidth = width;
			stackPanel_.MaxWidth = width;
			Microsoft.VisualStudio.Text.Formatting.ITextViewLine caretLine = view_.Caret.ContainingTextViewLine;
			SnapshotPoint point = view_.Caret.Position.BufferPosition.TranslateTo(requestedSnapshot, PointTrackingMode.Positive);
			ITextSnapshotLine line = requestedSnapshot.GetLineFromPosition(point);
			SnapshotSpan span = new SnapshotSpan(point, point);

			IntraTextAdornmentTag tag = new IntraTextAdornmentTag(stackPanel_, null, PositionAffinity.Successor);
			yield return new TagSpan<IntraTextAdornmentTag>(span, tag);
		}
	}

	[Export(typeof(IViewTaggerProvider))]
	[TagType(typeof(IntraTextAdornmentTag))]
	[ContentType("text")]
	internal class IntraGrayTextAdornmentTaggerProvider : IViewTaggerProvider
	{
		// Disable "Field is never assigned to..." and "Field is never used" compiler's warnings. Justification: the field is used by MEF.
#pragma warning disable 649, 169

		[Export(typeof(AdornmentLayerDefinition))]
		[Name("HandyToolsAdornmentLayer")]
		[Order(After = PredefinedAdornmentLayers.Caret)]
		private AdornmentLayerDefinition editorAdornmentLayer;

#pragma warning restore 649, 169

		//create a single tagger for each buffer.
		//the InlineGreyTextTagger displays grey text inserted between user text in the editor.
		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
		{
			return buffer.Properties.GetOrCreateSingletonProperty(
				typeof(IntraGrayTextAdornmentTagger),
				()=>{ return new IntraGrayTextAdornmentTagger((IWpfTextView)textView) as ITagger<T>;});
		}
	}
}
