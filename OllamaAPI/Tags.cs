using Newtonsoft.Json;
using System.Collections.Generic;

namespace OllamaAPI
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class Tags
    {
        [JsonProperty]
        public List<Model> models { get; set; } = new List<Model>();
    }
}
