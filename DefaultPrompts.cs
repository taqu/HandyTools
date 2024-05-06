namespace HandyTools
{
	internal static class DefaultPrompts
	{
		//public const string PromptCompletion = "<｜fim▁begin｜>{prefix}<｜fim▁hole｜>{suffix}<｜fim▁end｜>"; //for deepseek corder
		public const string PromptCompletion = "<|fim_prefix|>{prefix}<|fim_suffix|>{suffix}<|fim_middle|>"; //for codegemma
		//public const string PromptCompletion = "<fim_prefix>{prefix}<fim_suffix>{suffix}<fim_middle>"; //for starcorder2
		public const string PromptExplanation = "Explain this {filetype} code.\n\n{content}";
		public const string PromptTranslation = "Translate in English\n\n{content}";
		public const string PromptDocumentation = "Create a doxygen comment for the following C++ Function. doxygen comment only\n\n{content}";
	}
}
