using OllamaAPI;
using System.Threading;
using System.Threading.Tasks;
using static HandyTools.SettingFile;
using static HandyTools.Types;

namespace HandyTools.Models
{
	internal class ModelOllama : ModelBase
    {
        public TypeAIAPI APIType => TypeAIAPI.Ollama;
		public string Model { get { return apiProvider_.Model; } set { apiProvider_.Model = value; } }

		public ModelOllama(AIModelSettings settings, TypeModel requestType)
        {
			string APIEndpoint = settings.ApiEndpoint;
			if (!string.IsNullOrEmpty(APIEndpoint))
            {
				APIEndpoint = APIEndpoint.Trim();
            }
			apiProvider_ = new OllamaProvider(APIEndpoint, settings.GetModelName(requestType));
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

