using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace CarTechAssist.Contracts.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum IAFeedbackScoreDto : byte
    {
        [JsonPropertyName("NaoAvaliado")]
        NaoAvaliado = 0,

        [JsonPropertyName("MuitoRuim")]
        MuitoRuim = 1,

        [JsonPropertyName("Ruim")]
        Ruim = 2,

        [JsonPropertyName("Regular")]
        Regular = 3,

        [JsonPropertyName("Bom")]
        Bom = 4,

        [JsonPropertyName("Excelente")]
        Excelente = 5
    }
}
