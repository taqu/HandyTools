using LangChain.Providers;
using OllamaAPI;
using System.Threading;
using System.Threading.Tasks;
using static HandyTools.Types;

namespace HandyTools.Models
{
	internal class ModelOllama : ModelBase
    {
        public TypeAIAPI APIType => TypeAIAPI.Ollama;
		public string Model { get { return apiProvider_.Model; } set { apiProvider_.Model = value; } }

		public ModelOllama(SettingFile settingFile, TypeOllamaModel requestType)
        {
			string APIEndpoint = settingFile.ApiEndpoint;
			if (!string.IsNullOrEmpty(APIEndpoint))
            {
				APIEndpoint = APIEndpoint.Trim();
            }
			apiProvider_ = new OllamaProvider(APIEndpoint, settingFile.GetModelName(requestType));
			apiProvider_.Options = new OllamaAPI.Options();
			apiProvider_.Options.temperature = settingFile.Temperature;
		}

        public async Task<string> CompletionAsync(string userInput, CancellationToken cancellationToken = default)
        {
			return await apiProvider_.GenerateAsync(userInput, cancellationToken);
		}
		private OllamaProvider apiProvider_;
	}
}

