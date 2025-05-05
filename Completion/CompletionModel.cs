using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

    public class CompletionModel : IDisposable
    {
		[DllImport("cplm.dll")]
		static extern int get_int();

		[DllImport("cplm.dll", CharSet = CharSet.Ansi)]
		static extern unsafe IntPtr create_model(ulong size, byte* memory, int context);

		[DllImport("cplm.dll", CharSet = CharSet.Ansi)]
		static extern void destroy_model(IntPtr model);

		[DllImport("cplm.dll", CharSet = CharSet.Ansi)]
		static extern int generate_one(
			IntPtr model,
			int size,
			StringBuilder generated,
			string text,
			int context,
			ulong seed,
			float temperature,
			float minp,
			int steps);

		public const string ModelName = "qwen2.5-coder.calm";

		private bool disposed_ = false;
		private IntPtr model_ = IntPtr.Zero;

		public static async Task<CompletionModel> InitializeAsync()
		{
			string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			path = System.IO.Path.Combine(path, ModelName);
			System.IO.FileInfo fileInfo = new System.IO.FileInfo(path);
			if (!fileInfo.Exists)
			{
				return null;
			}
			try
			{
				using (FileStream stream = fileInfo.OpenRead())
				{
					byte[] buffer = new byte[fileInfo.Length];
					int size = await stream.ReadAsync(buffer, 0, buffer.Length);
					if (size <= 0)
					{
						return null;
					}
					IntPtr ptr = IntPtr.Zero;
					unsafe
					{
						fixed (byte* bytes = buffer)
						{
							ptr = create_model((ulong)buffer.LongLength, bytes, 4096);
							if (ptr == IntPtr.Zero)
							{
								return null;
							}
						}
					}
					CompletionModel model = new CompletionModel();
					model.model_ = ptr;
					return model;
				}
			}
			catch
			{
				return null;
			}
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

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposed_)
			{
				if(model_ != IntPtr.Zero)
				{
					destroy_model(model_);
					model_ = IntPtr.Zero;
				}
				disposed_ = true;
			}
		}

		~CompletionModel()
		{
			Dispose(false);
		}
	}
}
