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
        public string ModelName
        {
            get { return modelName_; }
            set { modelName_ = value; }
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
		[DisplayName("Prompt for Completion")]
		[Description("Prompt for Completion")]
		public string PromptCompletion 
		{
			get { return promptCompletion_; }
			set { promptCompletion_ = value; }
		}

        private TypeAIAPI typeAIAPI_ = TypeAIAPI.OpenAI;
        private TypeAIModel typeAIModel_ = TypeAIModel.GPT_3_5_Turbo;
        private string modelName_ = "llama2";
        private string apiKey_ = string.Empty;
        private string apiEndpoint_ = string.Empty;
        private bool formatResponse_ = false;
		private float temperature_ = 0.1f;
		private string promptCompletion_ = "Please complete the next {filetype} code. Write only the code, not the explanation.\ncode:{content}";
	}
}
