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
        [DefaultValue(TypeAIAPI.OpenAI)]
        public TypeAIAPI APIType
        {
            get { return typeAIAPI_; }
            set { typeAIAPI_ = value; }
        }

        [Category("AI")]
        [DisplayName("AI Model")]
        [Description("AI Model for OpenAI API")]
        [DefaultValue(TypeAIModel.GPT_3_5_Turbo)]
        public TypeAIModel AIModel
        {
            get { return typeAIModel_; }
            set { typeAIModel_ = value; }
        }

        [Category("AI")]
        [DisplayName("AI Model Name")]
        [Description("AI Model Name for Ollama")]
        [DefaultValue("llama2")]
        public string ModelName
        {
            get { return modelName_; }
            set { modelName_ = value; }
        }

        [Category("AI")]
        [DisplayName("AI API Key")]
        [Description("AI API Key for OpenAI API")]
        [DefaultValue("")]
        public string ApiKey
        {
            get { return apiKey_; }
            set { apiKey_ = value; }
        }

        [Category("AI")]
        [DisplayName("AI API Endpoint")]
        [Description("AI API Endpoint")]
        [DefaultValue("")]
        public string ApiEndpoint
        {
            get { return apiEndpoint_; }
            set { apiEndpoint_ = value; }
        }

		[Category("AI")]
		[DisplayName("Format After Generation")]
		[Description("Format After Generation")]
		[DefaultValue(false)]
		public bool FormatResponse
		{
			get { return formatResponse_; }
			set { formatResponse_ = value; }
		}

		[Category("AI")]
		[DisplayName("Prompt for Completion")]
		[Description("Prompt for Completion")]
		[DefaultValue("Please complete the next {filetype} code. Write only the code, not the explanation.\ncode:{content}")]
		public string PromptCompletion 
		{
			get { return promptCompletion_; }
			set { promptCompletion_ = value; }
		}

		[Category("AI")]
		[DisplayName("Temperature")]
		[Description("Temperature")]
		[DefaultValue(0.1f)]
		public float Temperature
		{
			get { return temperature_; }
			set { temperature_ = value; }
		}

		private TypeAIAPI typeAIAPI_;
        private TypeAIModel typeAIModel_;
        private string modelName_;
        private string apiKey_;
        private string apiEndpoint_;
        private bool formatResponse_;
        private string promptCompletion_;
        private float temperature_;

	}
}
