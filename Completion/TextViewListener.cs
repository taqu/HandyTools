using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE80;

namespace HandyTools.Completion
{

internal class HandyToolsCompletionHandler : IOleCommandTarget, IDisposable
{
    private readonly HandyToolsPackage package_;

    private readonly ITextView textView_;
    private readonly IVsTextView vsTextView_;
    private readonly ITextDocument textDocument_;

    private DateTime lastRequest_ = DateTime.MinValue;

    private LanguageInfo language_;
    private CancellationTokenSource? requestTokenSource_;
    private readonly TimeSpan intelliSenseDelay_ = TimeSpan.FromMilliseconds(250.0);

    private IOleCommandTarget nextCommandHandler_;
    private TextViewListener provider_;
    private CancellationTokenSource currentCancellTokenSource_;
    private CancellationToken currentCancellToken_;

    private string currentCompletionID_;
    private bool hasCompletionUpdated_;
    private List<Tuple<String, String>> suggestions_;
    private int suggestionIndex_;
    private Command completeSuggestionCommand_;

    [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)] 
    public static extern short GetAsyncKeyState(Int32 keyCode);

    public async void GetCompletion()
    {
        try
        {
            if (textDocument_ == null || null == package_)
            {
                return;
            }

            UpdateRequestTokenSource(new CancellationTokenSource());

            SnapshotPoint? caretPoint = textView_.Caret.Position.Point.GetPoint(
                textBuffer => (!textBuffer.ContentType.IsOfType("projection")),
                PositionAffinity.Successor);
            if (!caretPoint.HasValue)
            {
                return;
            }

            var caretPosition = caretPoint.Value.Position;

            string text = textDocument_.TextBuffer.CurrentSnapshot.GetText();
            int cursorPosition = textDocument_.Encoding.IsSingleByte
                ? caretPosition
                : Utf16OffsetToUtf8Offset(text, caretPosition);

            if (cursorPosition > text.Length)
            {
                Debug.Print("Error Caret past text position");
                return;
            }

            IList<Completion>? list = await package_.CompletionModel.GetCompletionsAsync(
                textDocument_.FilePath,
                text,
                language_,
                cursorPosition,
                textView_.Options.GetOptionValue(DefaultOptions.NewLineCharacterOptionId),
                textView_.Options.GetOptionValue(DefaultOptions.TabSizeOptionId),
                textView_.Options.GetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId),
                currentCancellTokenSource_.Token);

            int lineN;
            int characterN;

            int res = vsTextView_.GetCaretPos(out lineN, out characterN);
            String line = textView_.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(lineN).GetText();
				await Log.OutputAsync("completions " + list.Count.ToString());

            if (res != VSConstants.S_OK)
            {
                return;
            }

            if (list != null && list.Count > 0)
            {
					await Log.OutputAsync("completions " + list.Count.ToString());

                string prefix = line.Substring(0, Math.Min(characterN, line.Length));
                suggestions_ = ParseCompletion(list, text, line, prefix, characterN);

                CompletionTagger tagger = GetTagger();
                if (suggestions_ != null && suggestions_.Count > 0 && tagger != null)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    suggestionIndex_ = 0;
                    currentCompletionID_ = suggestions_[0].Item2;
                    var valid = tagger.SetSuggestion(suggestions_[0].Item1, characterN);
                }

                await Log.OutputAsync("Generated " + list.Count + $" proposals");
            }

        }
        catch (Exception ex)
        {
            await Log.OutputAsync("Exception: " + ex.ToString());
        }
    }

    List<Tuple<String, String>> ParseCompletion(IList<Completion> completionItems,
                                                string text, string line, string prefix,
                                                int cursorPoint)
    {
        if (completionItems == null || completionItems.Count <=0) {
                return null;
            }

        List<Tuple<String, String>> list = new List<Tuple<String, String>>(completionItems.Count);
        for (int i = 0; i < completionItems.Count; ++i)
        {
				Completion completion = completionItems[i];
                if(string.IsNullOrEmpty(completion.text)){
                    continue;
                }
            int startOffset = completion.startOffset;
            int endOffset = completion.endOffset;

            if (!textDocument_.Encoding.IsSingleByte)
            {
                startOffset = Utf8OffsetToUtf16Offset(text, startOffset);
                endOffset = Utf8OffsetToUtf16Offset(text, endOffset);
            }
            if (text.Length<endOffset) {
                    endOffset = text.Length;
                }
            string end = text.Substring(endOffset);
            String completionText = completion.text;
            if (!String.IsNullOrEmpty(end))
            {
                int endNewline = StringUtils.IndexOfNewLine(end);

                    if (endNewline <= -1)
                    {
                        endNewline = end.Length;
                    }

                completionText = completionText + end.Substring(0, endNewline);
            }
            int offset = StringUtils.CheckSuggestion(completionText, prefix);
            if (offset < 0 || completionText.Length < offset) {
                    continue;
                }

            completionText = completionText.Substring(offset);
            var set = new Tuple<String, String>(completionText, completion.id);

            // Filter out completions that don't match the current intellisense prefix
            ICompletionSession session = provider_.CompletionBroker.GetSessions(textView_).FirstOrDefault();
            if (session != null && session.SelectedCompletionSet != null)
            {
					Microsoft.VisualStudio.Language.Intellisense.Completion completionStatus = session.SelectedCompletionSet.SelectionStatus.Completion;
                if (completionStatus == null) {
                        continue;
                    }
                string intellisenseSuggestion = completionStatus.InsertionText;
                ITrackingSpan intellisenseSpan = session.SelectedCompletionSet.ApplicableTo;
                SnapshotSpan span = intellisenseSpan.GetSpan(intellisenseSpan.TextBuffer.CurrentSnapshot);
                if (intellisenseSuggestion.Length< span.Length) {
                        continue;
                    }
                string intellisenseInsertion = intellisenseSuggestion.Substring(span.Length);
                if (!completionText.StartsWith(intellisenseInsertion))
                {
                    continue;
                }
            }
            list.Add(set);
        }

        return list;
    }

    public record KeyItem(string Name, string KeyBinding, string Category, string Scope);

    public static async Task<Command> GetCommandsAsync(String name)
    {
        try
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            List<Command> items = new();
            DTE2 dte = await VS.GetServiceAsync<DTE, DTE2>();

            foreach (Command command in dte.Commands)
            {
                if (string.IsNullOrEmpty(command.Name))
                {
                    continue;
                }

                if (command.Name.Contains(name) && command.Bindings is object[] bindings)
                {
                    items.Add(command);
                }
            }

            if (items.Count > 0)
            {
                return items[0];
            }
        }
        catch (Exception e)
        {
                Log.OutputAsync(e.Message);
        }

        return null;
    }

    private void OnSuggestionAccepted(String proposalId)
    {
        // unfortunately in the SDK version 17.5.33428.388, there are no
        // SuggestionAcceptedEventArgs so we have to use reflection here
        ThreadHelper.JoinableTaskFactory
            .RunAsync(async delegate {
                await Log.OutputAsync($"Accepted completion {proposalId}");
                await HandyToolsPackage.GetPackage().CompletionModel.AcceptCompletionAsync(proposalId);
            })
            .FireAndForget(true);
    }

    public LanguageInfo GetLanguage()
    { 
        return language_;
    }

    private void UpdateRequestTokenSource(CancellationTokenSource newSource)
    {
        if (currentCancellTokenSource_ != null)
        {
            currentCancellTokenSource_.Cancel();
            currentCancellTokenSource_.Dispose();
        }
        currentCancellTokenSource_ = newSource;
    }

    public static int Utf16OffsetToUtf8Offset(string str, int utf16Offset)
    {
        return Encoding.UTF8.GetByteCount(str.ToCharArray(), 0, utf16Offset);
    }

    public static int Utf8OffsetToUtf16Offset(string str, int utf8Offset)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        return Encoding.UTF8.GetString(bytes.Take(utf8Offset).ToArray()).Length;
    }

    internal HandyToolsCompletionHandler(IVsTextView textViewAdapter, ITextView view,
        TextViewListener provider)
    {
        try
        {
                package_ = HandyToolsPackage.GetPackage();
            textView_ = view;
            provider_ = provider;
            var topBuffer = view.BufferGraph.TopBuffer;

            var projectionBuffer = topBuffer as IProjectionBufferBase;

            ITextBuffer textBuffer =
                projectionBuffer != null ? projectionBuffer.SourceBuffers[0] : topBuffer;
            provider.documentFactory.TryGetTextDocument(textBuffer, out textDocument_);

            if (textDocument_ != null)
            {
                    Log.Output("HandyToolsCompletionHandler filepath = " + textDocument_.FilePath);

                if (!provider.documentDictionary.ContainsKey(textDocument_.FilePath.ToLower()))
                {
                    provider.documentDictionary.Add(textDocument_.FilePath.ToLower(), textDocument_);
                }

                textDocument_.FileActionOccurred += OnFileActionOccurred;
                textDocument_.TextBuffer.ContentTypeChanged += OnContentTypeChanged;
                RefreshLanguage();
            }

            vsTextView_ = textViewAdapter;
            // add the command to the command chain
            textViewAdapter.AddCommandFilter(this, out nextCommandHandler_);
            // ShowIntellicodeMsg();

            view.Caret.PositionChanged += CaretUpdate;

            _ = Task.Run(() =>
            {
                try
                {
                    completeSuggestionCommand_ = GetCommandsAsync("HandyToolsAcceptCompletion").Result;
                }
                catch (Exception e)
                {
                    Debug.Write(e);
                }
            });

        }
        catch (Exception e)
        {
                Log.OutputAsync(e.Message);
        }
    }

    private void CaretUpdate(object sender, CaretPositionChangedEventArgs e)
    {
        try
        {
            var tagger = GetTagger();
            if (tagger == null)
            {
                return;
            }

            if (completeSuggestionCommand_ != null && completeSuggestionCommand_.Bindings is object[] bindings &&
                bindings.Length > 0)
            {
                tagger.ClearSuggestion();
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
                Log.Output(ex.Message);
        }
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
        try
        {
            if (textDocument_ != null)
            {
                language_ = SupportedLanguage.GetLanguage(textDocument_.TextBuffer.ContentType,
                    Path.GetExtension(textDocument_.FilePath)?.Trim('.'));
            }
        }
        catch (Exception ex)
        {

        }
    }

    public async void ShowNextSuggestion()
    {
        try
        {
            if (suggestions_ != null && suggestions_.Count > 1)
            {

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var oldSuggestion = suggestionIndex_;
                suggestionIndex_ = (suggestionIndex_ + 1) % suggestions_.Count;
                currentCompletionID_ = suggestions_[suggestionIndex_].Item2;

                CompletionTagger tagger = GetTagger();
                if (tagger == null) { return; }

                int lineN, characterN;
                int res = vsTextView_.GetCaretPos(out lineN, out characterN);

                if (res != VSConstants.S_OK)
                {
                    suggestionIndex_ = oldSuggestion;
                    currentCompletionID_ = suggestions_[suggestionIndex_].Item2;
                    return;
                }

                bool validSuggestion = tagger.SetSuggestion(suggestions_[suggestionIndex_].Item1, characterN);
                if (!validSuggestion)
                {
                    suggestionIndex_ = oldSuggestion;
                    currentCompletionID_ = suggestions_[suggestionIndex_].Item2;

                    tagger.SetSuggestion(suggestions_[suggestionIndex_].Item1, characterN);
                }
            }
        }
        catch (Exception ex)
        {
                Log.Output(ex.Message);
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
                OnSuggestionAccepted(currentCompletionID_);
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

    // Used to detect when the user interacts with the intellisense popup
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
    private CompletionTagger GetTagger()
    {
        var key = typeof(CompletionTagger);
        var props = textView_.TextBuffer.Properties;
        if (props.ContainsProperty(key)) { return props.GetProperty<CompletionTagger>(key); }
        else { return null; }
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
                this.package_,
                "Please disable IntelliCode to use Codeium. You can access Intellicode settings via Tools --> Options --> Intellicode.",
                "Disable IntelliCode",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }

    public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn,
        IntPtr pvaOut)
        {
        vsTextView_.RemoveCommandFilter(this);
        vsTextView_.AddCommandFilter(this, out nextCommandHandler_);
        
        // let the other handlers handle automation functions
        if (VsShellUtilities.IsInAutomationFunction(provider_.ServiceProvider))
        {
            return nextCommandHandler_.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        
        // check for a commit character
        bool regenerateSuggestion = false;
        if (!hasCompletionUpdated_ && nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB)
        {
            if (completeSuggestionCommand_ != null)
            {
                var bindings = completeSuggestionCommand_.Bindings as object[];
                if (bindings == null || bindings.Length <= 0)
                {
                    var tagger = GetTagger();
                    if (tagger == null) { return nextCommandHandler_.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut); }

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
            }
        }
        else if (nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN ||
                 nCmdID == (uint)VSConstants.VSStd2KCmdID.CANCEL)
        {
            ClearSuggestion();
        }

        CheckSuggestionUpdate(nCmdID);

        // make a copy of this so we can look at it after forwarding some commands
        uint commandID = nCmdID;
        char typedChar = char.MinValue;

        // make sure the input is a char before getting it
        if (pguidCmdGroup == VSConstants.VSStd2K &&
            nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR)
        {
            typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
        }

        // pass along the command so the char is added to the buffer
        int retVal =
            nextCommandHandler_.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        bool handled = false;

        if (hasCompletionUpdated_) { ClearSuggestion(); }
        // gets lsp completions on added character or deletions
        if (!typedChar.Equals(char.MinValue) || commandID == (uint)VSConstants.VSStd2KCmdID.RETURN || regenerateSuggestion)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    GetCompletion();
                }
                catch (Exception e)
                {
                    Debug.Write(e);
                }
            });
            handled = true;
        }
        else if (commandID == (uint)VSConstants.VSStd2KCmdID.BACKSPACE ||
                 commandID == (uint)VSConstants.VSStd2KCmdID.DELETE)
        {
            ClearSuggestion();

            _ = Task.Run(() =>
            {
                try
                {
                    GetCompletion();
                }
                catch (Exception e)
                {
                    Debug.Write(e);
                }
            });
            handled = true;
        }

        if (handled) return VSConstants.S_OK;
        return retVal;
    }

    // clears the intellisense popup window
    void ClearCompletionSessions() { provider_.CompletionBroker.DismissAllSessions(textView_); }

    public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
    {
        //package.LogAsync("QueeryStatus " + cCmds + " prgCmds = " + prgCmds + "pcmdText " + pCmdText);
        return nextCommandHandler_.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
    }

    public void Dispose()
    {
        if (textDocument_ != null)
        {
            textDocument_.FileActionOccurred -= OnFileActionOccurred;
            textDocument_.TextBuffer.ContentTypeChanged -= OnContentTypeChanged;
        }
        UpdateRequestTokenSource(null);
    }
}

[Export(typeof(IVsTextViewCreationListener))]
[Name("TextViewListener")]
[ContentType("code")]
[TextViewRole(PredefinedTextViewRoles.Document)]

internal class TextViewListener : IVsTextViewCreationListener
{
    // adapters are used to get the IVsTextViewAdapter from the IVsTextView
    [Import]
    internal IVsEditorAdaptersFactoryService AdapterService = null;

    // service provider is used to get the IVsServiceProvider which is needed to access lsp
    [Import]
    internal SVsServiceProvider ServiceProvider { get; set; }

    // CompletionBroker is used by intellisense (popups) to provide completion items.
    [Import]
    internal ICompletionBroker CompletionBroker {
        get; set;
    }

    // document factory is used to get information about the current text document such as filepath,
    // language, etc.
    [Import]
    internal ITextDocumentFactoryService documentFactory = null;

    internal static TextViewListener? Instance { get; private set; }

    public Dictionary<string, ITextDocument> documentDictionary = new Dictionary<string, ITextDocument>();
    public void VsTextViewCreated(IVsTextView textViewAdapter)
    {
        Instance = this;
        ITextView textView = AdapterService.GetWpfTextView(textViewAdapter);
        if (textView == null) return;

        Func<HandyToolsCompletionHandler> createCommandHandler = delegate()
        {
            return new HandyToolsCompletionHandler(textViewAdapter, textView, this);
        };
        textView.TextBuffer.Properties.GetOrCreateSingletonProperty<HandyToolsCompletionHandler>(typeof(HandyToolsCompletionHandler), createCommandHandler);
    }
}
}
