using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Appdoptame.DataModel
{
    public class TodoItem
    {
        public string Id { get; set; }

        [JsonProperty(PropertyName = "text")]
        public string Text { get; set; }

        [JsonProperty(PropertyName = "latitud")]
        public string Latitud { get; set; }

        [JsonProperty(PropertyName = "longitud")]
        public string Longitud { get; set; }

        [JsonProperty(PropertyName = "descripcion")]
        public string Descripcion { get; set; }

        [JsonProperty(PropertyName = "complete")]
        public bool Complete { get; set; }

        [JsonProperty(PropertyName = "containerName")]
        public string ContainerName { get; set; }

        [JsonProperty(PropertyName = "resourceName")]
        public string ResourceName { get; set; }

        [JsonProperty(PropertyName = "sasQueryString")]
        public string SasQueryString { get; set; }

        [JsonProperty(PropertyName = "imageUri")]
        public string ImageUri { get; set; }
    }
}
