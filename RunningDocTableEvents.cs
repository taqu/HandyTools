using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio;
using System.Linq;
using EnvDTE;
using System.IO;
using System.Text;
using HandyTools.Commands;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using static HandyTools.SettingFile;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;

namespace HandyTools
{
    internal class RunningDocTableEvents : IVsRunningDocTableEvents3
    {
        private readonly RunningDocumentTable runningDocumentTable_;

        public RunningDocTableEvents(HandyToolsPackage package)
        {
            runningDocumentTable_ = new RunningDocumentTable(package);
            runningDocumentTable_.Advise(this);
        }

        private Document GetCurrentDocument(uint docCookie, HandyToolsPackage package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            //Get the current document
            RunningDocumentInfo runningDocumentInfo = runningDocumentTable_.GetDocumentInfo(docCookie);
            EnvDTE.Document document = null;
            foreach(EnvDTE.Document doc in package.DTE.Documents.OfType<EnvDTE.Document>()) {
                if(doc.FullName == runningDocumentInfo.Moniker) {
                    document = doc;
                    break;
                }
            }
            return document;
        }

        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
        {
            return VSConstants.S_OK;
        }


        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie)
        {
#if false
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            //Get the current document
            EnvDTE.Document document = GetCurrentDocument(docCookie);
            if(null == document) {
                return VSConstants.S_OK;
            }
            if(document.Kind != EnvDTE.Constants.vsDocumentKindText) {
                return VSConstants.S_OK;
            }

			EnvDTE.TextDocument textDocument = document.Object("TextDocument") as EnvDTE.TextDocument;
            if(null == textDocument) {
                return VSConstants.S_OK;
            }

            //Check current encoding
            Types.TypeLanguage language = Types.TypeLanguage.Others;
            LoadSettings(out var linefeed, language, out var encoding, document.Path);
            UtfUnknown.DetectionDetail charsetResult;
            try {
                charsetResult = UtfUnknown.CharsetDetector.DetectFromFile(document.FullName).Detected;
            } catch {
                return VSConstants.S_OK;
            }

            //Overwrite if needed
            bool write = false;
            if(0.5f <= charsetResult.Confidence) {
                switch(encoding) {
                case Types.TypeEncoding.UTF8:
                    if(charsetResult.HasBOM || (System.Text.Encoding.UTF8 != charsetResult.Encoding && System.Text.Encoding.ASCII != charsetResult.Encoding)) {
                        write = true;
                    }
                    break;
                case Types.TypeEncoding.UTF8BOM:
                    if(!charsetResult.HasBOM || (System.Text.Encoding.UTF8 != charsetResult.Encoding && System.Text.Encoding.ASCII != charsetResult.Encoding)) {
                        write = true;
                    }
                    break;
                }
            } else {
                write = true;
            }

            if(write) {
                try {
                    EnvDTE.EditPoint editPoint = textDocument.StartPoint.CreateEditPoint();
                    string text = editPoint.GetText(textDocument.EndPoint);
                    UTF8Encoding utf8Encoding = new UTF8Encoding(encoding == Types.TypeEncoding.UTF8BOM);
					File.WriteAllText(document.FullName, text, utf8Encoding);

				} catch {
                }
            }
#endif
            return VSConstants.S_OK;
        }

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }


        public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
        {
            return VSConstants.S_OK;
        }

        private ITextDocument GetTextDocument(EnvDTE.Document document)
        {
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

			Community.VisualStudio.Toolkit.Documents documents = Community.VisualStudio.Toolkit.VS.Documents;
            if(null != documents)
            {
				var task = ThreadHelper.JoinableTaskFactory.RunAsync(async ()=>
				{
					await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
					return await documents.GetDocumentViewAsync(document.FullName);
				});
                DocumentView documentView = task.Join();
                if (null == documentView || null ==documentView.Document || document.FullName != documentView.FilePath)
                {
                    return null;
				}
                return documentView.Document;
			}
            return null;
		}

        private void SetEncoding(EnvDTE.Document document, ITextDocument textDocument, TextSettings textSettings)
        {
			ThreadHelper.ThrowIfNotOnUIThread();
			HandyToolsPackage package = HandyToolsPackage.GetPackage();
			if (null == package)
			{
				return;
			}
			UtfUnknown.DetectionDetail charsetResult;
            try {
                charsetResult = UtfUnknown.CharsetDetector.DetectFromFile(document.FullName).Detected;
            } catch {
                return;
            }

            //Overwrite if needed
            bool write = false;
            if(0.5f <= charsetResult.Confidence) {
                switch(textSettings.Encoding) {
                case Types.TypeEncoding.UTF8:
                    if(charsetResult.HasBOM || (System.Text.Encoding.UTF8 != charsetResult.Encoding && System.Text.Encoding.ASCII != charsetResult.Encoding)) {
                        write = true;
                    }
                    break;
                case Types.TypeEncoding.UTF8BOM:
                    if(!charsetResult.HasBOM || (System.Text.Encoding.UTF8 != charsetResult.Encoding && System.Text.Encoding.ASCII != charsetResult.Encoding)) {
                        write = true;
                    }
                    break;
                }
            } else {
                write = true;
            }
            Log.Output(string.Format("{0} Confidence {1} BOM {2}\n", charsetResult.EncodingName, charsetResult.Confidence, charsetResult.HasBOM));

            if(write) {
                try
                {
					switch (textSettings.Encoding)
                    {
                        case Types.TypeEncoding.UTF8:
							textDocument.Encoding = new UTF8Encoding(false, true);
                            textDocument.UpdateDirtyState(true, DateTime.Now);
							break;
                        case Types.TypeEncoding.UTF8BOM:
							textDocument.Encoding = new UTF8Encoding(true, true);
							textDocument.UpdateDirtyState(true, DateTime.Now);
							break;
                    }
                }
                catch
                {
                }
            }
		}

		public int OnBeforeSave(uint docCookie)
        {
			HandyToolsPackage package = HandyToolsPackage.GetPackage();
			if (null == package)
			{
				return VSConstants.S_OK;
			}
			//Get the current document
			EnvDTE.Document document = GetCurrentDocument(docCookie, package);
			if (null == document) {
                return VSConstants.S_OK;
            }
			ITextDocument itextDocument = GetTextDocument(document);
            if(null == itextDocument)
            {
                return VSConstants.S_OK;
            }

			ThreadHelper.ThrowIfNotOnUIThread();
			TextSettings textSettings = package.GetTextSettings(document.FullName);

			SetEncoding(document, itextDocument, textSettings);

			if (document.Kind != EnvDTE.Constants.vsDocumentKindText) {
                return VSConstants.S_OK;
            }
			EnvDTE.TextDocument textDocument = document.Object("TextDocument") as EnvDTE.TextDocument;
            if(null == textDocument) {
				return VSConstants.S_OK;
			}
			//Get the current language
			Types.TypeLanguage language = CodeUtil.GetLanguageFromDocument(document);

			//Specify a target line-feed code
            Types.TypeLineFeed linefeed = textSettings.Get(language);
			Log.Output(string.Format("doc:{0} => lang:{1} code:{2}\n", document.Language, language, linefeed));
            string replaceLineFeed;
            switch(linefeed) {
            case Types.TypeLineFeed.LF:
                replaceLineFeed = "\n";
                break;
            case Types.TypeLineFeed.CR:
                replaceLineFeed = "\r";
                break;
            //case OptionPageForceLineFeedCode.TypeLineFeed.CRLF:
            default:
                replaceLineFeed = "\r\n";
                break;
            }

            //Convert different codes
            int count = 0;
            EnvDTE.EditPoint editPoint = textDocument.StartPoint.CreateEditPoint();
            if(Types.TypeLineFeed.CRLF == linefeed) {
                while(!editPoint.AtEndOfDocument) {
                    editPoint.EndOfLine();
                    string linefeeding = editPoint.GetText(1);
                    if(string.IsNullOrEmpty(linefeeding)) {
                        continue;
                    }

                    for(int i = 0; i < linefeeding.Length; ++i) {
                        switch(linefeeding[i]) {
                        case '\n':
                            editPoint.ReplaceText(1, replaceLineFeed, 0);
                            ++count;
                            break;
                        case '\r':
                            if((linefeeding.Length - 1) <= i) {
                                editPoint.ReplaceText(1, replaceLineFeed, 0);
                                ++count;
                            } else if('\n' != linefeeding[i + 1]) {
                                editPoint.ReplaceText(1, replaceLineFeed, 0);
                                ++count;
                            } else {
                                ++i;
                            }
                            break;
                        default:
                            break;
                        }
                    }
                    editPoint.CharRight();
                }

            } else {
                while(!editPoint.AtEndOfDocument) {
                    editPoint.EndOfLine();
                    string linefeeding = editPoint.GetText(1);
                    if(string.IsNullOrEmpty(linefeeding)) {
                        continue;
                    }

                    for(int i = 0; i < linefeeding.Length; ++i) {
                        if(linefeeding[i] != replaceLineFeed[0]) {
                            editPoint.ReplaceText(1, replaceLineFeed, 0);
                            ++count;
                        }
                    }
                    editPoint.CharRight();
                }
            }
            Log.Output(string.Format("Replace {0} EOLs\n", count));
            return VSConstants.S_OK;
        }
    }
}
