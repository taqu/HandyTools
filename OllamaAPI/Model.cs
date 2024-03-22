using Newtonsoft.Json;
using System.Collections.Generic;

namespace OllamaAPI
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class Model
    {
        [JsonObject(MemberSerialization.OptIn)]
        public class Details
        {
            [JsonProperty]
            public string format { get; set; } = string.Empty;
            [JsonProperty]
            public string family { get; set; } = string.Empty;
            [JsonProperty(Required = Required.AllowNull)]
            public List<string> families { get; set; }
            [JsonProperty]
            public string parameter_size { get; set; } = string.Empty;
            [JsonProperty]
            public string quantization_level { get; set; } = string.Empty;
        };

        [JsonProperty]
        public string name { get; set; } = string.Empty;
        [JsonProperty]
        public string modified_at { get; set; } = string.Empty;
        [JsonProperty]
        public long size { get; set; }
        [JsonProperty]
        public string digest { get; set; } = string.Empty;
        [JsonProperty]
        public Details details { get; set; }
    }
}
