using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static HandyTools.Types;

namespace HandyTools.Options
{
    [ComVisible(true)]
	public class OptionPageHandyToolsAI : Microsoft.VisualStudio.Shell.DialogPage
    {
		[Category("API")]
        [DisplayName("API Key")]
        [Description("API Key for OpenAI API")]
        public string ApiKey
        {
            get { return apiKey_; }
            set { apiKey_ = value; }
        }

        [Category("API")]
        [DisplayName("API Endpoint")]
        [Description("API Endpoint")]
        public string ApiEndpoint
        {
            get { return apiEndpoint_; }
            set { apiEndpoint_ = value; }
        }

		[Category("API")]
		[DisplayName("Completion Endpoint")]
		[Description("API Endpoint Completion Tasks")]
		public string CompletionEndpoint
		{
			get { return completionEndpoint_; }
			set { completionEndpoint_ = value; }
		}

		[Category("Model")]
        [DisplayName("Model Name")]
        [Description("Model Name for General Purpose")]
        public string ModelGeneral
        {
            get { return modelGeneral_; }
            set { modelGeneral_ = value; }
        }

		[Category("Model")]
		[DisplayName("Generation Model Name")]
		[Description("Model Name for Generation, using when generating codes.")]
		public string ModelGeneration
		{
			get { return modelGeneration_; }
			set { modelGeneration_ = value; }
		}

		[Category("Model")]
		[DisplayName("Translation Model Name")]
		[Description("Model Name for Translation, using when translating.")]
		public string ModelTranslation
		{
			get { return modelTranslation_; }
			set { modelTranslation_ = value; }
		}

		[Category("Model")]
		[DisplayName("Format After Generation")]
		[Description("Format After Generation")]
		public bool FormatResponse
		{
			get { return formatResponse_; }
			set { formatResponse_ = value; }
		}

		[Category("Model")]
		[DisplayName("Temperature")]
		[Description("Temperature")]
		public float Temperature
		{
			get { return temperature_; }
			set { temperature_ = value; }
		}

		[Category("Model")]
		[DisplayName("Max Text Length")]
		[Description("Max Text Length which is Sent to AI. Not Max Tokens.")]
		public int MaxTextLength
		{
			get { return maxTextLength_; }
			set { maxTextLength_ = value; }
		}

		[Category("Model")]
		[DisplayName("Timeout")]
		[Description("Timeout for interacting with AI in seconds.")]
		public int Timeout
		{
			get { return timeout_; }
			set { timeout_ = value; }
		}

		[Category("Model")]
		[DisplayName("RealTimeCompletion")]
		[Description("Real time completion")]
		public bool RealTimeCompletion 
		{
			get { return realTimeCompletion_; }
			set { realTimeCompletion_ = value; }
		}

		[Category("Model")]
		[DisplayName("Max Interval Time for Completion")]
		[Description("Max Waiting Time for Completion")]
		public int CompletionIntervalInMilliseconds
		{
			get { return completionIntervalInMilliseconds_; }
			set { completionIntervalInMilliseconds_ = value; }
		}

		[Category("Model")]
		[DisplayName("Max Input for Completion")]
		[Description("Max Input Context Size for Completion")]
		public int MaxCompletionInputSize
		{
			get { return maxCompletionInputSize_; }
			set { maxCompletionInputSize_ = value; }
		}

		[Category("Model")]
		[DisplayName("Max Output for Completion")]
		[Description("Max Output Context Size for Completion")]
		public int MaxCompletionOutputSize
		{
			get { return maxCompletionOutputSize_; }
			set { maxCompletionOutputSize_ = value; }
		}

		[Category("Prompt")]
		[DisplayName("Completion")]
		[Description("Prompt for Completion")]
		public string PromptCompletion
		{
			get { return promptCompletion_; }
			set { promptCompletion_ = value; }
		}

		[Category("Prompt")]
		[DisplayName("Explanation")]
		[Description("Prompt for Explanation")]
		public string PromptExplanation
		{
			get { return promptExplanation_; }
			set { promptExplanation_ = value; }
		}

		[Category("Prompt")]
		[DisplayName("Translation")]
		[Description("Prompt for Translation")]
		public string PromptTranslation
		{
			get { return promptTranslation_; }
			set { promptTranslation_ = value; }
		}

		[Category("Prompt")]
		[DisplayName("Documentation")]
		[Description("Prompt for Documentation")]
		public string PromptDocumentation
		{
			get { return promptDocumentation_; }
			set { promptDocumentation_ = value; }
		}

        private string modelGeneral_ = "llama2";
		private string modelGeneration_ = "llama2";
		private string modelTranslation_ = "llama2";
		private string apiKey_ = "XXX";
        private string apiEndpoint_ = string.Empty;
		private string completionEndpoint_ = string.Empty;
		private bool formatResponse_ = false;
		private float temperature_ = 0.1f;
        private int maxTextLength_ = 4096;
		private int timeout_ = 30;
		private bool realTimeCompletion_ = false;
		private int completionIntervalInMilliseconds_ = 1000;
		private int maxCompletionInputSize_ = 4096;
		private int maxCompletionOutputSize_ = 64;
		private string promptCompletion_ = DefaultPrompts.PromptCompletion;
		private string promptExplanation_ = DefaultPrompts.PromptExplanation;
		private string promptTranslation_ = DefaultPrompts.PromptTranslation;
		private string promptDocumentation_ = DefaultPrompts.PromptDocumentation;
	}
}
