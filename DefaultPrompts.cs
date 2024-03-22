using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandyTools
{
	internal static class DefaultPrompts
	{
		public const string PromptCompletion = "Complete the next {filetype} code. Write only the code, not the explanation.\ncode:{content}";
		public const string PromptExplanation = "Explain the next {filetype} code.\ncode:{content}";
		public const string PromptTranslation = "Translate in English\n\n{content}";
		public const string PromptDocumentation = "Create a doxygen comment for the following C++ Function. doxygen comment only\n\n{content}";
	}
}
