using HandyTools.Commands;
using HandyTools.Completion;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace HandyTools
{

	internal class CompletionCommandHandler : IOleCommandTarget
	{
		[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
		public static extern short GetAsyncKeyState(Int32 keyCode);

		public static int Utf16OffsetToUtf8Offset(string str, int utf16Offset)
		{
			return Encoding.UTF8.GetByteCount(str.ToCharArray(), 0, utf16Offset);
		}

		public static int Utf16OffsetToUtf8Offset(ITextSnapshot text, int utf16Offset)
		{
			int offset = 0;
			char[] chars = new char[1];
			for (int i = 0; i < utf16Offset; ++i)
			{
				chars[0] = text[i];
				offset += Encoding.UTF8.GetByteCount(chars);
			}
			return offset;
		}

		public static int Utf8OffsetToUtf16Offset(string str, int utf8Offset)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(str);
			return Encoding.UTF8.GetString(bytes.Take(utf8Offset).ToArray()).Length;
		}

		public static int Utf8OffsetToUtf16Offset(ITextSnapshot text, int utf8Offset)
		{
			int offset = 0;
			char[] chars = new char[1];
			int i = 0;
			for (; i < text.Length; ++i)
			{
				chars[0] = text[i];
				offset += Encoding.UTF8.GetByteCount(chars);
				if (utf8Offset <= offset)
				{
					break;
				}
			}
			return i;
		}

		public struct Suggestion
		{
			public string text_;
		};

		private IOleCommandTarget nextCommandHandler_;
		private ITextView textView_;
		private IVsTextView textViewAdapter_;
		private ITextDocument document_;
		private LanguageInfo language_;

		private CompletionHandlerProvider provider_;

		private bool hasCompletionUpdated = false;
		private List<Suggestion> suggestions_ = new List<Suggestion>();

		//The command Handler processes keyboard input.
		internal CompletionCommandHandler(IVsTextView textViewAdapter, ITextView textView, CompletionHandlerProvider provider)
		{
			textView_ = textView;
			provider_ = provider;
			textViewAdapter_ = textViewAdapter;

			var topBuffer = textView.BufferGraph.TopBuffer;
			var projectionBuffer = topBuffer as IProjectionBufferBase;
			var typeName = topBuffer.GetType();
			ITextBuffer textBuffer = projectionBuffer != null ? projectionBuffer.SourceBuffers[0] : topBuffer;
			provider.documentFactory.TryGetTextDocument(textBuffer, out document_);
			if (null != document_)
			{
				document_.FileActionOccurred += OnFileActionOccurred;
				document_.TextBuffer.ContentTypeChanged += OnContentTypeChanged;
			}
			RefreshLanguage();
			//add the command to the command chain
			textViewAdapter_.AddCommandFilter(this, out nextCommandHandler_);
			textView_.Caret.PositionChanged += CaretUpdate;
		}

		private MultilineGreyTextTagger GetTagger()
		{
			var key = typeof(MultilineGreyTextTagger);
			var props = textView_.TextBuffer.Properties;
			if (props.ContainsProperty(key))
			{
				return props.GetProperty<MultilineGreyTextTagger>(key);
			}
			else
			{
				return null;
			}
		}

		//required by interface just boiler plate
		public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
		{
			return nextCommandHandler_.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
		}

		public bool IsInline(int lineN)
		{
			var text = textView_.TextSnapshot.GetLineFromLineNumber(lineN).GetText();
			return !String.IsNullOrWhiteSpace(text);
		}

#if false
        public async void GetCompletions()
        {
            SnapshotPoint? caretPoint = textView_.Caret.Position.Point.GetPoint(textBuffer => (!textBuffer.ContentType.IsOfType("projection")), PositionAffinity.Predecessor);

            if (!caretPoint.HasValue)
            {
                return;
            }
            int lineN;
            int characterN;
            if(VSConstants.S_OK != textViewAdapter_.GetCaretPos(out lineN, out characterN)) {
                return;
            }
            //Make sure caret is at the end of a line
            String untrimLine = textView_.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(lineN).GetText();
            if (characterN < untrimLine.Length)
            {
                String afterCaret = untrimLine.Substring(characterN);
                String escapedSymbols = Regex.Escape(":(){ },.\"\';");

                String pattern = "[\\s\\t\\n\\r" + escapedSymbols + "]*";
                Match m = Regex.Match(afterCaret, pattern, RegexOptions.IgnoreCase);
                if (!(m.Success && m.Index == 0 && m.Length == afterCaret.Length))
                {
                    return;
                }
            }

            HandyToolsPackage package = await HandyToolsPackage.GetPackageAsync();
            if(null == package)
            {
                return;
            }
            LanguageInfo language = SupportedLanguage.GetLanguage(textView_.TextDataModel.ContentType);
            CompletionModel completionModel = package.GetCompletionModel();
            if(null == completionModel)
            {
                return;
            }
            completionModel.GetCompletionsAsync(document_.FilePath, textView_.TextSnapshot, language, lineN)

            hasCompletionUpdated = false;
            bool multiline = !IsInline(lineN);
            if (completionTask == null || completionTask.IsCompleted)
            {
                //completionTask = client.RefactCompletion(m_textView.TextBuffer.Properties, filePath, lineN, multiline ? 0 : characterN, multiline);
                //var s = await completionTask;
                string s = string.Empty;
                //await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                //if (completionTask == null || completionTask.IsCompleted)
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        s = "test";
                    }
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    ShowSuggestion(s, lineN, characterN);
                }
            }
        }
#else

		public async Task GetCompletionsAsync()
		{
			try
			{
				suggestions_.Clear();

				if (null == document_)
				{
					return;
				}
				SnapshotPoint? caretPoint = textView_.Caret.Position.Point.GetPoint(
					textBuffer => (!textBuffer.ContentType.IsOfType("projection")),
					PositionAffinity.Successor);
				if (!caretPoint.HasValue)
				{
					return;
				}

				int caretPosition = caretPoint.Value.Position;
				ITextSnapshot textSnapshot = document_.TextBuffer.CurrentSnapshot;
				int cursorPosition = document_.Encoding.IsSingleByte
					? caretPosition
					: Utf16OffsetToUtf8Offset(textSnapshot, caretPosition);


				if (cursorPosition > textSnapshot.Length)
				{
					Debug.Print("Error Caret past text position");
					return;
				}

				{
					Options.OptionPageHandyToolsAI optionsAI = await HandyToolsPackage.GetOptionAIAsync();
					if (null != optionsAI && !optionsAI.InlineCompletion)
					{
						bool inline = false;
						for (int i = caretPosition; i < textSnapshot.Length; ++i)
						{
							if (CodeUtil.IsLineFeed(textSnapshot[i]))
							{
								break;
							}
							if (!CodeUtil.IsWhiteSpace(textSnapshot[i]))
							{
								inline = true;
								break;
							}
						}
						if (inline)
						{
							return;
						}
					}
				}

				HandyToolsPackage package = await HandyToolsPackage.GetPackageAsync();
				if (null == package)
				{
					return;
				}
				CompletionModel completionModel = package.GetCompletionModel();
				if (null == completionModel)
				{
					return;
				}
				IList<Completion.Completion>? completions = await completionModel.GetCompletionsAsync(document_.FilePath, textView_.TextSnapshot, language_, cursorPosition);
#if DEBUG
				string text = string.Empty;
				foreach (Completion.Completion completion in completions)
				{
					text += completion.text + '\n';
				}
				await Log.OutputAsync(text);
#endif
				//List<Completion.Completion> completions = new List<Completion.Completion>();
				//completions.Add(new Completion.Completion { id = Guid.NewGuid(), text = "test", startOffset = cursorPosition, endOffset = cursorPosition + 4 });
				package.SetCompletionModel(completionModel);

				if (null == completions || completions.Count <= 0)
				{
					return;
				}

				int lineNumber;
				int column;
				if (VSConstants.S_OK != textViewAdapter_.GetCaretPos(out lineNumber, out column))
				{
					return;
				}
				textSnapshot = document_.TextBuffer.CurrentSnapshot;
				ITextSnapshotLine line = textSnapshot.GetLineFromLineNumber(lineNumber);
				string prefix = line.Snapshot.GetText(0, Math.Min(column, line.Length));

				ParseCompletion(suggestions_, completions, textSnapshot, line, prefix, column);
				if (suggestions_.Count <= 0)
				{
					return;
				}

				MultilineGreyTextTagger tagger = GetTagger();
				if (tagger != null)
				{
					await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
					if(tagger.SetSuggestion(suggestions_[0], column, 1 < suggestions_.Count))
					{
						suggestions_.RemoveAt(0);
					}
					else
					{
						suggestions_.Clear();
					}
				}
			}
			catch (Exception ex)
			{
				await Log.OutputAsync("Exception: " + ex.ToString());
			}
		}
#endif

		public void ShowSuggestion(String s, int lineN, int characterN)
		{

			if (string.IsNullOrEmpty(s))
			{
				return;
			}
			//the caret must be in a non-projection location 
			SnapshotPoint? caretPoint = textView_.Caret.Position.Point.GetPoint(textBuffer => (!textBuffer.ContentType.IsOfType("projection")), PositionAffinity.Predecessor);
			if (!caretPoint.HasValue)
			{
				return;
			}

			int newLineN;
			int newCharacterN;
			int resCaretPos = textViewAdapter_.GetCaretPos(out newLineN, out newCharacterN);

			//double checks the cursor is still on the line the recommendation is for
			if (resCaretPos != VSConstants.S_OK || (lineN != newLineN) || (characterN != newCharacterN))
			{
				return;
			}

			MultilineGreyTextTagger tagger = GetTagger();
			if (tagger != null)
			{
				if(tagger.SetSuggestion(suggestions_[0], characterN, 1 < suggestions_.Count))
				{
					suggestions_.RemoveAt(0);
				}
				else
				{
					suggestions_.Clear();
				}
			}
		}

		private void CaretUpdate(object sender, CaretPositionChangedEventArgs e)
		{
			try
			{
				MultilineGreyTextTagger tagger = GetTagger();
				if (tagger == null)
				{
					return;
				}

				var key = GetAsyncKeyState(0x09);
				if ((0x8000 & key) > 0)
				{
					CompleteSuggestion(false);
				}
				else if (!tagger.OnSameLine())
				{
					tagger.ClearSuggestion();
				}
			}
			catch (Exception ex)
			{
				ThreadHelper.JoinableTaskFactory
					.RunAsync(async delegate { await Log.OutputAsync("Exception: " + ex.ToString()); })
					.FireAndForget(true);
			}
		}

		public async Task ShowNextSuggestionAsync()
		{
			try
			{
				if (suggestions_.Count <= 0)
				{
					return;
				}

				Suggestion suggestion = suggestions_[0];
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

				MultilineGreyTextTagger tagger = GetTagger();
				if (tagger == null)
				{
					return;
				}

				int lineN, characterN;
				if (VSConstants.S_OK != textViewAdapter_.GetCaretPos(out lineN, out characterN))
				{
					return;

				}

				_ = tagger.SetSuggestion(suggestion, characterN, 1< suggestions_.Count);
				suggestions_.RemoveAt(0);
			}
			catch (Exception ex)
			{
				ThreadHelper.JoinableTaskFactory
					.RunAsync(async delegate { await Log.OutputAsync("Exception: " + ex); })
					.FireAndForget(true);
			}

		}

		public bool CompleteSuggestion(bool checkLine = true)
		{
			var tagger = GetTagger();
			if (tagger != null)
			{
				if (tagger.IsSuggestionActive() && (tagger.OnSameLine() || !checkLine) && tagger.CompleteText())
				{
					ClearCompletionSessions();
					return true;
				}
				else { tagger.ClearSuggestion(); }
			}

			return false;
		}

		void ClearSuggestion()
		{
			var tagger = GetTagger();
			if (tagger != null) { tagger.ClearSuggestion(); }
		}

		//Used to detect when the user interacts with the intellisense popup
		void CheckSuggestionUpdate(uint nCmdID)
		{
			switch (nCmdID)
			{
				case ((uint)VSConstants.VSStd2KCmdID.UP):
				case ((uint)VSConstants.VSStd2KCmdID.DOWN):
				case ((uint)VSConstants.VSStd2KCmdID.PAGEUP):
				case ((uint)VSConstants.VSStd2KCmdID.PAGEDN):
					if (provider_.CompletionBroker.IsCompletionActive(textView_))
					{
						hasCompletionUpdated = true;
					}

					break;
				case ((uint)VSConstants.VSStd2KCmdID.TAB):
				case ((uint)VSConstants.VSStd2KCmdID.RETURN):
					hasCompletionUpdated = false;
					break;
			}
		}

		void ParseCompletion(
			List<Suggestion> suggestions,
			IList<Completion.Completion> completionItems,
			ITextSnapshot text, ITextSnapshotLine line, string prefix,
			int cursorPoint)
		{
			Debug.Assert(null != completionItems);
			for (int i = 0; i < completionItems.Count; i++)
			{
				Completion.Completion completionItem = completionItems[i];
				if (completionItem.text.Length <= 0)
				{
					continue;
				}
				int startOffset = completionItem.startOffset;
				int endOffset = completionItem.endOffset;
				int insertionStart = startOffset;

				if (!document_.Encoding.IsSingleByte)
				{
					startOffset = Utf8OffsetToUtf16Offset(text, startOffset);
					endOffset = Utf8OffsetToUtf16Offset(text, endOffset);
					insertionStart = Utf8OffsetToUtf16Offset(text, insertionStart);
				}
				if (endOffset > text.Length)
				{
					endOffset = text.Length;
				}

				string completionText = completionItem.text;
				if (endOffset < text.Length)
				{
					int endNewline = Commands.CodeUtil.IndexOfNewLine(text, endOffset);

					if (endNewline <= -1)
					{
						endNewline = text.Length;
					}

					completionText = completionText + text.GetText(endOffset, endNewline - endOffset);
				}
				//int offset = Commands.CodeUtil.CheckSuggestion(completionText, prefix);
				//if (offset < 0 || offset > completionText.Length)
				//{
				//	continue;
				//}

				//completionText = completionText.Substring(offset);
				Suggestion pair = new Suggestion { text_ = completionText };

				// Filter out completions that don't match the current intellisense prefix
				ICompletionSession session = provider_.CompletionBroker.GetSessions(textView_).FirstOrDefault();
				if (session != null && session.SelectedCompletionSet != null)
				{
					var completion = session.SelectedCompletionSet.SelectionStatus.Completion;
					if (completion == null)
					{
						continue;
					}
					string intellisenseSuggestion = completion.InsertionText;
					ITrackingSpan intellisenseSpan = session.SelectedCompletionSet.ApplicableTo;
					SnapshotSpan span = intellisenseSpan.GetSpan(intellisenseSpan.TextBuffer.CurrentSnapshot);
					if (span.Length > intellisenseSuggestion.Length)
					{
						continue;
					}
					string intellisenseInsertion = intellisenseSuggestion.Substring(span.Length);
					if (!completionText.StartsWith(intellisenseInsertion))
					{
						continue;
					}
				}
				suggestions.Add(pair);
			}
		}

		public bool IsIntellicodeEnabled()
		{
			var vsSettingsManager =
				provider_.ServiceProvider.GetService(typeof(SVsSettingsManager)) as IVsSettingsManager;

			vsSettingsManager.GetCollectionScopes(collectionPath: "ApplicationPrivateSettings",
												  out var applicationPrivateSettings);
			vsSettingsManager.GetReadOnlySettingsStore(applicationPrivateSettings,
													   out IVsSettingsStore readStore);
			var res2 =
				readStore.GetString("ApplicationPrivateSettings\\Microsoft\\VisualStudio\\IntelliCode",
									"WholeLineCompletions",
									out var str);
			return str != "1*System.Int64*2";
		}

		void ShowIntellicodeMsg()
		{
			if (IsIntellicodeEnabled())
			{
				VsShellUtilities.ShowMessageBox(
					HandyToolsPackage.GetPackage(),
					"Please disable IntelliCode to use Codeium. You can access Intellicode settings via Tools --> Options --> Intellicode.",
					"Disable IntelliCode",
					OLEMSGICON.OLEMSGICON_INFO,
					OLEMSGBUTTON.OLEMSGBUTTON_OK,
					OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
			}
		}

		//Key input handler
		public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
		{
			//let the other handlers handle automation functions
			if (VsShellUtilities.IsInAutomationFunction(provider_.ServiceProvider))
			{
				return nextCommandHandler_.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
			}
			if (language_.language == Completion.Language.None)
			{
				return nextCommandHandler_.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
			}

			//check for a commit character
			bool regenerateSuggestion = false;
			if (!hasCompletionUpdated && nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB)
			{

				MultilineGreyTextTagger tagger = GetTagger();

				if (tagger == null)
				{
					return nextCommandHandler_.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
				}

				ICompletionSession session = provider_.CompletionBroker.GetSessions(textView_).FirstOrDefault();
				if (session != null && session.SelectedCompletionSet != null)
				{
					tagger.ClearSuggestion();
					regenerateSuggestion = true;
				}
				else if (CompleteSuggestion())
				{
					return VSConstants.S_OK;
				}
			}
			else if (nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN || nCmdID == (uint)VSConstants.VSStd2KCmdID.CANCEL)
			{
				ClearSuggestion();
			}

			CheckSuggestionUpdate(nCmdID);
			//make a copy of this so we can look at it after forwarding some commands
			uint commandID = nCmdID;
			char typedChar = char.MinValue;

			//make sure the input is a char before getting it
			if (pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR)
			{
				typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
			}

			//pass along the command so the char is added to the buffer
			int retVal = nextCommandHandler_.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
			bool handled = false;
			if (hasCompletionUpdated) { ClearSuggestion(); }

			//gets lsp completions on added character or deletions
			if (!typedChar.Equals(char.MinValue) || commandID == (uint)VSConstants.VSStd2KCmdID.RETURN || regenerateSuggestion)
			{
				if (regenerateSuggestion || suggestions_.Count <= 0)
				{
					_ = Task.Run(() => GetCompletionsAsync());
				}
				else
				{
					_ = Task.Run(() => ShowNextSuggestionAsync());
				}
				handled = true;
			}
			else if (commandID == (uint)VSConstants.VSStd2KCmdID.BACKSPACE || commandID == (uint)VSConstants.VSStd2KCmdID.DELETE)
			{
				_ = Task.Run(() => GetCompletionsAsync());
				handled = true;
			}

			if (handled) return VSConstants.S_OK;
			return retVal;
		}

		//clears the intellisense popup window
		void ClearCompletionSessions()
		{
			provider_.CompletionBroker.DismissAllSessions(textView_);
		}

		private void OnContentTypeChanged(object sender, ContentTypeChangedEventArgs e)
		{
			RefreshLanguage();
		}

		private void OnFileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
		{
			RefreshLanguage();
		}

		private void RefreshLanguage()
		{
			if (null == document_)
			{
				language_ = SupportedLanguage.LanguageInfos[0];
			}
			else
			{
				language_ = SupportedLanguage.GetLanguage(textView_.TextDataModel.ContentType);
			}
		}
	}
}
