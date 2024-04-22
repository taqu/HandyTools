using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using MSXML;

namespace HandyTools.Completion
{
	internal class AICompletionCommandHandler : IOleCommandTarget, IVsExpansionClient
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
			return VSConstants.S_OK;
		}

		public int GetExpansionFunction(IXMLDOMNode xmlFunctionNode, string bstrFieldName, out IVsExpansionFunction pFunc)
		{
			throw new NotImplementedException();
		}

		public int FormatSpan(IVsTextLines pBuffer, TextSpan[] ts)
		{
			throw new NotImplementedException();
		}

		public int EndExpansion()
		{
			throw new NotImplementedException();
		}

		public int IsValidType(IVsTextLines pBuffer, TextSpan[] ts, string[] rgTypes, int iCountTypes, out int pfIsValidType)
		{
			throw new NotImplementedException();
		}

		public int IsValidKind(IVsTextLines pBuffer, TextSpan[] ts, string bstrKind, out int pfIsValidKind)
		{
			throw new NotImplementedException();
		}

		public int OnBeforeInsertion(IVsExpansionSession pSession)
		{
			throw new NotImplementedException();
		}

		public int OnAfterInsertion(IVsExpansionSession pSession)
		{
			throw new NotImplementedException();
		}

		public int PositionCaretForEditing(IVsTextLines pBuffer, TextSpan[] ts)
		{
			throw new NotImplementedException();
		}

		public int OnItemChosen(string pszTitle, string pszPath)
		{
			throw new NotImplementedException();
		}

		private IOleCommandTarget nextCommandHandler_;
		private ITextView textView_;
		private AICompletionHandlerProvider provider_;
	}
}
