using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
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
    internal class InlineGrayTextTagger : ITagger<IntraTextAdornmentTag>
    {
        protected readonly IWpfTextView view_;
        protected SnapshotSpan currentSpan_;
        private Brush grayBrush_;
        private StackPanel stackPanel_;
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public InlineGrayTextTagger(IWpfTextView view)
        {
            view_ = view;
            grayBrush_ = new SolidColorBrush(Colors.Gray);
            stackPanel_ = new StackPanel();
        }

        public void UpdateAdornment(UIElement text)
        {
            ClearAdornment();
            stackPanel_.Children.Add(text);
            stackPanel_.UpdateLayout();
        }

        public void ClearAdornment()
        {
            stackPanel_.Children.Clear();
            stackPanel_ = new StackPanel();
        }

        public void FormatText(TextRunProperties props)
        {
            if (props == null) {
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
            try
            {
                if (!view_.TextViewLines.IsValid) {
                    return;
                }
                SnapshotPoint changeStart = view_.TextViewLines.FirstVisibleLine.Start;
                SnapshotPoint changeEnd = view_.TextViewLines.LastVisibleLine.Start;

                ITextSnapshotLine startLine = view_.TextSnapshot.GetLineFromPosition(changeStart);
                ITextSnapshotLine endLine = view_.TextSnapshot.GetLineFromPosition(changeEnd);

                SnapshotSpan span = new SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak)
                                .TranslateTo(targetSnapshot: view_.TextBuffer.CurrentSnapshot, SpanTrackingMode.EdgePositive);

                TagsChanged(this, new SnapshotSpanEventArgs(new SnapshotSpan(span.Start, span.End)));
            }
            catch (Exception e)
            {
                Log.Output(e.Message);
            }
        }

        public virtual IEnumerable<ITagSpan<IntraTextAdornmentTag>>
        GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (stackPanel_.Children.Count == 0) { yield break; }

            ITextSnapshot requestedSnapshot = spans[0].Snapshot;
            double width = view_.FormattedLineSource.ColumnWidth *
                           ((stackPanel_.Children[0] as TextBlock).Inlines.First() as Run).Text.Length;
            double height = view_.LineHeight;

            stackPanel_.Measure(new Size(width, height));
            stackPanel_.MaxHeight = height;
            stackPanel_.MinHeight = height;
            stackPanel_.MinWidth = width;
            stackPanel_.MaxWidth = width;
            ITextViewLine caretLine = view_.Caret.ContainingTextViewLine;
            SnapshotPoint point = view_.Caret.Position.BufferPosition.TranslateTo(
                requestedSnapshot, PointTrackingMode.Positive);
            ITextSnapshotLine line = requestedSnapshot.GetLineFromPosition(point);
            SnapshotSpan span = new SnapshotSpan(point, point);

            IntraTextAdornmentTag tag = new IntraTextAdornmentTag(stackPanel_, null, PositionAffinity.Successor);
            yield return new TagSpan<IntraTextAdornmentTag>(span, tag);
        }
    }

    [Export(contractType: typeof(IViewTaggerProvider))]
    [TagType(typeof(IntraTextAdornmentTag))]
    [ContentType("text")]
    internal class InlineTaggerProvider : IViewTaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            Func<ITagger<T>> sc = delegate ()
            {
                return new InlineGrayTextTagger((IWpfTextView)textView) as ITagger<T>;
            };
            return buffer.Properties.GetOrCreateSingletonProperty(typeof(InlineGrayTextTagger), sc);
        }
    }

}
