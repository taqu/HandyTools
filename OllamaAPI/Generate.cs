using Newtonsoft.Json;
using System.Collections.Generic;

namespace OllamaAPI
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class GenerateRequest
    {
        [JsonProperty(Required = Required.Always)]
        public string model { get; set; } = string.Empty;
        [JsonProperty(Required = Required.Always)]
        public string prompt { get; set; } = string.Empty;
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public List<string> images { get; set; }

        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public string format { get; set; } // = "json";
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public Options options { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public string system { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public string template { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public string context { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public bool? stream { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public bool? raw { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public string keep_alive { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal class GenerateResponse
    {
        [JsonProperty(Required = Required.Always)]
        public string model { get; set; } = string.Empty;
        [JsonProperty(Required = Required.Default, NullValueHandling=NullValueHandling.Ignore)]
        public string created_at { get; set; }
        [JsonProperty(Required = Required.Always)]
        public string response { get; set; } = string.Empty;

        [JsonProperty(Required = Required.Default, NullValueHandling=NullValueHandling.Ignore)]
        public bool? done { get; set; }
        [JsonProperty(Required = Required.Default, NullValueHandling=NullValueHandling.Ignore)]
        public List<int> context { get; set; }
        [JsonProperty(Required = Required.Default, NullValueHandling=NullValueHandling.Ignore)]
        public long? total_duration { get; set; }
        [JsonProperty(Required = Required.Default, NullValueHandling=NullValueHandling.Ignore)]
        public long? load_duration { get; set; }
        [JsonProperty(Required = Required.Default, NullValueHandling=NullValueHandling.Ignore)]
        public int? prompt_eval_count { get; set; }
        [JsonProperty(Required = Required.Default, NullValueHandling=NullValueHandling.Ignore)]
        public long? prompt_eval_duration { get; set; }
        [JsonProperty(Required = Required.Default, NullValueHandling=NullValueHandling.Ignore)]
        public int? eval_count { get; set; }
        [JsonProperty(Required = Required.Default, NullValueHandling=NullValueHandling.Ignore)]
        public long? eval_duration { get; set; }
    }
}
