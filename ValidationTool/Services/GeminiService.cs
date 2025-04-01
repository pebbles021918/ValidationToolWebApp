using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class GeminiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        var apiKey = _configuration["GeminiAI:ApiKey"];  
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    public async Task<string> GenerateSoapNote(string patientInfo)
    {
        if (string.IsNullOrEmpty(_configuration["GeminiAI:ApiKey"]))
        {
            _logger.LogError("Missing Gemini AI API Key in appsettings.json.");
            return "Error: Gemini AI API key is missing!";
        }

        var requestBody = new
        {
            contents = new[]
            {
                new {
                    role = "system",
                    parts = new[]
                    {
                        new { text = @"
                            You are an AI-powered medical assistant specializing in generating SOAP notes. 
                            Your responses should always be structured in the SOAP format:

                            **S - Subjective:** Patient's chief complaint, medical history, and relevant symptoms.
                            **O - Objective:** Measurable data such as vital signs, lab results, and physical exam findings.
                            **A - Assessment:** Diagnosis or differential diagnosis based on available information.
                            **P - Plan:** Recommended treatments, medications, follow-up actions, and next steps.

                            Always maintain a **professional, concise, and medically appropriate tone**. 
                            Do not provide personal opinions. If information is insufficient, state that additional details are needed."
                        }
                    }
                },
                new {
                    role = "user",
                    parts = new[]
                    {
                        new { text = $"Generate a SOAP note for a patient using the following details:\n\n{patientInfo}" }
                    }
                }
            }
        };

        var jsonRequest = JsonSerializer.Serialize(requestBody);
        var requestContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await _httpClient.PostAsync(
                "https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent",  
                requestContent,
                cts.Token
            );

            if (!response.IsSuccessStatusCode)
            {
                string errorMessage = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.BadRequest => "Bad Request: Check your API call format.",
                    System.Net.HttpStatusCode.Unauthorized => "Unauthorized: Invalid API Key.",
                    System.Net.HttpStatusCode.Forbidden => "Forbidden: Check your API subscription level.",
                    System.Net.HttpStatusCode.TooManyRequests => "Rate limit exceeded. Please wait and try again.",
                    System.Net.HttpStatusCode.InternalServerError => "Gemini AI API is experiencing issues. Try again later.",
                    _ => $"Error: {response.StatusCode}"
                };

                _logger.LogError($"API Error: {response.StatusCode} - {errorMessage}");
                return errorMessage;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(responseBody);

            var result = jsonDoc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return result ?? "No response from API.";
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("Request timed out after 30 seconds.");
            return "Error: The request timed out. Gemini AI may be experiencing high traffic.";
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected error: {ex.Message}");
            return $"Unexpected error: {ex.Message}";
        }
    }
}
