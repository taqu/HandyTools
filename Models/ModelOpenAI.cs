using OpenAI_API;
using OpenAI_API.Chat;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using static HandyTools.SettingFile;
using static HandyTools.Types;

namespace HandyTools.Models
{
	public class ModelOpenAI
    {
		public class CustomHttpClientFactory : IHttpClientFactory
		{
			public HttpClient CreateClient(string name)
			{
                HttpClient httpClient = new HttpClient();
				httpClient.Timeout = TimeSpan.FromSeconds(180);
				return httpClient;
			}
		}

		public string Model { get { return model_; } set { model_ = value; } }
        public string APIEndpoint
        {
            set
            {
                string endpoint = value;
                if (!string.IsNullOrEmpty(endpoint))
                {
					endpoint = endpoint.Trim();
                    endpoint = endpoint.TrimEnd('/');
					apiProvider_.ApiUrlFormat = endpoint + "/{0}/{1}";
				}
			}
        }

		public ModelOpenAI(AIModelSettings settings, TypeModel requestType)
        {
            string APIKey = settings.ApiKey;
			if (string.IsNullOrEmpty(APIKey))
            {
                APIKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            }
			apiProvider_ = new OpenAIAPI(APIKey);
            APIEndpoint = settings.GetAPIEndpoint(requestType);
			apiProvider_.HttpClientFactory = new CustomHttpClientFactory();
			model_ = settings.GetModelName(requestType);
		}

        public async Task<string> CompletionAsync(string userInput, float temperature, CancellationToken cancellationToken = default, int MaxTokens = 0)
        {
			ChatRequest chatRequest = new ChatRequest();
			chatRequest.Model = model_;
            chatRequest.Temperature = temperature;
            if (0 < MaxTokens)
            {
                chatRequest.MaxTokens = MaxTokens;
            }
			chatRequest.Messages = new System.Collections.Generic.List<ChatMessage>();
            chatRequest.Messages.Add(new ChatMessage(ChatMessageRole.User, userInput));
            ChatResult response = await apiProvider_.Chat.CreateChatCompletionAsync(chatRequest);
			return response.ToString();
        }

        private OpenAIAPI apiProvider_;
		private string model_;
    }
}
