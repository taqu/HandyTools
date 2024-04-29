using OpenAI_API;
using OpenAI_API.Chat;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using static HandyTools.SettingFile;
using static HandyTools.Types;

namespace HandyTools.Models
{
    public class ModelOpenAI : ModelBase
    {
        public TypeAIAPI APIType => TypeAIAPI.OpenAI;
        public string Model { get { return model_; } set { model_ = value; } }

        public ModelOpenAI(AIModelSettings settings, TypeModel requestType)
        {
            string APIKey = settings.ApiKey;
            string APIEndpoint = settings.ApiEndpoint;
			if (string.IsNullOrEmpty(APIKey))
            {
                APIKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            }
            if (!string.IsNullOrEmpty(APIEndpoint))
            {
                APIEndpoint = APIEndpoint.Trim();
                APIEndpoint = APIEndpoint.TrimEnd('/');
            }
            if (string.IsNullOrEmpty(APIEndpoint))
            {
                apiProvider_ = new OpenAIAPI(APIKey);
			}
            else
            {
                apiProvider_ = new OpenAIAPI(APIKey);
                apiProvider_.ApiUrlFormat = APIEndpoint + "/{0}/{1}";
            }
            model_ = settings.GetModelName(requestType);
		}

        public async Task<string> CompletionAsync(string userInput, float temperature, CancellationToken cancellationToken = default)
        {
            ChatRequest chatRequest = new ChatRequest();
            chatRequest.Model = model_;
            chatRequest.Temperature = temperature;
            chatRequest.Messages = new System.Collections.Generic.List<ChatMessage>();
            chatRequest.Messages.Add(new ChatMessage(ChatMessageRole.User, userInput));
            ChatResult response = await apiProvider_.Chat.CreateChatCompletionAsync(chatRequest);
            return response.ToString();
        }

        private OpenAIAPI apiProvider_;
        private string model_;
    }
}
