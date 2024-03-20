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
        [Category("AI")]
        [DisplayName("AI API Type")]
        [Description("AI API Type")]
        public TypeAIAPI APIType
        {
            get { return typeAIAPI_; }
            set { typeAIAPI_ = value; }
        }

        [Category("AI")]
        [DisplayName("AI Model")]
        [Description("AI Model for OpenAI API")]
        public TypeAIModel AIModel
        {
            get { return typeAIModel_; }
            set { typeAIModel_ = value; }
        }

        [Category("AI")]
        [DisplayName("AI Model Name")]
        [Description("AI Model Name for Ollama")]
        public string ModelGeneral
        {
            get { return modelGeneral_; }
            set { modelGeneral_ = value; }
        }

		[Category("AI")]
		[DisplayName("AI Generation Model Name")]
		[Description("AI Model Name for Ollama, using when generating codes.")]
		public string ModelGeneration
		{
			get { return modelGeneration_; }
			set { modelGeneration_ = value; }
		}

		[Category("AI")]
		[DisplayName("AI Translation Model Name")]
		[Description("AI Model Name for Ollama, using when translating.")]
		public string ModelTranslation
		{
			get { return modelTranslation_; }
			set { modelTranslation_ = value; }
		}

		[Category("AI")]
        [DisplayName("AI API Key")]
        [Description("AI API Key for OpenAI API")]
        public string ApiKey
        {
            get { return apiKey_; }
            set { apiKey_ = value; }
        }

        [Category("AI")]
        [DisplayName("AI API Endpoint")]
        [Description("AI API Endpoint")]
        public string ApiEndpoint
        {
            get { return apiEndpoint_; }
            set { apiEndpoint_ = value; }
        }

		[Category("AI")]
		[DisplayName("Format After Generation")]
		[Description("Format After Generation")]
		public bool FormatResponse
		{
			get { return formatResponse_; }
			set { formatResponse_ = value; }
		}

		[Category("AI")]
		[DisplayName("Temperature")]
		[Description("Temperature")]
		public float Temperature
		{
			get { return temperature_; }
			set { temperature_ = value; }
		}

		[Category("AI")]
		[DisplayName("Max Text Length")]
		[Description("Max Text Length which is Sent to AI. Not Max Tokens.")]
		public int MaxTextLength
		{
			get { return maxTextLength_; }
			set { maxTextLength_ = value; }
		}

		[Category("AI")]
		[DisplayName("Timeout")]
		[Description("Timeout for interacting with AI in seconds.")]
		public int Timeout
		{
			get { return timeout_; }
			set { timeout_ = value; }
		}

		[Category("AI")]
		[DisplayName("Prompt for Completion")]
		[Description("Prompt for Completion")]
		public string PromptCompletion 
		{
			get { return promptCompletion_; }
			set { promptCompletion_ = value; }
		}

		[Category("AI")]
		[DisplayName("Prompt for Explanation")]
		[Description("Prompt for Explanation")]
		public string PromptExplanation
		{
			get { return promptExplanation_; }
			set { promptExplanation_ = value; }
		}

		[Category("AI")]
		[DisplayName("Prompt for Translation")]
		[Description("Prompt for Translation")]
		public string PromptTranslation
		{
			get { return promptTranslation_; }
			set { promptTranslation_ = value; }
		}

		[Category("AI")]
		[DisplayName("Prompt for Documentation")]
		[Description("Prompt for Documentation")]
		public string PromptDocumentation
		{
			get { return promptDocumentation_; }
			set { promptDocumentation_ = value; }
		}

		private TypeAIAPI typeAIAPI_ = TypeAIAPI.OpenAI;
        private TypeAIModel typeAIModel_ = TypeAIModel.GPT_3_5_Turbo;
        private string modelGeneral_ = "llama2";
		private string modelGeneration_ = "llama2";
		private string modelTranslation_ = "llama2";
		private string apiKey_ = string.Empty;
        private string apiEndpoint_ = string.Empty;
        private bool formatResponse_ = false;
		private float temperature_ = 0.1f;
        private int maxTextLength_ = 4096;
		private int timeout_ = 30;
		private string promptCompletion_ = DefaultPrompts.PromptCompletion;
        private string promptExplanation_ = DefaultPrompts.PromptExplanation;
		private string promptTranslation_ = DefaultPrompts.PromptTranslation;
		private string promptDocumentation_ = DefaultPrompts.PromptDocumentation;
	}
}
