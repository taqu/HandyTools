using Newtonsoft.Json;
using System.Collections.Generic;

namespace OllamaAPI
{
    internal class Options
    {
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public int? num_keep { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public int? seed { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public int? num_predict { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public int? top_k { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public float? top_p { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public float? tfs_z { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public float? typical_p { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public int? repeat_last_n { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public float? temperature { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public float? repeat_penalty { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public float? presence_penalty { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public float? frequency_penalty { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public int? mirostat { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public float? mirostat_tau { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public float? mirostat_eta { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public bool? penalize_newline { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public List<string> stop { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public bool? numa { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public int? num_ctx { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public int? num_batch { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public int? num_gqa { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public int? num_gpu { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public int? main_gpu { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public bool? low_vram { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public bool? f16_kv { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public bool? vocab_only { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public bool? use_mmap { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public bool? use_mlock { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public float? rope_frequency_base { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public float? rope_frequency_scale { get; set; }
        [JsonProperty(Required = Required.AllowNull, NullValueHandling=NullValueHandling.Ignore)]
        public int? num_thread { get; set; }
    }
}
