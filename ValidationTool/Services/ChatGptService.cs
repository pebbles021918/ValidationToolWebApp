using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class ChatGptService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatGptService> _logger;

    public ChatGptService(HttpClient httpClient, IConfiguration configuration, ILogger<ChatGptService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        var apiKey = _configuration["OpenAI:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    public async Task<string> GenerateSoapNote(string patientInfo)
    {
        if (string.IsNullOrEmpty(_configuration["OpenAI:ApiKey"]))
        {
            _logger.LogError("Missing OpenAI API Key in appsettings.json.");
            return "Error: OpenAI API key is missing!";
        }

        var requestBody = new
        {
            model = "gpt-4",
            messages = new[]
     {
        new {
            role = "system",
            content = @"
              You are a clinical documentation specialist.

A physician SOAP note is provided below. Evaluate it according to U.S. SOAP documentation standards, referencing:

- CMS (Centers for Medicare & Medicaid Services) Guidelines
- AAFP (American Academy of Family Physicians)
- The Joint Commission Standards
- AMA CPT® Guidelines for E/M Documentation

---

📋 Instructions:
1. For each section (Subjective, Objective, Assessment, Plan), review the listed subsections.
2. For each subsection, label it with:
   - ✅ Valid – Clearly documented
   - ❓ Vague – Present but unclear or insufficient
   - ❌ Missing – Not documented
3. Assign a score based on the following rules:
   - ✅ Valid → Full points
   - ❓ Vague → Half points
   - ❌ Missing → 0 points
4. Do not rewrite or summarize the note. Only evaluate and assign a result.
5. Output results in a table format showing:
   Section | Subsection | Evaluation (Valid/Vague/Missing) | Score
6. At the end, show the total score out of 100.

---

🧾 Subsections & Point Values

Subjective
- Patient arrival time – 10
- Patient condition at arrival – 15
- Chief complaint (in patient's own words) – 25
- History of present illness (HPI) – 20
- Past medical history relevant to complaint – 10
- Medications / Allergies – 10
- Social history (if relevant) – 5
- Review of systems (focused) – 5

Objective
- Vital signs (BP, HR, Temp, etc.) – 20
- General physical appearance – 10
- Focused physical exam findings – 30
- Lab/imaging results reviewed – 20
- Diagnostic test interpretation (brief) – 10
- Functional assessments (if applicable) – 10

Assessment
- Working diagnosis – 30
- Differential diagnosis (if applicable) – 15
- Clinical reasoning summary – 30
- Risk factors and severity – 15
- Response to previous treatment (if any) – 10

Plan
- Treatment plan (medications, procedures) – 30
- Follow-up and monitoring plan – 25
- Patient education or counseling provided – 15
- Referrals or consultations – 15
- Contingency plan (what to do if worsens) – 15

---

🧾 Output Format Example

| Section   | Subsection                               | Evaluation | Score |
|-----------|-------------------------------------------|------------|-------|
| Subjective| Chief complaint (in patient's own words) | ✅ Valid   | 25    |
| Subjective| Social history (if relevant)             | ❌ Missing | 0     |
| Assessment| Working diagnosis                        | ❓ Vague   | 15    |
| ...       | ...                                       | ...        | ...   |
| TOTAL     |                                           |            | 78    |

---

📝 SOAP Note Input:
```
[Insert the full SOAP note text here]
```"
        },
        new {
            role = "user",
            content = $@"
                Generate a SOAP note for a patient using the following details:
                {patientInfo}

                **Validation Request:**
                After generating the SOAP note, evaluate each section:
                - Indicate whether each section is **VALID or NOT VALID**.
                - Do NOT provide explanations for missing information.

                Format the response like this:
                ---
                **SOAP Note:**
                **S - Subjective:** [Generated Text]
                **Validity:** [Valid / Not Valid]

                **O - Objective:** [Generated Text]
                **Validity:** [Valid / Not Valid]

                **A - Assessment:** [Generated Text]
                **Validity:** [Valid / Not Valid]

                **P - Plan:** [Generated Text]
                **Validity:** [Valid / Not Valid]

                Ensure the response is **structured and professional** without additional explanations."
        }
    },
            temperature = 0.7
        };


        var jsonRequest = JsonSerializer.Serialize(requestBody);
        var requestContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", requestContent, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                string errorMessage = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.BadRequest => "Bad Request: Check your API call format.",
                    System.Net.HttpStatusCode.Unauthorized => "Unauthorized: Invalid API Key.",
                    System.Net.HttpStatusCode.Forbidden => "Forbidden: Check your API subscription level.",
                    System.Net.HttpStatusCode.TooManyRequests => "Rate limit exceeded. Please wait and try again.",
                    System.Net.HttpStatusCode.InternalServerError => "OpenAI API is experiencing issues. Try again later.",
                    _ => $"Error: {response.StatusCode}"
                };

                _logger.LogError($"API Error: {response.StatusCode} - {errorMessage}");
                return errorMessage;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(responseBody);
            var result = jsonDoc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            return result ?? "No response from API.";
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("Request timed out after 30 seconds.");
            return "Error: The request timed out. OpenAI may be experiencing high traffic.";
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected error: {ex.Message}");
            return $"Unexpected error: {ex.Message}";
        }
    }
}
