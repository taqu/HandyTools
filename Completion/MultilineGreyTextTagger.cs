using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Documents;
using System.Diagnostics;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Reflection;
using Microsoft.VisualStudio.VCProjectEngine;
using System.Windows.Media.TextFormatting;
using Microsoft.VisualStudio.Settings.Internal;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace HandyTools
{

	internal sealed class MultilineGreyTextTagger : ITagger<CompletionTag>
	{
		/// panel with multiline grey text
		private StackPanel stackPanel;

		/// used to set the colour of the grey text
		private Brush greyBrush;

		/// used to set the colour of text that overlaps with the users text
		private Brush transparentBrush;

		/// contains the editor text and OnChange triggers on any text changes
		private ITextBuffer buffer;

		/// current editor display, immutable data
		private ITextSnapshot snapshot;

		/// the editor display object
		private IWpfTextView view;

		/// contains the grey text
		private IAdornmentLayer adornmentLayer;

		/// true if a suggestion should be shown
		private bool showSuggestion = false;
		private bool inlineSuggestion = false;
		private bool isTextInsertion = false;
		private bool prefixLineFeed_ = false;

		///  line number the suggestion should be displayed at
		private int currentTextLineN;
		private int currentVisualLineN;
		private int suggestionIndex;
		private int insertionPoint;
		private int userIndex;
		private string userEndingText;
		private string virtualText_;
		/// suggestion to display
		/// first string is to match against second item: array is for formatting
		private Tuple<string, string[]> suggestion_ = null;

		private InlineGreyTextTagger GetTagger()
		{
			var key = typeof(InlineGreyTextTagger);
			var props = view.TextBuffer.Properties;
			if (props.ContainsProperty(key))
			{
				return props.GetProperty<InlineGreyTextTagger>(key);
			}
			else
			{
				return null;
			}
		}

		public bool SetSuggestion(CompletionCommandHandler.Suggestion newSuggestion, int caretPoint, bool prefixLineFeed=false)
		{
			try
			{
				ClearSuggestion();
				int lineN = GetCurrentTextLine();

				if (lineN < 0)
				{
					return false;
				}

				string untrim = buffer.CurrentSnapshot.GetLineFromLineNumber(lineN).GetText();

				virtualText_ = string.Empty;
				if (string.IsNullOrWhiteSpace(untrim) && untrim.Length < caretPoint)
				{
					virtualText_ = new string(' ', caretPoint - untrim.Length);
				}
				string line = untrim.TrimStart();
				int offset = untrim.Length - line.Length;

				caretPoint = Math.Max(0, caretPoint - offset);

				string combineSuggestion = line + newSuggestion.text_;
				if (line.Length - caretPoint > 0)
				{
					string currentText = line.Substring(0, caretPoint);
					combineSuggestion = currentText + newSuggestion.text_;
					userEndingText = line.Substring(caretPoint).Trim();
					var userIndex = newSuggestion.text_.IndexOf(userEndingText);

					if (userIndex < 0) { return false; }
					userIndex += currentText.Length;

					this.userIndex = userIndex;
					isTextInsertion = true;
					insertionPoint = line.Length - caretPoint;
				}
				else { isTextInsertion = false; }
				var suggestionLines = combineSuggestion.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
				suggestion_ = new Tuple<string, string[]>(combineSuggestion, suggestionLines);
				prefixLineFeed_ = prefixLineFeed;
				return Update(prefixLineFeed_);
			}
			catch (Exception ex)
			{
				Log.Output("Exception: " + ex.ToString());
				return false;
			}
		}

		public bool OnSameLine()
		{
			return GetCurrentTextLine() == currentTextLineN;
		}

		private void LostFocus(object sender, EventArgs e)
		{
			ClearSuggestion();
		}

		public MultilineGreyTextTagger(IWpfTextView view, ITextBuffer buffer)
		{
			this.stackPanel = new StackPanel();

			this.buffer = buffer;
			this.snapshot = buffer.CurrentSnapshot;
			this.buffer.Changed += BufferChanged;
			this.view = view;
			this.adornmentLayer = view.GetAdornmentLayer("HandyToolsAdornmentLayer");

			this.view.LayoutChanged += this.OnSizeChanged;

			this.transparentBrush = new SolidColorBrush();
			this.transparentBrush.Opacity = 0;
			this.greyBrush = new SolidColorBrush(Colors.Gray);
			view.LostAggregateFocus += LostFocus;
		}

		public bool IsSuggestionActive()
		{
			return showSuggestion;
		}

		public string GetSuggestion()
		{
			if (suggestion_ != null && showSuggestion)
			{
				return suggestion_.Item1;
			}
			else
			{
				return "";
			}
		}

		//This an iterator that is used to iterate through all of the test tags
		//tags are like html tags they mark places in the view to modify how those sections look
		//Testtag is a tag that tells the editor to add empty space
		public IEnumerable<ITagSpan<CompletionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
		{
			var currentSuggestion = suggestion_;
			if (!showSuggestion || currentSuggestion == null || currentSuggestion.Item2.Length <= 1)
			{
				yield break;
			}

			SnapshotSpan entire = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End).TranslateTo(snapshot, SpanTrackingMode.EdgeExclusive);
			ITextSnapshot currentSnapshot = spans[0].Snapshot;

			var line = currentSnapshot.GetLineFromLineNumber(currentTextLineN).Extent;
			var span = new SnapshotSpan(line.End, line.End);

			var snapshotLine = currentSnapshot.GetLineFromLineNumber(currentTextLineN);

			var height = view.LineHeight * (currentSuggestion.Item2.Length - 1);

			if (currentTextLineN == 0 && currentSnapshot.Lines.Count() == 1 && string.IsNullOrEmpty(currentSnapshot.GetText()))
			{
				height += view.LineHeight;
			}

			yield return new TagSpan<CompletionTag>(span, new CompletionTag(0, 0, 0, 0, height, PositionAffinity.Predecessor, stackPanel, this));
		}

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		//triggers when the editor text buffer changes
		void BufferChanged(object sender, TextContentChangedEventArgs e)
		{
			// If this isn't the most up-to-date version of the buffer, then ignore it for now (we'll eventually get another change event).
			if (e.After != buffer.CurrentSnapshot)
				return;
			_ = Update(prefixLineFeed_);
		}

		TextRunProperties GetTextFormat()
		{
			var line = view.TextViewLines.FirstVisibleLine;
			return line.GetCharacterFormatting(line.Start);
		}

		//used to set formatting of the displayed multi lines
		public void FormatText(TextBlock block)
		{
			//var pos = snapshot.GetLineFromLineNumber(currentLineN).Start;

			var line = view.TextViewLines.FirstVisibleLine;
			var format = line.GetCharacterFormatting(line.Start);
			if (format != null)
			{
				block.FontFamily = format.Typeface.FontFamily;
				block.FontSize = format.FontRenderingEmSize;
			}
		}

		string ConvertTabsToSpaces(string text)
		{
			int tabSize = view.Options.GetTabSize();
			return Regex.Replace(text, "\t", new string(' ', tabSize));
		}
		void FormatTextBlock(TextBlock textBlock)
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
			if (line.Length <= suggestionIndex)
				return;

			int emptySpaceLength = userText.Length - userText.TrimStart().Length;
			string emptySpace = ConvertTabsToSpaces(userText.Substring(0, emptySpaceLength));
			string editedUserText = emptySpace + userText.TrimStart();
			if (isTextInsertion)
			{
				editedUserText = emptySpace + line.Substring(0, start);
			}
			string remainder = line.Substring(start);
			TextBlock textBlock = new TextBlock();
			textBlock.Inlines.Add(item: new Run(editedUserText) { Foreground = transparentBrush });
			textBlock.Inlines.Add(item: new Run(remainder) { Foreground = greyBrush });

			stackPanel.Children.Add(textBlock);
		}

		void AddInsertionTextBlock(int start, int end, string line)
		{
			if (line.Length <= suggestionIndex)
				return;

			TextBlock textBlock = new TextBlock();
			string remainder = line.Substring(start, end - start);
			GetTagger().UpdateAdornment(CreateTextBox(remainder, greyBrush));
		}

		//Updates the grey text
		public void UpdateAdornment(IWpfTextView view, string userText, int suggestionStart)
		{
			stackPanel.Children.Clear();
			GetTagger().ClearAdornment();
			for (int i = suggestionStart; i < suggestion_.Item2.Length; i++)
			{
				string line = suggestion_.Item2[i];

				if (i == 0)
				{
					int offset = line.Length - line.TrimStart().Length;

					if (isTextInsertion && suggestionIndex < userIndex)
					{
						if (suggestionIndex > 0 && char.IsWhiteSpace(line[suggestionIndex - 1]) && !char.IsWhiteSpace(userText[userText.Length - insertionPoint - 1]))
						{
							suggestionIndex--;
						}
						AddInsertionTextBlock(suggestionIndex + offset, userIndex, line);
						if (line.Length > userIndex + 1)
						{
							AddSuffixTextBlocks(userIndex + userEndingText.Trim().Length, line, userText);
						}
					}
					else
					{
						AddSuffixTextBlocks(userText.Length > 0 ? suggestionIndex + offset : 0, line, userText);
					}
				}
				else
				{
					stackPanel.Children.Add(CreateTextBox(line, greyBrush));
				}
			}

			this.adornmentLayer.RemoveAllAdornments();

			//usually only happens the moment a bunch of text has rentered such as an undo operation
			try
			{
				ITextSnapshotLine snapshotLine = view.TextSnapshot.GetLineFromLineNumber(currentTextLineN);
				var start = view.TextViewLines.GetCharacterBounds(snapshotLine.Start);

				// Place the image in the top left hand corner of the line
				Canvas.SetLeft(stackPanel, start.Left);
				Canvas.SetTop(stackPanel, start.Top);
				var span = snapshotLine.Extent;
				// Add the image to the adornment layer and make it relative to the viewport
				this.adornmentLayer.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, stackPanel, null);

			}
			catch (ArgumentOutOfRangeException e)
			{
				Log.Output("Exception: " + e);
			}
		}

		//Adds grey text to display
		private void OnSizeChanged(object sender, EventArgs e)
		{
			if (!showSuggestion)
			{
				return;
			}

			foreach (TextBlock block in stackPanel.Children)
			{
				FormatText(block);
			}

			GetTagger().FormatText(GetTextFormat());
			if (stackPanel.Children.Count > 0)
			{
				// Clear the adornment layer of previous adornments
				this.adornmentLayer.RemoveAllAdornments();

				ITextSnapshotLine snapshotLine = view.TextSnapshot.GetLineFromLineNumber(currentTextLineN);
				var start = view.TextViewLines.GetCharacterBounds(snapshotLine.Start);

				var span = snapshotLine.Extent;

				// Place the image in the top left hand corner of the line
				Canvas.SetLeft(stackPanel, start.Left);
				Canvas.SetTop(element: stackPanel, start.Top);

				// Add the image to the adornment layer and make it relative to the viewport
				this.adornmentLayer.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, stackPanel, null);
			}
		}

		//returns the number of times letter c appears in s
		int GetOccurenceOfLetter(string s, char c)
		{
			int n = 0;
			for (int i = 0; (i = s.IndexOf(c, i)) >= 0; i++, n++) { }
			return n;
		}

		//Gets the line number of the caret
		int GetCurrentTextLine()
		{
			CaretPosition caretPosition = view.Caret.Position;

			var textPoint = caretPosition.Point.GetPoint(buffer, caretPosition.Affinity);

			if (!textPoint.HasValue)
			{
				return -1;
			}

			return buffer.CurrentSnapshot.GetLineNumberFromPosition(textPoint.Value);
		}

		//update multiline data
		public bool Update(bool prefixLineFeed=false)
		{

			if (suggestion_ == null)
			{
				return false;
			}

			int textLineN = GetCurrentTextLine();

			if (textLineN < 0)
			{
				return false;
			}

			ITextSnapshot newSnapshot = buffer.CurrentSnapshot;
			this.snapshot = newSnapshot;

			string untrimLine = newSnapshot.GetLineFromLineNumber(textLineN).GetText();
			string line = untrimLine.TrimStart();

			//get line carat is on
			//if suggestion matches line (possibly including preceding lines)
			//  show suggestion
			//else
			//  clear suggestions

			int suggestionIndex = Commands.CodeUtil.CheckSuggestion(suggestion_.Item1, line);
			if (suggestionIndex >= 0)
			{
				this.currentTextLineN = textLineN;
				this.suggestionIndex = suggestionIndex;
				ShowSuggestion(untrimLine, 0, prefixLineFeed);
				return true;
			}
			else
			{
				ClearSuggestion();
				return false;
			}
		}

		//Adds the grey text to the file replacing current line in the process
		public bool CompleteText()
		{
			if (!showSuggestion || suggestion_ == null)
			{
				return false;
			}
			int textLineN = GetCurrentTextLine();
			if (textLineN < 0 || textLineN != currentTextLineN)
			{
				return false;
			}

			string untrimLine = this.snapshot.GetLineFromLineNumber(currentTextLineN).GetText();
			string line = untrimLine.Trim();

			int suggestionLineN = Commands.CodeUtil.CheckSuggestion(suggestion_.Item1, line);
			if (suggestionLineN >= 0)
			{
				int diff = untrimLine.Length - untrimLine.TrimStart().Length;
				string whitespace = string.IsNullOrWhiteSpace(untrimLine) ? "" : untrimLine.Substring(0, diff);
				ReplaceText(whitespace + suggestion_.Item1, currentTextLineN);
				return true;
			}

			return false;
		}

		//replaces text in the editor
		void ReplaceText(string text, int lineN)
		{
			ClearSuggestion();
			SnapshotSpan span = this.snapshot.GetLineFromLineNumber(lineN).Extent;
			ITextEdit edit = view.BufferGraph.TopBuffer.CreateEdit();
			var spanLength = span.Length;
			edit.Replace(span, text);
			var newSnapshot = edit.Apply();

			if (spanLength == 0 && text.Length > 0)
			{
				view.Caret.MoveToPreviousCaretPosition();
				view.Caret.MoveToNextCaretPosition();
			}
		}

		//sets up the suggestion for display
		void ShowSuggestion(string text, int suggestionLineStart, bool prefixLineFeed=false)
		{
			UpdateAdornment(view, text, suggestionLineStart);
			showSuggestion = true;
			MarkDirty();
		}

		//removes the suggestion
		public void ClearSuggestion()
		{
			if (!showSuggestion) return;
			InlineGreyTextTagger inlineTagger = GetTagger();
			inlineTagger.ClearAdornment();
			inlineTagger.MarkDirty();
			suggestion_ = null;
			adornmentLayer.RemoveAllAdornments();
			showSuggestion = false;

			MarkDirty();
		}

		//triggers refresh of the screen 
		void MarkDirty()
		{
			GetTagger().MarkDirty();
			ITextSnapshot newSnapshot = buffer.CurrentSnapshot;
			this.snapshot = newSnapshot;

			if (view.TextViewLines == null) return;

			var changeStart = view.TextViewLines.FirstVisibleLine.Start;
			var changeEnd = view.TextViewLines.LastVisibleLine.Start;

			var startLine = view.TextSnapshot.GetLineFromPosition(changeStart);
			var endLine = view.TextSnapshot.GetLineFromPosition(changeEnd);

			var span = new SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak).
			TranslateTo(targetSnapshot: newSnapshot, SpanTrackingMode.EdgePositive);

			//lines we are marking dirty
			//currently all of them for simplicity 
			if (this.TagsChanged != null)
			{
				this.TagsChanged(this, new SnapshotSpanEventArgs(span));
			}
		}
	}
}