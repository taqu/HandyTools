using EnvDTE;
using LLama;
using LLama.Common;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.RpcContracts.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HandyTools.Completion
{
    public record struct Completion(
        string id,
        string text,
        string stop,
        double score,
        ulong[] tokens,
        List<string> decodedTokens,
        double[] probabilities,
        double[] adjustedProbabilities,
        ulong generatedLength,
		int startOffset,
		int endOffset
		);

    public class CompletionModel
    {
		public const string ModelName = "starcoderbase-1b.Q4_K_M.gguf";
		private InteractiveExecutor executor_;

		public static async Task<CompletionModel> InitializeAsync()
		{
			string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			path = System.IO.Path.Combine(path, ModelName);
			System.IO.FileInfo fileInfo = new System.IO.FileInfo(path);
			if (!fileInfo.Exists)
			{
				return null;
			}
			ModelParams modelParams = new ModelParams(path)
			{
				ContextSize = 4096
			};
			try
			{
				LLamaWeights weights = await LLamaWeights.LoadFromFileAsync(modelParams);
				InteractiveExecutor executor = new InteractiveExecutor(weights.CreateContext(modelParams));
				return new CompletionModel(executor);
			}
			catch
			{
				return null;
			}
		}

		private CompletionModel(InteractiveExecutor executor){
			executor_ = executor;
		}

		public async Task<IList<Completion>?> GetCompletionsAsync(
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
			List<Completion> completions = new List<Completion>();
			Completion completion = new Completion();
			completion.id = Guid.NewGuid().ToString();
			completion.text = "test";
			completions.Add(completion);
			completion.startOffset = cursorPosition;
			completion.endOffset = cursorPosition;
			return completions;
        }

        public async Task AcceptCompletionAsync(string completionId)
        {
            //AcceptCompletionRequest data =
            //    new() { metadata = GetMetadata(), completion_id = completionId };

            //await RequestCommandAsync<AcceptCompletionResponse>("AcceptCompletion", data);
        }
    }
}
