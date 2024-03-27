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

		public ModelOllama(SettingFile settingFile, TypeModel requestType)
        {
			string APIEndpoint = settingFile.ApiEndpoint;
			if (!string.IsNullOrEmpty(APIEndpoint))
            {
				APIEndpoint = APIEndpoint.Trim();
            }
			apiProvider_ = new OllamaProvider(APIEndpoint, settingFile.GetModelName(requestType));
			apiProvider_.Options = new OllamaAPI.Options();
		}

        public async Task<string> CompletionAsync(string userInput, float temperature, CancellationToken cancellationToken = default)
        {
			apiProvider_.Options.temperature = temperature;
			return await apiProvider_.GenerateAsync(userInput, cancellationToken);
		}
		private OllamaProvider apiProvider_;
	}
}

