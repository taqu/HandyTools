using LangChain.Providers;
using LangChain.Providers.OpenAI;
using LangChain.Providers.OpenAI.Predefined;
using System.Threading;
using System.Threading.Tasks;
using static HandyTools.Types;

namespace HandyTools.Models
{
    public class ModelOpenAI : ModelBase
    {
        public TypeAIAPI APIType => TypeAIAPI.OpenAI;
        public ModelOpenAI(SettingFile settingFile)
        {
            string APIKey = settingFile.ApiKey;
            string APIEndpoint = settingFile.ApiEndpoint;
			if (string.IsNullOrEmpty(APIKey))
            {
                APIKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            }
            if (!string.IsNullOrEmpty(APIEndpoint))
            {
                APIEndpoint = APIEndpoint.Trim();
            }
            if (string.IsNullOrEmpty(APIEndpoint))
            {
				apiProvider_ = new OpenAiProvider(APIKey);
			}
            else
            {
                apiProvider_ = new OpenAiProvider(APIKey, APIEndpoint);
            }
            switch (settingFile.AIModel)
            {
            case TypeAIModel.GPT_3_5_Turbo:
                model_ = new Gpt35TurboModel(apiProvider_);
                break;
            case TypeAIModel.GPT_3_5_Turbo_16k:
                model_ = new Gpt35TurboModel(apiProvider_);
                break;
            case TypeAIModel.GPT_4:
                model_ = new Gpt4Model(apiProvider_);
                break;
                default:
                model_ = new Gpt35TurboModel(apiProvider_);
                break;
            }
            settings_ = new OpenAiChatSettings() { Temperature = settingFile.Temperature };

		}

        public async Task<string> CompletionAsync(string userInput, CancellationToken cancellationToken = default)
        {
            ChatResponse response = await model_.GenerateAsync(userInput, settings_, cancellationToken);
            return response.ToString();
        }

        private OpenAiChatSettings settings_;
        private OpenAiProvider apiProvider_;
        private OpenAiChatModel model_;
    }
}
