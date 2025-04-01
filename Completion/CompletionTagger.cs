using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Documents;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using System.Text.RegularExpressions;
using System.Windows.Media.TextFormatting;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Formatting;

namespace HandyTools.Completion
{

    internal sealed class CompletionTagger : ITagger<CompletionTag>
    {
        public const string AunamentLayerName = "HandyToolsAdornmentLayer";

        /// panel with multiline grey text
        private StackPanel stackPanel_;

        /// used to set the colour of the grey text
        private Brush grayBrush_;

        /// used to set the colour of text that overlaps with the users text
        private Brush transparentBrush_;

        /// contains the editor text and OnChange triggers on any text changes
        ITextBuffer textBuffer_;

        /// current editor display, immutable data
        ITextSnapshot textSnapshot_;

        /// the editor display object
        IWpfTextView textView_;

        /// contains the grey text
        private IAdornmentLayer adornmentLayer_;

        /// true if a suggestion should be shown
        private bool showSuggestion_ = false;
        private bool isTextInsertion_ = false;

        ///  line number the suggestion should be displayed at
        private int currentTextLineNumber_;
        private int suggestionIndex_;
        private int insertionPoint_;
        private int userIndex_;
        private String userEndingText_;
        private String virtualText_ = "";

        /// suggestion to display
        /// first string is to match against second item: array is for formatting
        private static Tuple<String, String[]> suggestion_ = null;

        private InlineGrayTextTagger GetTagger()
        {
            Type key = typeof(InlineGrayTextTagger);
            var props = textView_.TextBuffer.Properties;
            if (props.ContainsProperty(key))
            {
                return props.GetProperty<InlineGrayTextTagger>(key);
            }
            else
            {
                return null;
            }
        }

        public bool SetSuggestion(String newSuggestion, int caretPoint)
        {
            try
            {
                // If this isn't the most up-to-date version of the buffer, then ignore it for now (we'll
                newSuggestion = newSuggestion.TrimEnd();
                newSuggestion = newSuggestion.Replace("\r", "");
                int lineNumber = GetCurrentTextLine();

                if (lineNumber < 0)
                {
                    return false;
                }
                ClearSuggestion();

                String untrim = textBuffer_.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();

                virtualText_ = string.Empty;
                if (String.IsNullOrWhiteSpace(untrim) && untrim.Length < caretPoint)
                {
                    virtualText_ = new string(' ', caretPoint - untrim.Length);
                }
                String line = untrim.TrimStart();
                int offset = untrim.Length - line.Length;

                caretPoint = Math.Max(0, caretPoint - offset);

                String combineSuggestion = line + newSuggestion;
                if (0 < line.Length - caretPoint)
                {
                    String currentText = line.Substring(0, caretPoint);
                    combineSuggestion = currentText + newSuggestion;
                    userEndingText_ = line.Substring(caretPoint).Trim();
                    var userIndex = newSuggestion.IndexOf(userEndingText_);

                    if (userIndex < 0) { return false; }
                    userIndex += currentText.Length;

                    this.userIndex_ = userIndex;
                    isTextInsertion_ = true;
                    insertionPoint_ = line.Length - caretPoint;
                }
                else
                {
                    isTextInsertion_ = false;
                }
                string[] suggestionLines = combineSuggestion.Split('\n');
                suggestion_ = new Tuple<String, String[]>(combineSuggestion, suggestionLines);
                return Update();
            }
            catch (Exception e)
            {
                Log.Output(e.Message);
                return false;
            }
        }

        public bool OnSameLine()
        {
            return GetCurrentTextLine() == currentTextLineNumber_;
        }

        private void LostFocus(object sender, EventArgs e)
        {
            try
            {
                ClearSuggestion();
            }
            catch (Exception exception)
            {
                Log.Output(exception.Message);
            }
        }

        public CompletionTagger(IWpfTextView view, ITextBuffer buffer)
        {
            stackPanel_ = new StackPanel();

            textBuffer_ = buffer;
            textSnapshot_ = buffer.CurrentSnapshot;
            view.TextBuffer.Changed += BufferChanged;
            textView_ = view;
            adornmentLayer_ = view.GetAdornmentLayer(CompletionTagger.AunamentLayerName);

            textView_.LayoutChanged += this.OnSizeChanged;

            transparentBrush_ = new SolidColorBrush();
            transparentBrush_.Opacity = 0;
            grayBrush_ = new SolidColorBrush(Colors.Gray);
            view.LostAggregateFocus += LostFocus;
        }

        public bool IsSuggestionActive()
        {
            return showSuggestion_;
        }

        public String GetSuggestion()
        {
            if (suggestion_ != null && showSuggestion_)
            {
                return suggestion_.Item1;
            }
            else
            {
                return string.Empty;
            }
        }

        // This an iterator that is used to iterate through all of the test tags
        // tags are like html tags they mark places in the view to modify how those sections look
        // Testtag is a tag that tells the editor to add empty space
        public IEnumerable<ITagSpan<CompletionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            var currentSuggestion = suggestion_;
            if (!showSuggestion_ || currentSuggestion == null || currentSuggestion.Item2.Length <= 1)
            {
                yield break;
            }

            SnapshotSpan entire = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End)
                                      .TranslateTo(textSnapshot_, SpanTrackingMode.EdgeExclusive);
            ITextSnapshot currentSnapshot = spans[0].Snapshot;

            SnapshotSpan line = currentSnapshot.GetLineFromLineNumber(currentTextLineNumber_).Extent;
            SnapshotSpan span = new SnapshotSpan(line.End, line.End);

            ITextSnapshotLine snapshotLine = currentSnapshot.GetLineFromLineNumber(currentTextLineNumber_);

            double height = textView_.LineHeight * (currentSuggestion.Item2.Length - 1);
            double lineHeight = 0;

            if (String.IsNullOrEmpty(line.GetText())) { lineHeight = textView_.LineHeight; }
            yield return new TagSpan<CompletionTag>(
                span,
                new CompletionTag(
                    0, 0, lineHeight, 0, height, PositionAffinity.Predecessor, stackPanel_, this));
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        // triggers when the editor text buffer changes
        private void BufferChanged(object sender, TextContentChangedEventArgs e)
        {
            try
            {
                // If this isn't the most up-to-date version of the buffer, then ignore it for now (we'll
                // eventually get another change event).
                if (e.After != textBuffer_.CurrentSnapshot)
                {
                    return;
                }
                Update();
            }
            catch (Exception exception)
            {
                Log.Output(exception.Message);
            }
        }

        private TextRunProperties GetTextFormat()
        {
            IWpfTextViewLine line = textView_.TextViewLines.FirstVisibleLine;
            return line.GetCharacterFormatting(line.Start);
        }

        // used to set formatting of the displayed multi lines
        public void FormatText(TextBlock block)
        {
            // var pos = snapshot.GetLineFromLineNumber(currentLineN).Start;

            IWpfTextViewLine line = textView_.TextViewLines.FirstVisibleLine;
            TextRunProperties format = line.GetCharacterFormatting(line.Start);
            if (format != null)
            {
                block.FontFamily = format.Typeface.FontFamily;
                block.FontSize = format.FontRenderingEmSize;
            }
        }

        private String ConvertTabsToSpaces(string text)
        {
            int tabSize = textView_.Options.GetTabSize();
            return Regex.Replace(text, "\t", new string(' ', tabSize));
        }
        private void FormatTextBlock(TextBlock textBlock)
        {
            textBlock.FontStyle = FontStyles.Normal;
            textBlock.FontWeight = FontWeights.Normal;
        }

        TextBlock CreateTextBox(string text, Brush textColour)
        {
            TextBlock textBlock = new TextBlock();
            textBlock.Inlines.Add(item: new Run(text) { Foreground = textColour });
            FormatTextBlock(textBlock);
            return textBlock;
        }

        void AddSuffixTextBlocks(int start, string line, string userText)
        {
            if (isTextInsertion_ && line.Length < start)
            {
                return;
            }
            int emptySpaceLength = userText.Length - userText.TrimStart().Length;
            string emptySpace = ConvertTabsToSpaces(userText.Substring(0, emptySpaceLength));
            string editedUserText = emptySpace + userText.TrimStart();
            if (isTextInsertion_)
            {
                editedUserText = emptySpace + line.Substring(0, start);
            }
            string remainder = line.Substring(start);
            TextBlock textBlock = new TextBlock();
            textBlock.Inlines.Add(item: new Run(editedUserText) { Foreground = transparentBrush_ });
            textBlock.Inlines.Add(item: new Run(remainder) { Foreground = grayBrush_ });

            stackPanel_.Children.Add(textBlock);
        }

        void AddInsertionTextBlock(int start, int end, string line)
        {
            if (line.Length <= suggestionIndex_ || end < start)
            {
                return;
            }
            try
            {
                string remainder = line.Substring(start, end - start);
                TextBlock textBlock = CreateTextBox(remainder, grayBrush_);
                InlineGrayTextTagger inlineTagger = GetTagger();
                if (inlineTagger == null)
                {
                    return;
                }
                inlineTagger.UpdateAdornment(textBlock);
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }
        }

        // Updates the grey text
        public void UpdateAdornment(IWpfTextView view, string userText, int suggestionStart)
        {
            try
            {
                stackPanel_.Children.Clear();
                InlineGrayTextTagger inlineTagger = GetTagger();
                if (inlineTagger == null)
                {
                    return;
                }
                inlineTagger.ClearAdornment();
                for (int i = suggestionStart; i < suggestion_.Item2.Length; ++i)
                {
                    string line = suggestion_.Item2[i];
                    if (0 != i)
                    {
                        stackPanel_.Children.Add(CreateTextBox(line, grayBrush_));
                        continue;
                    }

                    int offset = line.Length - line.TrimStart().Length;

                    if (isTextInsertion_ && suggestionIndex_ < userIndex_)
                    {
                        if (suggestionIndex_ > 0 && suggestionIndex_ < line.Length && char.IsWhiteSpace(line[suggestionIndex_ - 1]) &&
                            userText.Length > insertionPoint_ + 1 &&
                            !char.IsWhiteSpace(userText[userText.Length - insertionPoint_ - 1]))
                        {
                            suggestionIndex_--;
                        }
                        AddInsertionTextBlock(suggestionIndex_ + offset, userIndex_, line);
                        if (line.Length > userIndex_ + 1)
                        {
                            AddSuffixTextBlocks(
                                userIndex_ + userEndingText_.Trim().Length, line, userText);
                        }
                        else
                        {
                            stackPanel_.Children.Add(CreateTextBox("", grayBrush_));
                        }
                    }
                    else
                    {
                        if (String.IsNullOrEmpty(line))
                        {
                            stackPanel_.Children.Add(CreateTextBox("", grayBrush_));
                        }
                        else
                        {
                            String suggestedLine =
                                virtualText_.Length > 0 ? virtualText_ + line.TrimStart() : line;
                            AddSuffixTextBlocks(userText.Length > 0 ? suggestionIndex_ + offset : 0,
                                                suggestedLine,
                                                userText);
                        }
                    }
                }

                adornmentLayer_.RemoveAllAdornments();

                // usually only happens the moment a bunch of text has rentered such as an undo operation
                ITextSnapshotLine snapshotLine =
                    view.TextSnapshot.GetLineFromLineNumber(currentTextLineNumber_);
                Microsoft.VisualStudio.Text.Formatting.TextBounds start = view.TextViewLines.GetCharacterBounds(snapshotLine.Start);

                // Place the image in the top left hand corner of the line
                Canvas.SetLeft(stackPanel_, start.Left);
                Canvas.SetTop(stackPanel_, start.TextTop);
                SnapshotSpan span = snapshotLine.Extent;
                // Add the image to the adornment layer and make it relative to the viewport
                adornmentLayer_.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, stackPanel_, null);
            }
            catch (Exception e)
            {
                Log.Output(e.Message);
            }
        }

        // Adds grey text to display
        private void OnSizeChanged(object sender, EventArgs e)
        {

            try
            {
                if (!showSuggestion_) {
                    return;
                }

                foreach (TextBlock block in stackPanel_.Children)
                {
                    FormatText(block);
                }

                ITextSnapshotLine snapshotLine = textView_.TextSnapshot.GetLineFromLineNumber(currentTextLineNumber_);

                Microsoft.VisualStudio.Text.Formatting.TextBounds start = textView_.TextViewLines.GetCharacterBounds(snapshotLine.Start);

                InlineGrayTextTagger inlineTagger = GetTagger();
                if (inlineTagger == null) {
                    return;
                }
                inlineTagger.FormatText(GetTextFormat());

                if (stackPanel_.Children.Count > 0)
                {
                    adornmentLayer_.RemoveAllAdornments();

                    SnapshotSpan span = snapshotLine.Extent;

                    // Place the image in the top left hand corner of the line
                    Canvas.SetLeft(stackPanel_, start.Left);
                    Canvas.SetTop(element: stackPanel_, start.TextTop);
                    double diff = start.Top - start.TextTop;
#if DEBUG
                    Log.Output("Top = " + (start.Top.ToString()) +
                                " TextTop = " + (start.TextTop.ToString()) + " bottom " +
                                (start.TextBottom.ToString()));
#endif
                    // Add the image to the adornment layer and make it relative to the viewport
                    adornmentLayer_.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, stackPanel_, null);
                }
            }
            catch (Exception exception)
            {
                Log.Output(exception.Message);
            }
        }

        // Gets the line number of the caret
        int GetCurrentTextLine()
        {
            CaretPosition caretPosition = textView_.Caret.Position;
            SnapshotPoint? textPoint = caretPosition.Point.GetPoint(textBuffer_, caretPosition.Affinity);
            if (!textPoint.HasValue) {
                return -1;
            }
            return textBuffer_.CurrentSnapshot.GetLineNumberFromPosition(textPoint.Value);
        }

        // update multiline data
        public bool Update()
        {

            if (suggestion_ == null) {
                return false;
            }

            int textLineNumber = GetCurrentTextLine();

            if (textLineNumber < 0) {
                return false;
            }

            ITextSnapshot newSnapshot = textBuffer_.CurrentSnapshot;
            this.textSnapshot_ = newSnapshot;

            String untrimLine = newSnapshot.GetLineFromLineNumber(textLineNumber).GetText();
            String line = untrimLine.TrimStart();

            // get line carat is on
            // if suggestion matches line (possibly including preceding lines)
            //   show suggestion
            // else
            //   clear suggestions

            int newIndex = StringUtils.CheckSuggestion(suggestion_.Item1, line, isTextInsertion_, insertionPoint_);
            if (newIndex >= 0)
            {
                this.currentTextLineNumber_ = textLineNumber;
                this.suggestionIndex_ = newIndex;
                ShowSuggestion(untrimLine, 0);
                return true;
            }
            else
            {
                ClearSuggestion();
            }

            return false;
        }

        // Adds the grey text to the file replacing current line in the process
        public bool CompleteText()
        {
            try
            {
                if (!showSuggestion_ || suggestion_ == null)
                {
                    return false;
                }

                String untrimLine = this.textSnapshot_.GetLineFromLineNumber(currentTextLineNumber_).GetText();
                String line = untrimLine.Trim();

                int suggestionLineNumber = StringUtils.CheckSuggestion(suggestion_.Item1, line, isTextInsertion_, insertionPoint_);
                if (0<=suggestionLineNumber)
                {
                    int diff = untrimLine.Length - untrimLine.TrimStart().Length;
                    string whitespace =
                        String.IsNullOrWhiteSpace(untrimLine) ? "" : untrimLine.Substring(0, diff);
                    ReplaceText(whitespace + suggestion_.Item1, currentTextLineNumber_);
                    return true;
                }

            }
            catch (Exception e)
            {
                Log.Output(e.Message);
            }

            return false;
        }

        // replaces text in the editor
        void ReplaceText(string text, int lineNumber)
        {
            if (textView_.Options.GetOptionValue(DefaultOptions.NewLineCharacterOptionId) == "\r\n")
            {
                text = text.Replace("\n", "\r\n");
            }
            int oldLineNumber = lineNumber + suggestion_.Item2.Length - 1;
            bool insertion = isTextInsertion_ && suggestion_.Item2.Length == 1;
            int oldUserIndex = userIndex_;
            int offset = text.Length - suggestion_.Item1.Length;
            ClearSuggestion();
            SnapshotSpan span = this.textSnapshot_.GetLineFromLineNumber(lineNumber).Extent;
            ITextEdit edit = textView_.TextBuffer.CreateEdit();
            int spanLength = span.Length;
            edit.Replace(span, text);
            ITextSnapshot newSnapshot = edit.Apply();

            if (spanLength == 0 && 0<text.Length)
            {
                textView_.Caret.MoveTo(newSnapshot.GetLineFromLineNumber(oldLineNumber).End);
            }

            if (insertion)
            {
                textView_.Caret.MoveTo(newSnapshot.GetLineFromLineNumber(oldLineNumber).Start.Add(oldUserIndex + offset));
            }
        }

        // sets up the suggestion for display
        void ShowSuggestion(String text, int suggestionLineStart)
        {
            UpdateAdornment(textView_, text, suggestionLineStart);

            showSuggestion_ = true;
            MarkDirty();
        }

        // removes the suggestion
        public void ClearSuggestion()
        {
            try
            {
                if (!showSuggestion_)
                {
                    return;
                }
                InlineGrayTextTagger inlineTagger = GetTagger();
                if (inlineTagger == null) {
                    return;
                }
                inlineTagger.ClearAdornment();
                inlineTagger.MarkDirty();
                suggestion_ = null;
                adornmentLayer_.RemoveAllAdornments();
                showSuggestion_ = false;

                MarkDirty();

            }
            catch (Exception e)
            {
                Log.Output(e.Message);
            }
        }

        // triggers refresh of the screen
        void MarkDirty()
        {
            try
            {
                InlineGrayTextTagger inlineTagger = GetTagger();
                if (inlineTagger == null) {
                    return;
                }
                inlineTagger.MarkDirty();
                ITextSnapshot newSnapshot = textBuffer_.CurrentSnapshot;
                this.textSnapshot_ = newSnapshot;

                if (textView_.TextViewLines == null)
                {
                    return;
                }
                if (!textView_.TextViewLines.IsValid)
                {
                    return;
                }

                SnapshotPoint changeStart = textView_.TextViewLines.FirstVisibleLine.Start;
                SnapshotPoint changeEnd = textView_.TextViewLines.LastVisibleLine.Start;

                ITextSnapshotLine startLine = textView_.TextSnapshot.GetLineFromPosition(changeStart);
                ITextSnapshotLine endLine = textView_.TextSnapshot.GetLineFromPosition(changeEnd);

                SnapshotSpan span = new SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak)
                    .TranslateTo(targetSnapshot: newSnapshot, SpanTrackingMode.EdgePositive);

                // lines we are marking dirty
                // currently all of them for simplicity
                if (this.TagsChanged != null) {
                    TagsChanged(this, new SnapshotSpanEventArgs(span));
                }
            }
            catch (Exception e) {
                Log.Output(e.Message);
            }
        }
    }

    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(CompletionTag))]
    [ContentType("text")]
    internal sealed class SuggestionProvider : IViewTaggerProvider
    {

        [Export(typeof(AdornmentLayerDefinition))]
        [Name(CompletionTagger.AunamentLayerName)]
        [Order(After = PredefinedAdornmentLayers.Caret)]
        private AdornmentLayerDefinition editorAdornmentLayer;

        // create a single tagger for each buffer.
        // the MultilineGreyTextTagger displays the grey text in the editor.
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer)
            where T : ITag
        {
            Func<ITagger<T>> sc = delegate ()
            {
                return new CompletionTagger((IWpfTextView)textView, buffer) as ITagger<T>;
            };
            return buffer.Properties.GetOrCreateSingletonProperty<ITagger<T>>(typeof(CompletionTagger), sc);
        }
    }
}
