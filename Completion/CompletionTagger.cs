using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;

namespace HandyTools.Completion
{
	internal class CompletionTagger : ITagger<CompletionTag>
	{
		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		private StackPanel stackPanel_;

		private ITextBuffer buffer_;
		private ITextSnapshot snapshot_;
		private IWpfTextView view_;

		private IAdornmentLayer adornmentLayer_;

		private Brush grayBrush_;
		private Brush transparentBrush_;

		private bool showSuggestion_;
		private bool inlineSuggestion_;
		private bool isTextInsertion_;

		private int currentTextLineNumber_;
		private int currentVisualLineN_;
		private int suggestionIndex_;
		private int insertionPoint_;
		private int userIndex_;
		private String userEndingText_;
		private static Tuple<String, String[]> suggestion = null;

		public CompletionTagger(IWpfTextView view, ITextBuffer buffer)
		{
			this.stackPanel_ = new StackPanel();

			this.buffer_ = buffer;
			this.snapshot_ = buffer.CurrentSnapshot;
			this.buffer_.Changed += OnBufferChanged;
			this.view_ = view;
			this.adornmentLayer_ = view.GetAdornmentLayer("HandyToolsAdornmentLayer");

			this.view_.LayoutChanged += OnViewLayoutChanged;

			this.grayBrush_ = new SolidColorBrush(Colors.Gray);
			this.transparentBrush_ = new SolidColorBrush();
			this.transparentBrush_.Opacity = 0;
			this.view_.LostAggregateFocus += OnViewLostAggregateFocus;
			this.view_.Caret.PositionChanged += OnViewCaretUpdate;
		}

		public void SetSuggestion(String newSuggestion, bool inline, int caretPoint)
		{
			ClearSuggestion();
			inlineSuggestion_ = inline;

			int lineN = GetCurrentTextLine();

			if (lineN < 0) return;

			String untrim = buffer_.CurrentSnapshot.GetLineFromLineNumber(lineN).GetText();
			String line = untrim.TrimStart();
			int offset = untrim.Length - line.Length;

			caretPoint = Math.Max(0, caretPoint - offset);

			String combineSuggestion = line + newSuggestion;
			if (line.Length - caretPoint > 0)
			{
				String currentText = line.Substring(0, caretPoint);
				combineSuggestion = currentText + newSuggestion;
				userEndingText_ = line.Substring(caretPoint).TrimEnd();
				var userIndex = newSuggestion.IndexOf(userEndingText_);
				if (userIndex < 0)
				{
					return;
				}
				userIndex += currentText.Length;

				this.userIndex_ = userIndex;
				isTextInsertion_ = true;
				insertionPoint_ = line.Length - caretPoint;
			}
			else
			{
				isTextInsertion_ = false;
			}

			suggestion = new Tuple<String, String[]>(combineSuggestion, combineSuggestion.Split('\n'));
			Update();
		}

		private void OnViewCaretUpdate(object sender, CaretPositionChangedEventArgs e)
		{
			if (showSuggestion_ && GetCurrentTextLine() != currentTextLineNumber_)
			{
				ClearSuggestion();
			}
		}

		private void OnViewLostAggregateFocus(object sender, EventArgs e)
		{
			ClearSuggestion();
		}

		public bool IsSuggestionActive()
		{
			return showSuggestion_;
		}

		public String GetSuggestion()
		{
			if (suggestion != null && showSuggestion_)
			{
				return suggestion.Item1;
			}
			else
			{
				return string.Empty;
			}
		}

		private IntraGrayTextAdornmentTagger GetTagger()
		{
			Type key = typeof(IntraGrayTextAdornmentTagger);
			Microsoft.VisualStudio.Utilities.PropertyCollection props = view_.TextBuffer.Properties;
			if (props.ContainsProperty(key))
			{
				return props.GetProperty<IntraGrayTextAdornmentTagger>(key);
			}
			else
			{
				return null;
			}
		}

		public IEnumerable<ITagSpan<CompletionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
		{
			Tuple<string, string[]> currentSuggestion = suggestion;
			if (!showSuggestion_ || currentSuggestion == null || currentSuggestion.Item2.Length <= 1)
			{
				yield break;
			}

			SnapshotSpan entire = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End).TranslateTo(snapshot_, SpanTrackingMode.EdgeExclusive);
			ITextSnapshot currentSnapshot = spans[0].Snapshot;

			SnapshotSpan line = currentSnapshot.GetLineFromLineNumber(currentTextLineNumber_).Extent;
			SnapshotSpan span = new SnapshotSpan(line.End, line.End);

			ITextSnapshotLine snapshotLine = currentSnapshot.GetLineFromLineNumber(currentTextLineNumber_);

			double height = view_.LineHeight * (currentSuggestion.Item2.Length - 1);

			if (currentTextLineNumber_ == 0 && currentSnapshot.Lines.Count() == 1 && String.IsNullOrEmpty(currentSnapshot.GetText()))
			{
				height += view_.LineHeight;
			}

			yield return new TagSpan<CompletionTag>(span, new CompletionTag(0, 0, 0, 0, height, PositionAffinity.Predecessor, stackPanel_, this));
		}

		private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
		{
			if (e.After != buffer_.CurrentSnapshot)
			{
				return;
			}
			Update();
		}

		private TextRunProperties GetTextFormat()
		{
			Microsoft.VisualStudio.Text.Formatting.IWpfTextViewLine line = view_.TextViewLines.FirstVisibleLine;
			return line.GetCharacterFormatting(line.Start);
		}

		public void FormatText(TextBlock block)
		{
			var line = view_.TextViewLines.FirstVisibleLine;
			var format = line.GetCharacterFormatting(line.Start);
			if (format != null)
			{
				block.FontFamily = format.Typeface.FontFamily;
				block.FontSize = format.FontRenderingEmSize;
			}
		}

		private String ConvertTabsToSpaces(string text)
		{
			int tabSize = view_.Options.GetTabSize();
			return Regex.Replace(text, "\t", new string(' ', tabSize));
		}

		private void FormatTextBlock(TextBlock textBlock)
		{
			textBlock.FontStyle = FontStyles.Normal;
			textBlock.FontWeight = FontWeights.Normal;
		}

		private TextBlock CreateTextBox(string text, Brush textColour)
		{
			TextBlock textBlock = new TextBlock();
			textBlock.Inlines.Add(item: new Run(text) { Foreground = textColour });
			FormatTextBlock(textBlock);
			return textBlock;
		}

		private void AddSuffixTextBlocks(int start, string line, string userText)
		{
			if (line.Length <= suggestionIndex_)
				return;

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

		private void AddInsertionTextBlock(int start, int end, string line)
		{
			if (line.Length <= suggestionIndex_)
				return;

			TextBlock textBlock = new TextBlock();
			string remainder = line.Substring(start, end - start);
			GetTagger().UpdateAdornment(CreateTextBox(remainder, grayBrush_));
		}

		public void UpdateAdornment(IWpfTextView view, string userText, int suggestionStart)
		{
			stackPanel_.Children.Clear();
			GetTagger().ClearAdornment();
			for (int i = suggestionStart; i < suggestion.Item2.Length; i++)
			{
				string line = suggestion.Item2[i];

				if (i == 0)
				{
					int offset = line.Length - line.TrimStart().Length;

					if (isTextInsertion_ && suggestionIndex_ < userIndex_)
					{
						if (0<suggestionIndex_ && char.IsWhiteSpace(line[suggestionIndex_ - 1]) && !char.IsWhiteSpace(userText[userText.Length - insertionPoint_ - 1]))
						{
							--suggestionIndex_;
						}
						AddInsertionTextBlock(suggestionIndex_ + offset, userIndex_, line);
						if (userIndex_ + 1 < line.Length)
						{
							AddSuffixTextBlocks(userIndex_ + userEndingText_.Trim().Length, line, userText);
						}
					}
					else
					{
						AddSuffixTextBlocks(userText.Length > 0 ? suggestionIndex_ + offset : 0, line, userText);
					}
				}
				else
				{
					stackPanel_.Children.Add(CreateTextBox(line, grayBrush_));
				}
			}

			adornmentLayer_.RemoveAllAdornments();

			try
			{
				ITextSnapshotLine snapshotLine = view.TextSnapshot.GetLineFromLineNumber(currentTextLineNumber_);
				Microsoft.VisualStudio.Text.Formatting.TextBounds start = view.TextViewLines.GetCharacterBounds(snapshotLine.Start);

				Canvas.SetLeft(stackPanel_, start.Left);
				Canvas.SetTop(stackPanel_, start.Top);
				SnapshotSpan span = snapshotLine.Extent;
				this.adornmentLayer_.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, stackPanel_, null);

			}
			catch (ArgumentOutOfRangeException e)
			{
				Log.Output(string.Format("{0}\n", e.ToString()));
			}
		}

		private void OnViewLayoutChanged(object sender, EventArgs e)
		{
			if (!showSuggestion_)
			{
				return;
			}

			foreach (TextBlock block in stackPanel_.Children)
			{
				FormatText(block);
			}

			GetTagger().FormatText(GetTextFormat());
			if (stackPanel_.Children.Count > 0)
			{
				adornmentLayer_.RemoveAllAdornments();

				ITextSnapshotLine snapshotLine = view_.TextSnapshot.GetLineFromLineNumber(currentTextLineNumber_);
				Microsoft.VisualStudio.Text.Formatting.TextBounds start = view_.TextViewLines.GetCharacterBounds(snapshotLine.Start);

				SnapshotSpan span = snapshotLine.Extent;

				Canvas.SetLeft(stackPanel_, start.Left);
				Canvas.SetTop(element: stackPanel_, start.Top);

				adornmentLayer_.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, stackPanel_, null);
			}
		}

		private int GetOccurrenceOfLetter(String s, char c)
		{
			int n = 0;
			for (int i = 0; (i = s.IndexOf(c, i)) >= 0; ++i, ++n) { }
			return n;
		}

		private int NextNonWhitespace(String s, int index)
		{
			for (; index < s.Length && Char.IsWhiteSpace(s[index]); ++index)
			{
			}
			return index;
		}

		private bool IsNameChar(char c)
		{
			return Char.IsLetterOrDigit(c) || c == '_';
		}

		private Tuple<int, int> CompareStrings(String a, String b)
		{
			int a_index = 0, b_index = 0;
			while (a_index < a.Length && b_index < b.Length)
			{
				char aChar = a[a_index];
				char bChar = b[b_index];
				if (aChar == bChar)
				{
					++a_index;
					++b_index;
				}
				else
				{
					if (Char.IsWhiteSpace(bChar))
					{
						b_index = NextNonWhitespace(b, b_index);

						continue;
					}

					if (Char.IsWhiteSpace(aChar) && (b_index >= 1 && !IsNameChar(b[b_index - 1])))
					{
						a_index = NextNonWhitespace(a, a_index);

						continue;
					}

					break;
				}
			}

			return new Tuple<int, int>(a_index, b_index);
		}

		private int CheckSuggestion(String suggestion, String line)
		{
			if (line.Length == 0)
			{
				return 0;
			}

			int index = suggestion.IndexOf(line);
			int endPos = index + line.Length;
			int firstLineBreak = suggestion.IndexOf('\n');

			if (index > -1 && (firstLineBreak == -1 || endPos < firstLineBreak))
			{
				return index == 0 ? line.Length : -1;
			}
			else
			{
				Tuple<int, int> res = CompareStrings(line, suggestion);
				int endPoint = isTextInsertion_ ? line.Length - insertionPoint_ : line.Length;
				return res.Item1 >= endPoint ? res.Item2 : -1;
			}
		}

		private int GetCurrentTextLine()
		{
			CaretPosition caretPosition = view_.Caret.Position;

			var textPoint = caretPosition.Point.GetPoint(buffer_, caretPosition.Affinity);

			if (!textPoint.HasValue)
			{
				return -1;
			}

			return buffer_.CurrentSnapshot.GetLineNumberFromPosition(textPoint.Value);
		}

		public void Update()
		{

			if (suggestion == null)
			{
				return;
			}

			int textLineN = GetCurrentTextLine();

			if (textLineN < 0)
			{
				return;
			}

			ITextSnapshot newSnapshot = buffer_.CurrentSnapshot;
			snapshot_ = newSnapshot;

			String untrimLine = newSnapshot.GetLineFromLineNumber(textLineN).GetText();
			String line = untrimLine.TrimStart();

			int suggestionIndex = CheckSuggestion(suggestion.Item1, line);
			if (0<=suggestionIndex)
			{
				currentTextLineNumber_ = textLineN;
				suggestionIndex_ = suggestionIndex;
				ShowSuggestion(untrimLine, 0);
			}
			else
			{
				ClearSuggestion();
			}
		}

		public bool CompleteText()
		{
			int textLineN = GetCurrentTextLine();

			if (textLineN < 0 || textLineN != currentTextLineNumber_)
			{
				return false;
			}

			String untrimLine = snapshot_.GetLineFromLineNumber(currentTextLineNumber_).GetText();
			String line = untrimLine.Trim();

			int suggestionLineN = CheckSuggestion(suggestion.Item1, line);
			if (0<=suggestionLineN)
			{
				int diff = untrimLine.Length - untrimLine.TrimStart().Length;
				string whitespace = String.IsNullOrWhiteSpace(untrimLine) ? string.Empty : untrimLine.Substring(0, diff);
				ReplaceText(whitespace + suggestion.Item1, currentTextLineNumber_);
				return true;
			}

			return false;
		}

		void ReplaceText(string text, int lineN)
		{
			ClearSuggestion();
			SnapshotSpan span = snapshot_.GetLineFromLineNumber(lineN).Extent;
			ITextEdit edit = view_.BufferGraph.TopBuffer.CreateEdit();
			int spanLength = span.Length;
			edit.Replace(span, text);
			ITextSnapshot newSnapshot = edit.Apply();

			if (spanLength == 0 && 0<text.Length)
			{
				view_.Caret.MoveToPreviousCaretPosition();
				view_.Caret.MoveToNextCaretPosition();
			}
		}

		void ShowSuggestion(String text, int suggestionLineStart)
		{
			UpdateAdornment(view_, text, suggestionLineStart);

			showSuggestion_ = true;
			MarkDirty();
		}

		public void ClearSuggestion()
		{
			if (!showSuggestion_)
			{
				return;
			}
			IntraGrayTextAdornmentTagger inlineTagger = GetTagger();
			inlineTagger.ClearAdornment();
			inlineTagger.MarkDirty();
			suggestion = null;
			adornmentLayer_.RemoveAllAdornments();
			showSuggestion_ = false;

			MarkDirty();
		}

		void MarkDirty()
		{
			GetTagger().MarkDirty();
			ITextSnapshot newSnapshot = buffer_.CurrentSnapshot;
			snapshot_ = newSnapshot;

			if (view_.TextViewLines == null)
			{
				return;
			}

			SnapshotPoint changeStart = view_.TextViewLines.FirstVisibleLine.Start;
			SnapshotPoint changeEnd = view_.TextViewLines.LastVisibleLine.Start;

			ITextSnapshotLine startLine = view_.TextSnapshot.GetLineFromPosition(changeStart);
			ITextSnapshotLine endLine = view_.TextSnapshot.GetLineFromPosition(changeEnd);

			SnapshotSpan span = new SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak).
			TranslateTo(targetSnapshot: newSnapshot, SpanTrackingMode.EdgePositive);

			if (TagsChanged != null)
			{
				TagsChanged(this, new SnapshotSpanEventArgs(span));
			}
		}
	}
}
