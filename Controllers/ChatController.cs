using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ParadeGuard.Api.Config;
using ParadeGuard.Api.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ParadeGuard.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("ApiPolicy")]
    public class ChatController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ApiKeysConfig _apiKeys;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            IHttpClientFactory httpClientFactory,
            IOptions<ApiKeysConfig> apiKeys,
            ILogger<ChatController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _apiKeys = apiKeys.Value;
            _logger = logger;
        }

        [HttpPost]
        [ProducesResponseType(typeof(GroqApiResponse), 200)]
        [ProducesResponseType(typeof(ApiError), 400)]
        [ProducesResponseType(typeof(ApiError), 500)]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            var requestId = HttpContext.TraceIdentifier;

            try
            {
                if (request?.Messages == null || !request.Messages.Any())
                {
                    _logger.LogWarning("Invalid chat request. RequestId: {RequestId}", requestId);
                    return BadRequest(new ApiError
                    {
                        Message = "Invalid request: messages array is required",
                        RequestId = requestId
                    });
                }

                // Get API key from ApiKeysConfig (which reads from environment)
                if (string.IsNullOrWhiteSpace(_apiKeys.GroqApiKey))
                {
                    _logger.LogError("GROQ_API_KEY is not configured. RequestId: {RequestId}", requestId);
                    return StatusCode(500, new ApiError
                    {
                        Message = "Server configuration error: API key not configured",
                        RequestId = requestId
                    });
                }

                var groqRequest = new GroqApiRequest
                {
                    Model = "llama-3.3-70b-versatile",
                    Messages = request.Messages,
                    MaxTokens = 500,
                    Temperature = 0.7
                };

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _apiKeys.GroqApiKey);
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                var jsonContent = JsonSerializer.Serialize(groqRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogDebug("Sending request to Groq API. RequestId: {RequestId}", requestId);

                var response = await httpClient.PostAsync(
                    "https://api.groq.com/openai/v1/chat/completions",
                    content
                );

                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Groq API Error: {StatusCode} - {Content}. RequestId: {RequestId}",
                        response.StatusCode, responseContent, requestId);

                    return StatusCode((int)response.StatusCode, new ApiError
                    {
                        Message = $"API Error: {response.StatusCode} - {response.ReasonPhrase}",
                        Details = responseContent,
                        RequestId = requestId
                    });
                }

                var groqResponse = JsonSerializer.Deserialize<GroqApiResponse>(
                    responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (groqResponse?.Choices == null || !groqResponse.Choices.Any())
                {
                    _logger.LogError("Invalid response format from Groq API. RequestId: {RequestId}", requestId);
                    return StatusCode(500, new ApiError
                    {
                        Message = "Invalid response format from API",
                        RequestId = requestId
                    });
                }

                _logger.LogInformation("Chat request completed successfully. RequestId: {RequestId}", requestId);

                return Ok(groqResponse);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while calling Groq API. RequestId: {RequestId}", requestId);
                return StatusCode(503, new ApiError
                {
                    Message = "External service temporarily unavailable",
                    RequestId = requestId
                });
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout while calling Groq API. RequestId: {RequestId}", requestId);
                return StatusCode(408, new ApiError
                {
                    Message = "Request timeout",
                    RequestId = requestId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in chat endpoint. RequestId: {RequestId}", requestId);
                return StatusCode(500, new ApiError
                {
                    Message = "Internal server error",
                    RequestId = requestId
                });
            }
        }
    }
}