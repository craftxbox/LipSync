using Newtonsoft.Json;

namespace OpenLipSync.Inference;

/// <summary>
/// Model configuration loaded from config.json
/// </summary>
public class ModelConfig
{
    [JsonProperty("model")] public ModelInfo? Model { get; set; }
    [JsonProperty("audio")] public AudioInfo? Audio { get; set; }
    [JsonProperty("training")] public TrainingInfo? Training { get; set; }
}