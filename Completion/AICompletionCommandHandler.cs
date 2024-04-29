using HandyTools.Commands;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using MSXML;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace HandyTools.Completion
{
	internal class AICompletionCommandHandler : IOleCommandTarget
	{
		internal AICompletionCommandHandler(IVsTextView textViewAdapter, ITextView textView, AICompletionCommandHandlerProvider provider)
		{
			vsTextView_ = textViewAdapter;
			textView_ = textView;
			provider_ = provider;

			//add the command to the command chain
			textViewAdapter.AddCommandFilter(this, out nextCommandHandler_);
		}

		public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (!VsShellUtilities.IsInAutomationFunction(provider_.ServiceProvider))
			{
				if (pguidCmdGroup == PackageGuids.HandyTools && 0<cCmds)
				{
					// make the Insert Snippet command appear on the context menu 
					if (prgCmds[0].cmdID == (uint)PackageIds.CommandLineCompletion)
					{
						prgCmds[0].cmdf = (int)Constants.MSOCMDF_ENABLED | (int)Constants.MSOCMDF_SUPPORTED;
						return VSConstants.S_OK;
					}
				}
			}
			return nextCommandHandler_.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
		}

		public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (VsShellUtilities.IsInAutomationFunction(provider_.ServiceProvider))
			{
				return nextCommandHandler_.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
			}
			//make a copy of this so we can look at it after forwarding some commands
			uint commandID = nCmdID;
			char typedChar = char.MinValue;
			//make sure the input is a char before getting it
			if (pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR)
			{
				typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
			}

			//check for a commit character
			if (!hasCompletionUpdated_ && nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB)
			{

				var tagger = GetTagger();

				if (tagger != null)
				{
					if (tagger.IsSuggestionActive() && tagger.CompleteText())
					{
						ClearCompletionSessions();
						return VSConstants.S_OK;
					}
					else
					{
						tagger.ClearSuggestion();
					}
				}

			}
			else if (nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN || nCmdID == (uint)VSConstants.VSStd2KCmdID.CANCEL)
			{
				var tagger = GetTagger();
				if (tagger != null)
				{
					tagger.ClearSuggestion();
				}
			}

			CheckSuggestionUpdate(nCmdID);

			//pass along the command so the char is added to the buffer
			int retVal = nextCommandHandler_.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
			bool handled = false;

			//gets lsp completions on added character or deletions
			if (pguidCmdGroup == PackageGuids.HandyTools && commandID == (uint)PackageIds.CommandLineCompletion)
			{
				_ = Task.Run(() => GetLSPCompletions());
				handled = true;
			}
			else if (!typedChar.Equals(char.MinValue) || commandID == (uint)VSConstants.VSStd2KCmdID.RETURN)
			{
				_ = Task.Run(() => GetLSPCompletions());
				handled = true;
			}
			else if (commandID == (uint)VSConstants.VSStd2KCmdID.BACKSPACE || commandID == (uint)VSConstants.VSStd2KCmdID.DELETE)
			{
				_ = Task.Run(() => GetLSPCompletions());
				handled = true;
			}

			if (handled)
			{
				return VSConstants.S_OK;
			}
			return retVal;
		}

		private CompletionTagger GetTagger()
		{
			Type key = typeof(CompletionTagger);
			Microsoft.VisualStudio.Utilities.PropertyCollection props = textView_.TextBuffer.Properties;
			if (props.ContainsProperty(key))
			{
				return props.GetProperty<CompletionTagger>(key);
			}
			else
			{
				return null;
			}
		}

		private void GetLSPCompletions()
		{
			SnapshotPoint? caretPoint = textView_.Caret.Position.Point.GetPoint(textBuffer => (!textBuffer.ContentType.IsOfType("projection")), PositionAffinity.Predecessor);
			if (!caretPoint.HasValue)
			{
				return;
			}
			int lineN;
			int characterN;
			int res = vsTextView_.GetCaretPos(out lineN, out characterN);

			if (res != VSConstants.S_OK)
			{
				return;
			}
			String untrimLine = textView_.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(lineN).GetText();
			if (characterN < untrimLine.Length)
			{
				String afterCaret = untrimLine.Substring(characterN);
				String escapedSymbols = Regex.Escape(":(){ },.\"\';");

				String pattern = "[\\s\\t\\n\\r" + escapedSymbols + "]*";
				Match m = Regex.Match(afterCaret, pattern, RegexOptions.IgnoreCase);
				if (!(m.Success && m.Index == 0 && m.Length == afterCaret.Length))
					return;
			}
			hasCompletionUpdated_ = false;
			bool multiline = !IsInline(lineN);
			if (completionTask_ == null || completionTask_.IsCompleted)
			{
				string text = "test";
				if (completionTask_ == null || completionTask_.IsCompleted)
				{
					_ = ShowCompletionAsync(text, lineN, characterN);
				}
			}
		}

		private bool IsInline(int lineNumber)
		{
			string text = textView_.TextSnapshot.GetLineFromLineNumber(lineNumber).GetText();
			return !String.IsNullOrWhiteSpace(text);
		}

		private async Task ShowCompletionAsync(String text, int lineNumber, int characterNumber)
		{
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			SnapshotPoint? caretPoint = textView_.Caret.Position.Point.GetPoint(textBuffer => (!textBuffer.ContentType.IsOfType("projection")), PositionAffinity.Predecessor);
			if (!caretPoint.HasValue)
			{
				return;
			}

			int newLineNumber;
			int newCharacterNumber;
			int resCaretPos = vsTextView_.GetCaretPos(out newLineNumber, out newCharacterNumber);

			if (resCaretPos != VSConstants.S_OK || (lineNumber != newLineNumber) || (characterNumber != newCharacterNumber))
			{
				return;
			}

			CompletionTagger tagger = GetTagger();
			if (null != tagger && !string.IsNullOrEmpty(text))
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
				//tagger.SetSuggestion(text, IsInline(newLineNumber), newCharacterNumber);
			}
		}

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
						hasCompletionUpdated_ = true;
					}

					break;
				case ((uint)VSConstants.VSStd2KCmdID.TAB):
				case ((uint)VSConstants.VSStd2KCmdID.RETURN):
					hasCompletionUpdated_ = false;
					break;
			}
		}

		private void ClearCompletionSessions()
		{
			provider_.CompletionBroker.DismissAllSessions(textView_);
		}

		private IOleCommandTarget nextCommandHandler_;
		private IVsTextView vsTextView_;
		private ITextView textView_;
		private ITextDocument textDocument_;
		private AICompletionCommandHandlerProvider provider_;
		private ICompletionSession session_;
		private bool hasCompletionUpdated_;
		private Task<string> completionTask_;
	}
}
