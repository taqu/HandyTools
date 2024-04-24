using HandyTools.Commands;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using MSXML;
using System.Runtime.InteropServices;

namespace HandyTools.Completion
{
	internal class AICompletionCommandHandler : IOleCommandTarget, IVsExpansionClient
	{
		internal AICompletionCommandHandler(IVsTextView textViewAdapter, ITextView textView, AICompletionCommandHandlerProvider provider)
		{
			vsTextView_ = textViewAdapter;
			textView_ = textView;
			provider_ = provider;

			//get the text manager from the service provider
			IVsTextManager2 textManager = provider_.ServiceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager2;
			textManager.GetExpansionManager(out exManager_);

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

			//the expansion insertion is handled in OnItemChosen
			//if the expansion session is still active, handle tab/backtab/return/cancel
			if (exSession_ != null)
			{
				if (nCmdID == (uint)VSConstants.VSStd2KCmdID.BACKTAB)
				{
					exSession_.GoToPreviousExpansionField();
					return VSConstants.S_OK;
				}
				else if (nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB)
				{

					exSession_.GoToNextExpansionField(0); //false to support cycling through all the fields
					return VSConstants.S_OK;
				}
				else if (nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN || nCmdID == (uint)VSConstants.VSStd2KCmdID.CANCEL)
				{
					if (exSession_.EndCurrentExpansion(0) == VSConstants.S_OK)
					{
						exSession_ = null;
						return VSConstants.S_OK;
					}
				}
			}
			//neither an expansion session nor a completion session is open, but we got a tab, so check whether the last word typed is a snippet shortcut 
			if (exSession_ == null && pguidCmdGroup == PackageGuids.HandyTools && nCmdID == (uint)PackageIds.CommandLineCompletion)
			{
				//get the word that was just added 
				//CaretPosition pos = textView_.Caret.Position;
				//TextExtent word = provider_.NavigatorService.GetTextStructureNavigator(textView_.TextBuffer).GetExtentOfWord(pos.BufferPosition - 1); //use the position 1 space back
				//string textString = word.Span.GetText(); //the word that was just added
														 //if it is a code snippet, insert it, otherwise carry on
				if (InsertAnyExpansion())
				{
					return VSConstants.S_OK;
				}
			}
			return nextCommandHandler_.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
		}

		public int GetExpansionFunction(IXMLDOMNode xmlFunctionNode, string bstrFieldName, out IVsExpansionFunction pFunc)
		{
			pFunc = null;
			return VSConstants.S_OK;
		}

		public int FormatSpan(IVsTextLines pBuffer, TextSpan[] ts)
		{
			return VSConstants.S_OK;
		}

		public int EndExpansion()
		{
			exSession_ = null;
			return VSConstants.S_OK;
		}

		public int IsValidType(IVsTextLines pBuffer, TextSpan[] ts, string[] rgTypes, int iCountTypes, out int pfIsValidType)
		{
			pfIsValidType = 1;
			return VSConstants.S_OK;
		}

		public int IsValidKind(IVsTextLines pBuffer, TextSpan[] ts, string bstrKind, out int pfIsValidKind)
		{
			pfIsValidKind = 1;
			return VSConstants.S_OK;
		}

		public int OnBeforeInsertion(IVsExpansionSession pSession)
		{
			return VSConstants.S_OK;
		}

		public int OnAfterInsertion(IVsExpansionSession pSession)
		{
			return VSConstants.S_OK;
		}

		public int PositionCaretForEditing(IVsTextLines pBuffer, TextSpan[] ts)
		{
			return VSConstants.S_OK;
		}

		public int OnItemChosen(string pszTitle, string pszPath)
		{
			return VSConstants.S_OK;
		}

		private bool InsertAnyExpansion()
		{
			//first get the location of the caret, and set up a TextSpan
			int endColumn, startLine;
			//get the column number from  the IVsTextView, not the ITextView
			vsTextView_.GetCaretPos(out startLine, out endColumn);

			TextSpan addSpan = new TextSpan();
			addSpan.iStartIndex = endColumn;
			addSpan.iEndIndex = endColumn;
			addSpan.iStartLine = startLine;
			addSpan.iEndLine = startLine;

			//if (shortcut != null) //get the expansion from the shortcut
			//{
			//    //reset the TextSpan to the width of the shortcut, 
			//    //because we're going to replace the shortcut with the expansion
			//    addSpan.iStartIndex = addSpan.iEndIndex - shortcut.Length;

			//    m_exManager.GetExpansionByShortcut(
			//        this,
			//        new Guid(SnippetUtilities.LanguageServiceGuidStr),
			//        shortcut,
			//        m_vsTextView,
			//        new TextSpan[] { addSpan },
			//        0,
			//        out path,
			//        out title);

			//}
			IVsTextLines textLines;
			vsTextView_.GetBuffer(out textLines);
			IVsExpansion bufferExpansion = (IVsExpansion)textLines;

			if (bufferExpansion != null)
			{
				DOMDocument domDoc = SnippetUtil.GenerateSnippetXml("test", "C++");
				int hr = bufferExpansion.InsertSpecificExpansion(
					domDoc,
					addSpan,
					this,
					Guid.Empty,
					null,
					out exSession_);
				if (VSConstants.S_OK == hr)
				{
					return true;
				}
			}
			return false;
		}

		private IOleCommandTarget nextCommandHandler_;
		private IVsTextView vsTextView_;
		private ITextView textView_;
		private AICompletionCommandHandlerProvider provider_;
		private IVsExpansionManager exManager_;
		private IVsExpansionSession exSession_;
	}
}
