using System.Text.Json.Serialization;

namespace ParadeGuard.Api.Models
{
    public class ChatRequest
    {
        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = new();
    }

    public class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    public class GroqApiRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "llama-3.3-70b-versatile";

        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = new();

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; } = 500;

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.7;
    }

    public class GroqApiResponse
    {
        [JsonPropertyName("choices")]
        public List<GroqChoice> Choices { get; set; } = new();
    }

    public class GroqChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage Message { get; set; } = new();
    }
}