using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using static Microsoft.VisualStudio.Shell.ThreadedWaitDialogHelper;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Text;

namespace HandyTools.Completion
{
	internal class AICompletionCommandHandler : IOleCommandTarget
	{
		internal AICompletionCommandHandler(IVsTextView textViewAdapter, ITextView textView, AICompletionHandlerProvider provider)
		{
			textView_ = textView;
			provider_ = provider;

			//add the command to the command chain
			textViewAdapter.AddCommandFilter(this, out nextCommandHandler_);
		}

		public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
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
			if (nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN
				|| nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB
				|| (char.IsWhiteSpace(typedChar) || char.IsPunctuation(typedChar)))
			{
				//check for a selection
				if (session_ != null && !session_.IsDismissed)
				{
					//if the selection is fully selected, commit the current session
					if (session_.SelectedCompletionSet.SelectionStatus.IsSelected)
					{
						session_.Commit();
						//also, don't add the character to the buffer
						return VSConstants.S_OK;
					}
					else
					{
						//if there is no selection, dismiss the session
						session_.Dismiss();
					}
				}
			}

			//pass along the command so the char is added to the buffer
			int retVal = nextCommandHandler_.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
			bool handled = false;
			if (!typedChar.Equals(char.MinValue) && char.IsLetterOrDigit(typedChar))
			{
				if (session_ == null || session_.IsDismissed) // If there is no active session, bring up completion
				{
					this.TriggerCompletion();
					session_.Filter();
				}
				else    //the completion session is already active, so just filter
				{
					session_.Filter();
				}
				handled = true;
			}
			else if (commandID == (uint)VSConstants.VSStd2KCmdID.BACKSPACE //redo the filter if there is a deletion
				|| commandID == (uint)VSConstants.VSStd2KCmdID.DELETE)
			{
				if (session_ != null && !session_.IsDismissed)
				{
					session_.Filter();
				}
				handled = true;
			}
			if (handled)
			{
				return VSConstants.S_OK;
			}
			return retVal;
		}

		private bool TriggerCompletion()
		{
			//the caret must be in a non-projection location 
			SnapshotPoint? caretPoint =
			textView_.Caret.Position.Point.GetPoint(
			textBuffer => (!textBuffer.ContentType.IsOfType("projection")), PositionAffinity.Predecessor);
			if (!caretPoint.HasValue)
			{
				return false;
			}

			session_ = provider_.CompletionBroker.CreateCompletionSession(
				textView_,
				caretPoint.Value.Snapshot.CreateTrackingPoint(caretPoint.Value.Position, PointTrackingMode.Positive),
				true);

			//subscribe to the Dismissed event on the session 
			session_.Dismissed += this.OnSessionDismissed;
			session_.Start();

			return true;
		}

		private void OnSessionDismissed(object sender, EventArgs e)
		{
			session_.Dismissed -= this.OnSessionDismissed;
			session_ = null;
		}

		private ITextView textView_;
		private AICompletionHandlerProvider provider_;
		private IOleCommandTarget nextCommandHandler_;
		private ICompletionSession session_;
	}
}
