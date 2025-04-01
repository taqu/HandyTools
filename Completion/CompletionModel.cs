using EnvDTE;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.RpcContracts.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HandyTools.Completion
{
    public record struct Completion(
        string competionId,
        string text,
        string stop,
        double score,
        ulong[] tokens,
        List<string> decodedTokens,
        double[] probabilities,
        double[] adjustedProbabilities,
        ulong generatedLength
        );
    public record struct CompletionItem(
        Completion completion
        );

    public class CompletionModel
    {
        public async Task<IList<CompletionItem>?> GetCompletionsAsync(
            string absolutePath, string text, LanguageInfo language,
            int cursorPosition, string lineEnding, int tabSize, bool insertSpaces,
            CancellationToken token)
        {
            //if (!_initializedWorkspace)
            //{
            //    await InitializeTrackedWorkspaceAsync();
            //}
            //var uri = new System.Uri(absolutePath);
            //var absoluteUri = uri.AbsoluteUri;
            //GetCompletionsRequest data =
            //    new()
            //    {
            //        metadata = GetMetadata(),
            //        document = new()
            //        {
            //            text = text,
            //            editor_language = language.Name,
            //            language = language.Type,
            //            cursor_offset = (ulong)cursorPosition,
            //            line_ending = lineEnding,
            //            absolute_path = absolutePath,
            //            absolute_uri = absoluteUri,
            //            relative_path = Path.GetFileName(absolutePath)
            //        },
            //        editor_options = new()
            //        {
            //            tab_size = (ulong)tabSize,
            //            insert_spaces = insertSpaces,
            //            disable_autocomplete_in_comments =
            //                    !_package.SettingsPage.EnableCommentCompletion,
            //        }
            //    };

            //GetCompletionsResponse? result =
            //    await RequestCommandAsync<GetCompletionsResponse>("GetCompletions", data, token);
            //return result != null ? result.completionItems : [];
            return null;
        }

        public async Task AcceptCompletionAsync(string completionId)
        {
            //AcceptCompletionRequest data =
            //    new() { metadata = GetMetadata(), completion_id = completionId };

            //await RequestCommandAsync<AcceptCompletionResponse>("AcceptCompletion", data);
        }
    }
}
