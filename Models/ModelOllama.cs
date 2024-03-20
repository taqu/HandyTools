using LangChain.Providers;
using LangChain.Providers.Ollama;
using System.Threading;
using System.Threading.Tasks;
using static HandyTools.Types;

namespace HandyTools.Models
{
	internal class ModelOllama : ModelBase
    {
        public TypeAIAPI APIType => TypeAIAPI.Ollama;
        public TypeOllamaModel ModelType { get; set; }

		public ModelOllama(SettingFile settingFile, TypeOllamaModel requestType)
        {
			ModelType = requestType;
			string APIEndpoint = settingFile.ApiEndpoint;
			if (!string.IsNullOrEmpty(APIEndpoint))
            {
				APIEndpoint = APIEndpoint.Trim();
            }
            OllamaOptions options = new OllamaOptions();
			options.Temperature = settingFile.Temperature;
            //try
            //{
            //    HttpClient httpClient = new HttpClient() { BaseAddress = new Uri(APIEndpoint), Timeout = TimeSpan.FromSeconds(settingFile.Timeout)};
            //    apiProvider_ = new OllamaProvider(httpClient, options);
            //}
            string modelName = string.Empty;
            switch (requestType)
            {
                case TypeOllamaModel.General:
                    modelName = settingFile.ModelGeneral;
					break;
				case TypeOllamaModel.Generation:
					modelName = settingFile.ModelGeneration;
					break;
				case TypeOllamaModel.Translation:
					modelName = settingFile.ModelTranslation;
					break;
			}

			//apiProvider_ = new OllamaProvider(APIEndpoint, options);
			apiProvider_ = new OllamaProvider(APIEndpoint);
			model_ = new OllamaChatModel(apiProvider_, modelName);
		}

        public async Task<string> CompletionAsync(string userInput, CancellationToken cancellationToken = default)
        {
			ChatResponse response = await model_.GenerateAsync(userInput, null, cancellationToken);
			return response.ToString();
		}

		private OllamaProvider apiProvider_;
		private OllamaChatModel model_;
	}
}

