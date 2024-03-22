using Newtonsoft.Json;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OllamaAPI
{
    internal class OllamaProvider
    {
        private const string APITags = "api/tags";
        private const string APIGenerate = "api/generate";

        public Options Options { get { return options_; } set { options_ = value; } }
        public string Model { get { return model_; } set { model_ = value; } }

        public OllamaProvider(string uri)
        {
            System.Diagnostics.Debug.Assert(null != uri);
            baseAddress_ = string.IsNullOrEmpty(uri) ? string.Empty : uri.Trim();
            httpClient_ = new HttpClient();
            if (!baseAddress_.EndsWith("/"))
            {
                baseAddress_ += '/';
            }
        }

        public OllamaProvider(string uri, string model)
            : this(uri)
        {
            System.Diagnostics.Debug.Assert(null != uri);
            model_ = model;
        }

        public async Task<string> TagsAsync()
        {
            stringBuilder_.Length = 0;
            stringBuilder_.Append(baseAddress_);
            stringBuilder_.Append(APITags);
            string uri = stringBuilder_.ToString();
            HttpResponseMessage response = await httpClient_.GetAsync(uri);
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        {
            System.Diagnostics.Debug.Assert(null != prompt);
            (StringContent content, string uri) = Create(prompt, false);
            using (content)
            {
                HttpResponseMessage responseMessage = await httpClient_.PostAsync(uri, content, cancellationToken);
                string responseJson = await responseMessage.Content.ReadAsStringAsync();
                responseMessage.EnsureSuccessStatusCode();
                GenerateResponse response = JsonConvert.DeserializeObject<GenerateResponse>(responseJson);
                return response.response;
            }
        }

        public async Task GenerateStreamAsync(string prompt, System.Action<string> responseCallback, CancellationToken cancellationToken = default)
        {
            System.Diagnostics.Debug.Assert(null != prompt);
            (StringContent content, string uri) = Create(prompt, true);
            using (content)
            {
                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, uri);
                requestMessage.Content = content;
                HttpResponseMessage responseMessage = await httpClient_.SendAsync(requestMessage, cancellationToken);
                responseMessage.EnsureSuccessStatusCode();
                using (System.IO.Stream responseStream = await responseMessage.Content.ReadAsStreamAsync())
                using (System.IO.StreamReader responseReader = new System.IO.StreamReader(responseStream))
                {
                    while (!responseReader.EndOfStream && !cancellationToken.IsCancellationRequested)
                    {
                        string responseJson = await responseReader.ReadLineAsync();
                        GenerateResponse response = JsonConvert.DeserializeObject<GenerateResponse>(responseJson);
                        responseCallback(response.response);
                    }
                }
            }
        }

        private (StringContent, string) Create(string prompt, bool stream)
        {
            System.Diagnostics.Debug.Assert(null != prompt);
            stringBuilder_.Length = 0;
            stringBuilder_.Append(baseAddress_);
            stringBuilder_.Append(APIGenerate);
            string uri = stringBuilder_.ToString();
            GenerateRequest request = new GenerateRequest();
            request.model = model_;
            request.prompt = prompt;
            request.options = options_;
            request.stream = stream;
            string requestJson = JsonConvert.SerializeObject(request);
            return (new StringContent(requestJson, Encoding.UTF8, "application/json"), uri);
        }

        private StringBuilder stringBuilder_ = new StringBuilder();
        private string baseAddress_;
        private HttpClient httpClient_;
        private string model_ = "llama2";
        private Options options_;
    }
}

