using HandyTools.Commands;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HandyTools.Completion
{
	public record struct Completion(
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
		[DllImport("llama.cpp.dll", CharSet = CharSet.Unicode)]
		static extern unsafe IntPtr create_model(string path, int n_gpu_layers=99);
		[DllImport("llama.cpp.dll")]
		static extern void destroy_model(IntPtr model);

		[DllImport("llama.cpp.dll", CharSet = CharSet.Unicode)]
		static extern int get_fim_prefix(IntPtr model, int size, StringBuilder str);
		[DllImport("llama.cpp.dll", CharSet = CharSet.Unicode)]
		static extern int get_fim_middle(IntPtr model, int size, StringBuilder str);
		[DllImport("llama.cpp.dll", CharSet = CharSet.Unicode)]
		static extern int get_fim_suffix(IntPtr model, int size, StringBuilder str);
		[DllImport("llama.cpp.dll", CharSet = CharSet.Unicode)]
		static extern int get_fim_pad(IntPtr model, int size, StringBuilder str);

		[DllImport("llama.cpp.dll", CharSet = CharSet.Unicode)]
		static extern IntPtr begin(IntPtr model, int size, string prompt, int n_predict, float temperature=0.0f, uint seed=0xFFFFFFFF);
		[DllImport("llama.cpp.dll", CharSet = CharSet.Unicode)]
		static extern void end(IntPtr model, IntPtr context);
		[DllImport("llama.cpp.dll", CharSet = CharSet.Unicode)]
		static extern int generate(IntPtr model, IntPtr context, int size, StringBuilder output);

#if false
		public const int MaxQuery = 4096;
		public const int MaxContext = 3072;
		public const int MaxTokenSize = 16;
		public const int MaxResponse = MaxContext*MaxTokenSize;
#elif false
		public const int MaxQuery = 2048;
		public const int MaxContext = 1024;
		public const int MaxTokenSize = 16;
		public const int MaxResponse = MaxContext*MaxTokenSize;
#else
		public const int MaxQuery = 512;
		public const int MaxContext = 256;
		public const int MaxTokenSize = 16;
		public const int MaxResponse = MaxContext * MaxTokenSize;
#endif
		public const int MaxLines = 4;
		public const string ModelName = "refact-1_6b-Q4_K_M.gguf";

		private bool disposed_ = false;
		private IntPtr model_ = IntPtr.Zero;
		private StringBuilder buffer_ = new StringBuilder(MaxResponse);

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
			buffer_.Length = 0;
			PrefixToken = get_fim_prefix(model_, MaxTokenSize, buffer_);
			if (0 <= PrefixToken)
			{
				Prefix = buffer_.ToString();
			}
		}

		private void GetMiddle()
		{
			buffer_.Length = 0;
			MiddleToken = get_fim_middle(model_, MaxTokenSize, buffer_);
			if (0 <= MiddleToken)
			{
				Middle = buffer_.ToString();
			}
		}

		private void GetSuffix()
		{
			buffer_.Length = 0;
			SuffixToken = get_fim_suffix(model_, MaxTokenSize, buffer_);
			if (0 <= SuffixToken)
			{
				Suffix = buffer_.ToString();
			}
		}

		private void GetPad()
		{
			buffer_.Length = 0;
			PadToken = get_fim_pad(model_, MaxTokenSize, buffer_);
			if (0 <= PadToken)
			{
				Pad = buffer_.ToString();
			}
		}

		public static CompletionModel Initialize()
		{
			string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			path = System.IO.Path.Combine(path, "Resources", ModelName);
			if (!System.IO.File.Exists(path))
			{
				return null;
			}
			try
			{
				IntPtr ptr = create_model(path);
				if (ptr == IntPtr.Zero)
				{
					return null;
				}
				CompletionModel model = new CompletionModel();
				model.model_ = ptr;
				model.GetPrefix();
				model.GetMiddle();
				model.GetSuffix();
				model.GetPad();
				return model;
			}
			catch
			{
				return null;
			}
		}

		public static async Task<CompletionModel> InitializeAsync()
		{
			string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			path = System.IO.Path.Combine(path, "Resources", ModelName);
			if (!System.IO.File.Exists(path))
			{
				return null;
			}
			try
			{
				IntPtr ptr = await Task.Run<IntPtr>(()=> create_model(path));
				if (ptr == IntPtr.Zero)
				{
					return null;
				}
				CompletionModel model = new CompletionModel();
				model.model_ = ptr;
				model.GetPrefix();
				model.GetMiddle();
				model.GetSuffix();
				model.GetPad();
				return model;
			}
			catch
			{
				return null;
			}
		}

		public async Task<IList<Completion>> GetCompletionsAsync(
			string absolutePath,
			ITextSnapshot text,
			LanguageInfo language,
			int cursorPosition,
			CancellationToken cancellationToken)
		{
			if (IntPtr.Zero == model_)
			{
				return null;
			}
			if (language.language == Language.None)
			{
				return null;
			}
            string query = createQuery(text, cursorPosition, 0.5f, MaxQuery);
            IntPtr context = begin(model_, query.Length, query, MaxContext);
            if (IntPtr.Zero == context)
            {
                return null;
            }
            buffer_.Length = 0;
			try
			{
				IList<Completion>? completions = await Task.Run<IList<Completion>?>(
					() =>
					{
						return GetCompletionsImpl(context, cursorPosition);

					},
					cancellationToken
				);
				end(model_, context);
				return completions;
            }
            catch(OperationCanceledException e)
			{
                end(model_, context);
				return null;
            }
        }

        private IList<Completion>? GetCompletionsImpl(
            IntPtr context,
            int cursorPosition)
		{
			generate(model_, context, MaxResponse-1, buffer_);

			List<Completion> completions = new List<Completion>();
			getSuggestion(completions, buffer_, cursorPosition, MaxLines);
			return completions;
		}

		private string createQuery(ITextSnapshot text, int cursorPosition, float prefix_rate, int max_length)
		{
			char c = text[cursorPosition];
			prefix_rate = Math.Min(1.0f, Math.Max(0.0f, prefix_rate));
			int prefix_max = (int)(max_length * prefix_rate);

			int prefix_start = Math.Max(0, cursorPosition - prefix_max);
			while (char.IsWhiteSpace(text[prefix_start]) && prefix_start < text.Length)
			{
				++prefix_start;
			}
			int suffix_max = max_length - (cursorPosition - prefix_start);
			int suffix_end = Math.Min(cursorPosition + suffix_max, text.Length - 1);
			for (; cursorPosition < suffix_end; --suffix_end)
			{
				if (char.IsWhiteSpace(text[suffix_end]))
				{
					--suffix_end;
					while (cursorPosition < suffix_end && char.IsWhiteSpace(text[suffix_end]))
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

		private int SkipLineFeed(StringBuilder buffer, int position)
		{
			for (; position < buffer.Length; ++position)
			{
				if (!CodeUtil.IsLineFeed(buffer[position]))
				{
					break;
				}
			}
			return position;
		}

		private int SkipSpace(StringBuilder buffer, int position)
		{
			for(; position < buffer.Length; ++position)
			{
				if (!CodeUtil.IsWhiteSpace(buffer[position]))
				{
					break;
				}
			}
			return position;
		}

		private void getSuggestion(List<Completion> completions, StringBuilder buffer, int position, int max_lines)
		{
			int start = 0;
			int i = 0;
			for(; i<buffer.Length;)
			{
				if (CodeUtil.IsLineFeed(buffer[i]))
				{
					string line = buffer.ToString(start, i - start);
					i = start = SkipLineFeed(buffer, i);
					if (string.IsNullOrEmpty(line))
					{
						continue;
					}
					Completion completion = new Completion();
					completion.text = line;
					completion.startOffset = position;
					completion.endOffset = position;
					completions.Add(completion);
					if(max_lines<= completions.Count)
					{
						break;
					}
				}
				else
				{
					++i;
				}
			}
			if(completions.Count<max_lines){
				string line = buffer.ToString(start, i - start);
				if (!string.IsNullOrEmpty(line))
				{
					Completion completion = new Completion();
					completion.text = line;
					completion.startOffset = position;
					completion.endOffset = position;
					completions.Add(completion);
				}
			}
		}

		private void splitWords(List<Completion> completions, string str, int position, int max_words)
		{
			string[] lines = str.Split('\n', '\r');
			if (lines.Length <= 0)
			{
				return;
			}
			string[] words = lines[0].Split(' ', '\t', '\b');
			for (int i = 0; i < words.Length && i <= max_words; ++i)
			{
				if (string.IsNullOrEmpty(words[i]))
				{
					continue;
				}
				Completion completion = new Completion();
				completion.text = words[i];
				completion.startOffset = position;
				completion.endOffset = position + words[i].Length;
				position += words[i].Length;
				completions.Add(completion);
			}
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
				if (model_ != IntPtr.Zero)
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
