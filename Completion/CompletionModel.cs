using EnvDTE80;
using MessagePack.Formatters;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
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
        Guid id,
        string text,
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
		[DllImport("cplm.dll")]
		static extern unsafe IntPtr create_model(ulong size, IntPtr memory, int context);

		[DllImport("cplm.dll")]
		static extern void destroy_model(IntPtr model);

		[DllImport("cplm.dll")]
		static extern bool is_gpu(IntPtr model);
		[DllImport("cplm.dll", CharSet = CharSet.Unicode)]
		static extern int get_fim_prefix(IntPtr model, int size, StringBuilder str);
		[DllImport("cplm.dll", CharSet = CharSet.Unicode)]
		static extern int get_fim_middle(IntPtr model, int size, StringBuilder str);
		[DllImport("cplm.dll", CharSet = CharSet.Unicode)]
		static extern int get_fim_suffix(IntPtr model, int size, StringBuilder str);
		[DllImport("cplm.dll", CharSet = CharSet.Unicode)]
		static extern int get_fim_pad(IntPtr model, int size, StringBuilder str);

		[DllImport("cplm.dll", CharSet = CharSet.Unicode)]
		static extern void generate_one(
			IntPtr model,
			uint buffer_size,
			StringBuilder generated,
			uint size,
			string text,
			int context,
			ulong seed=0,
			float temperature=1.0f,
			float minp=0.1f,
			int steps=4096,
			int stop_token=-1);

#if false
		public const int MaxQuery = 3072;
		public const int MaxContext = 4096;
		public const int MaxResponse = MaxContext*3;
#elif false
		public const int MaxQuery = 1024;
		public const int MaxContext = 2048;
		public const int MaxResponse = MaxContext*3;
#else
		public const int MaxQuery = 256;
		public const int MaxContext = 512;
		public const int MaxResponse = MaxContext*3;
#endif
		public const int MaxWords = 64;
		public const string ModelName = "qwen2.5-coder.calm";

		private bool disposed_ = false;
		private IntPtr model_ = IntPtr.Zero;
		private StringBuilder buffer_ = new StringBuilder(4096);
		private StringBuilder generated_ = new StringBuilder(MaxResponse);

		private int PrefixToken = -1;
		private string Prefix = "<|fim_prefix|>";
		private int SuffixToken = -1;
		private string Suffix = "<|fim_suffix|>";
		private int MiddleToken = -1;
		private string Middle = "<|fim_middle|>";
		private int PadToken = -1;
		private string Pad = "<|fim_middle|>";

		private void GetPrefix()
		{
			generated_.Length = 0;
			PrefixToken = get_fim_prefix(model_, MaxResponse, generated_);
			if(0<= PrefixToken)
			{
				Prefix = generated_.ToString();
			}
		}

		private void GetMiddle()
		{
			generated_.Length = 0;
			MiddleToken = get_fim_middle(model_, MaxResponse, generated_);
			if (0 <= MiddleToken)
			{
				Middle = generated_.ToString();
			}
		}

		private void GetSuffix()
		{
			generated_.Length = 0;
			SuffixToken = get_fim_suffix(model_, MaxResponse, generated_);
			if (0 <= SuffixToken)
			{
				Suffix = generated_.ToString();
			}
		}

		private void GetPad()
		{
			generated_.Length = 0;
			PadToken = get_fim_pad(model_, MaxResponse, generated_);
			if (0 <= PadToken)
			{
				Pad = generated_.ToString();
			}
		}

		public static CompletionModel Initialize()
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
					int size = stream.Read(buffer, 0, buffer.Length);
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
					model.GetSuffix();
					model.GetMiddle();
					model.GetSuffix();
					model.GetPad();
					return model;
				}
			}
			catch
			{
				return null;
			}
		}

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
					model.GetPrefix();
					model.GetMiddle();
					model.GetSuffix();
					model.GetPad();
					return model;
				}
			}
			catch
			{
				return null;
			}
		}

		public async Task<IList<Completion>?> GetCompletionsAsync(
			string absolutePath,
			ITextSnapshot text,
			LanguageInfo language,
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

			generated_.Length = 0;
			generate_one(model_, MaxResponse, generated_, (uint)query.Length, query, MaxContext, 0, 1.0f, 0.1f, MaxContext, PadToken);
			List<Completion> completions = new List<Completion>();
			getSuggestion(completions, generated_, cursorPosition, MaxWords);
#if false
			if (!string.IsNullOrEmpty(suggestion))
			{
				Completion completion = new Completion();
				completion.id = Guid.NewGuid();
				completion.text = suggestion;
				completion.startOffset = cursorPosition;
				completion.endOffset = cursorPosition + suggestion.Length;
				completions.Add(completion);
			}
#endif
			return completions;
        }
		
		private string createQuery(ITextSnapshot text, int cursorPosition, float prefix_rate, int max_length)
		{
			char c = text[cursorPosition];
			prefix_rate = Math.Min(1.0f, Math.Max(0.0f, prefix_rate));
			int prefix_max = (int)(max_length * prefix_rate);

			int prefix_start = Math.Max(0, cursorPosition - prefix_max);
			while (char.IsWhiteSpace(text[prefix_start])&& prefix_start<text.Length)
			{
				++prefix_start;
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
			string prefix_text = text.GetText(prefix_start, cursorPosition - prefix_start);
			string suffix_text = text.GetText(cursorPosition, suffix_end);
			buffer_.Length = 0;
			buffer_.Append(Prefix);
			buffer_.Append(prefix_text);
			buffer_.Append(Suffix);
			buffer_.Append(suffix_text);
			buffer_.Append(Middle);
			return buffer_.ToString();
		}

		private void getSuggestion(List<Completion> completions, StringBuilder buffer, int position, int max_words)
		{
			string str = buffer.ToString();
			splitWords(completions, str, position, max_words);
			Log.Output(completions.ToString());
		}

		private void splitWords(List<Completion> completions, string str, int position, int max_words)
		{
			string[] lines = str.Split('\n', '\r');
			if (lines.Length <= 0)
			{
				return;
			}
			string[] words = lines[0].Split(' ', '\t', '\b');
			for (int i = 0; i < words.Length && i<=max_words; ++i)
			{
				if(string.IsNullOrEmpty(words[i]))
				{
					continue;
				}
				Completion completion = new Completion();
				completion.id = Guid.NewGuid();
				completion.text = words[i];
				completion.startOffset = position;
				completion.endOffset = position + words[i].Length;
				position += words[i].Length;
				completions.Add(completion);
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
