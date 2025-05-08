using EnvDTE80;
using Microsoft.VisualStudio.Debugger.Interop;
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

	public struct AcceptCompletionRequest
	{
		public string completion_id_ { get; set; }
		public AcceptCompletionRequest()
		{
			completion_id_ = string.Empty;
		}
	}

	public class CompletionModel : IDisposable
    {
		[DllImport("cplm.dll", CharSet = CharSet.Ansi)]
		static extern unsafe IntPtr create_model(ulong size, IntPtr memory, int context);

		[DllImport("cplm.dll", CharSet = CharSet.Ansi)]
		static extern void destroy_model(IntPtr model);

		[DllImport("cplm.dll", CharSet = CharSet.Ansi)]
		static extern int generate_one(
			IntPtr model,
			int size,
			StringBuilder generated,
			string text,
			int context,
			ulong seed=0,
			float temperature=1.0f,
			float minp=0.1f,
			int steps=4096);

#if false
		public const int MaxQuery = 3072;
		public const int MaxResponse = 4096;
#else
		public const int MaxQuery = 1024;
		public const int MaxResponse = 2048;
#endif
		public const string ModelName = "qwen2.5-coder.calm";

		private bool disposed_ = false;
		private IntPtr model_ = IntPtr.Zero;
		private StringBuilder buffer_ = new StringBuilder(4096);

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
							ptr = create_model((ulong)buffer.LongLength, (IntPtr)bytes, 4096);
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
			if (IntPtr.Zero == model_)
			{
				return null;
			}
			if(language.language == Language.None)
			{
				return null;
			}
#if false
			var uri = new System.Uri(absolutePath);
			var absoluteUri = uri.AbsoluteUri;
			GetCompletionsRequest data =
				new()
				{
					metadata = GetMetadata(),
					document = new()
					{
						text = text,
						editor_language = language.Name,
						language = language.Type,
						cursor_offset = (ulong)cursorPosition,
						line_ending = lineEnding,
						absolute_path = absolutePath,
						absolute_uri = absoluteUri,
						relative_path = Path.GetFileName(absolutePath)
					},
					editor_options = new()
					{
						tab_size = (ulong)tabSize,
						insert_spaces = insertSpaces,
						disable_autocomplete_in_comments =
								!_package.SettingsPage.EnableCommentCompletion,
					}
				};

			GetCompletionsResponse? result =
				await RequestCommandAsync<GetCompletionsResponse>("GetCompletions", data, token);
			return result != null ? result.completionItems : [];
#endif
			string query = createQuery(text, cursorPosition, 0.5f, MaxQuery);

			StringBuilder generated = new StringBuilder(MaxResponse);
			int len = generate_one(model_, MaxResponse, generated, query, MaxResponse, 0, 1.0f, 0.1f, MaxResponse);
			List<Completion> completions = new List<Completion>();
			string suggestion = getSuffix(generated);
			if (0 < len && !string.IsNullOrEmpty(suggestion))
			{
				Completion completion = new Completion();
				completion.id = Guid.NewGuid().ToString();
				completion.text = suggestion;
				completion.startOffset = cursorPosition;
				completion.endOffset = cursorPosition;
				completions.Add(completion);
			}
			return completions;
        }
		public const string Prefix = "<|fim_prefix|>";
		public const string Suffix = "<|fim_suffix|>";
		public const string Middle = "<|fim_middle|>";

		private string createQuery(string text, int cursorPosition, float prefix_rate, int max_length)
		{
			char c = text[cursorPosition];
			prefix_rate = Math.Min(1.0f, Math.Max(0.0f, prefix_rate));
			int prefix_max = (int)(max_length * prefix_rate);

			int prefix_start = Math.Max(0, cursorPosition - prefix_max);
			for (; prefix_start < cursorPosition; ++prefix_start)
			{
				if (char.IsWhiteSpace(text[prefix_start]))
				{
					++prefix_start;
					while (prefix_start < cursorPosition && char.IsWhiteSpace(text[prefix_start]))
					{
						++prefix_start;
					}
					break;
				}
			}

			int suffix_max = max_length - (cursorPosition - prefix_start);
			int suffix_end = Math.Min(cursorPosition + suffix_max, text.Length-1);
			for(; cursorPosition<suffix_end; --suffix_end)
			{
				if (char.IsWhiteSpace(text[suffix_end]))
				{
					--suffix_end;
					while (cursorPosition< suffix_end && char.IsWhiteSpace(text[suffix_end]))
					{
						--suffix_end;
					}
					break;
				}
			}
			string prefix_text = text.Substring(prefix_start, cursorPosition - prefix_start);
			string suffix_text = text.Substring(cursorPosition, suffix_end);
			StringBuilder buffer_ = new StringBuilder(max_length);
			buffer_.Append(Prefix);
			buffer_.Append(prefix_text);
			buffer_.Append(Suffix);
			buffer_.Append(suffix_text);
			buffer_.Append(Middle);
			text = buffer_.ToString();
			//Log.Output(text);
			return text;
		}

		private string getSuffix(StringBuilder buffer)
		{
			string t = buffer.ToString();
			int suffix_start = -1;
			for (int i = buffer.Length-1; 0 <= i; --i)
			{
				if('<' == buffer[i] && i<=(buffer.Length- Middle.Length)){
					bool found = true;
					for(int j=1; j< Middle.Length; ++j)
					{
						if(Middle[j] != buffer[i + j])
						{
							found = false;
							break;
						}
					}
					if (!found)
					{
						continue;
					}
					suffix_start = i+ Middle.Length;
					break;
				}
			}
			if (0 <= suffix_start)
			{
				return buffer.ToString(suffix_start, buffer.Length-suffix_start);
			}
			else
			{
				return string.Empty;
			}
		}

		//public async Task AcceptCompletionAsync(string completionId)
		//      {
		//	AcceptCompletionRequest data = new() { metadata = GetMetadata(), completion_id = completionId };
		//	await RequestCommandAsync<AcceptCompletionResponse>("AcceptCompletion", data);
		//}

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
