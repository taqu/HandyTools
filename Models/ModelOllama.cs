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

        public ModelOllama(SettingFile settingFile)
        {
            string APIEndpoint = settingFile.ApiEndpoint;
			if (!string.IsNullOrEmpty(APIEndpoint))
            {
				APIEndpoint = APIEndpoint.Trim();
            }
            OllamaOptions options = new OllamaOptions();
			options.Temperature = settingFile.Temperature;
			apiProvider_ = new OllamaProvider(APIEndpoint, options);
            model_ = new OllamaChatModel(apiProvider_, settingFile.ModelName);
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

