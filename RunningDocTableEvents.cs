using System.Collections.Generic;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio;
using System.Linq;
using EnvDTE;
using System;
using UtfUnknown;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.Text;

namespace HandyTools
{
    internal class RunningDocTableEvents : IVsRunningDocTableEvents3
    {
        private readonly HandyToolsPackage package_;
        private readonly RunningDocumentTable runningDocumentTable_;

        public RunningDocTableEvents(HandyToolsPackage package)
        {
            package_ = package;
            runningDocumentTable_ = new RunningDocumentTable(package);
            runningDocumentTable_.Advise(this);
        }

        /**
        @brief Load a setting for the language
        */
        private void LoadSettings(out Types.TypeLineFeed linefeed, Types.TypeLanguage language, out Types.TypeEncoding encoding, string documentPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            linefeed = Types.TypeLineFeed.LF;
            encoding = Types.TypeEncoding.UTF8;

            //Load from a file
            if(package_.Options.LoadSettingFile) {
                SettingFile settingFile = package_.LoadFileSettings(documentPath);
                if(null != settingFile) {
                    linefeed = settingFile.Get(language);
                    return;
                }
            }
            //Otherwise, load from the option page
            OptionPageHandyTools optionPage = package_.Options;
            if(null != optionPage) {
                switch(language) {
                case Types.TypeLanguage.C_Cpp:
                    linefeed = optionPage.LineFeedCpp;
                    break;
                case Types.TypeLanguage.CSharp:
                    linefeed = optionPage.LineFeedCSharp;
                    break;
                default:
                    linefeed = optionPage.LineFeedOthers;
                    break;
                }
                encoding = optionPage.Encoding;
            }
        }

        private Document GetCurrentDocument(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            //Get the current document
            RunningDocumentInfo runningDocumentInfo = runningDocumentTable_.GetDocumentInfo(docCookie);
            EnvDTE.Document document = null;
            foreach(EnvDTE.Document doc in package_.DTE.Documents.OfType<EnvDTE.Document>()) {
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
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            Log.Output(string.Format("Load _handytools.xml: {0}\n", package_.Options.LoadSettingFile));

            //Get the current document
            EnvDTE.Document document = GetCurrentDocument(docCookie);
            if(null == document) {
                return VSConstants.S_OK;
            }
            if(document.Kind != "{8E7B96A8-E33D-11D0-A6D5-00C04FB67F6A}") {
                return VSConstants.S_OK;
            }
            ProjectItem projectItem = document.ProjectItem;
            EnvDTE.TextDocument textDocument = document.Object("TextDocument") as EnvDTE.TextDocument;
            if(null == textDocument) {
                return VSConstants.S_OK;
            }

            //Get the current language
            Types.TypeLanguage language = Types.TypeLanguage.Others;
            //Specify a target line-feed code
            LoadSettings(out var linefeed, language, out var encoding, document.Path);
            UtfUnknown.DetectionDetail charsetResult;
            try {
                charsetResult = UtfUnknown.CharsetDetector.DetectFromFile(document.FullName).Detected;
            } catch {
                return VSConstants.S_OK;
            }

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

        public int OnBeforeSave(uint docCookie)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            Log.Output(string.Format("Load _handytools.xml: {0}\n", package_.Options.LoadSettingFile));

            //Get the current document
            EnvDTE.Document document = GetCurrentDocument(docCookie);
            if(null == document) {
                return VSConstants.S_OK;
            }
            if(document.Kind != "{8E7B96A8-E33D-11D0-A6D5-00C04FB67F6A}") {
                return VSConstants.S_OK;
            }
            ProjectItem projectItem = document.ProjectItem;
            EnvDTE.TextDocument textDocument = document.Object("TextDocument") as EnvDTE.TextDocument;
            if(null == textDocument) {
                return VSConstants.S_OK;
            }

            //Get the current language
            Types.TypeLanguage language = Types.TypeLanguage.Others;
            switch(document.Language) {
            case "C/C++":
                language = Types.TypeLanguage.C_Cpp;
                break;
            case "CSharp":
                language = Types.TypeLanguage.CSharp;
                break;
            default:
                language = Types.TypeLanguage.Others;
                break;
            }

            //Specify a target line-feed code
            LoadSettings(out var linefeed, language, out var encoding, document.Path);

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
                    string lineending = editPoint.GetText(1);
                    if(string.IsNullOrEmpty(lineending)) {
                        continue;
                    }

                    for(int i = 0; i < lineending.Length; ++i) {
                        switch(lineending[i]) {
                        case '\n':
                            editPoint.ReplaceText(1, replaceLineFeed, 0);
                            ++count;
                            break;
                        case '\r':
                            if((lineending.Length - 1) <= i) {
                                editPoint.ReplaceText(1, replaceLineFeed, 0);
                                ++count;
                            } else if('\n' != lineending[i + 1]) {
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
                    string lineending = editPoint.GetText(1);
                    if(string.IsNullOrEmpty(lineending)) {
                        continue;
                    }

                    for(int i = 0; i < lineending.Length; ++i) {
                        if(lineending[i] != replaceLineFeed[0]) {
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
