using System.Net.Http;
using System.Net.Http.Headers;
using Avalonia.Media.Imaging;

namespace DailyMealPlannerExtended.Services;

public class CloudflareAIService
{
    private readonly HttpClient _httpClient;
    private readonly string _accountId;
    private readonly string _apiToken;
    private const string ModelEndpoint = "@cf/black-forest-labs/flux-1-schnell";

    public CloudflareAIService()
    {
        _httpClient = new HttpClient();
        _apiToken = CloudflareConfig.WorkersAIToken;

        // Extract account ID from the token (Cloudflare tokens typically contain the account ID)
        // For now, we'll use a default or require it in .env
        _accountId = CloudflareConfig.AccountId;

        if (string.IsNullOrEmpty(_accountId))
        {
            // Try to extract from token or use a placeholder
            // Cloudflare account IDs are typically 32-character hex strings
            Logger.Instance.Warning("Cloudflare Account ID not configured. Please add CLOUDFLARE_ACCOUNT_ID to .env file");
        }
    }

    /// <summary>
    /// Generates an AI image for a food product
    /// </summary>
    /// <param name="productName">The name of the product</param>
    /// <param name="alternateNames">Alternate names for the product</param>
    /// <param name="description">Description of the product</param>
    /// <returns>A bitmap image, or null if generation failed</returns>
    public async Task<Bitmap?> GenerateProductImageAsync(string productName, string? alternateNames = null, string? description = null)
    {
        if (string.IsNullOrEmpty(_apiToken))
        {
            Logger.Instance.Warning("Cloudflare Workers AI token not configured");
            return null;
        }

        if (string.IsNullOrEmpty(_accountId))
        {
            Logger.Instance.Warning("Cloudflare Account ID not configured");
            return null;
        }

        try
        {
            // Build the prompt
            var prompt = BuildPrompt(productName, alternateNames, description);

            Logger.Instance.Information("Generating AI image for: {Product}", productName);

            // Create the API URL
            var url = $"https://api.cloudflare.com/client/v4/accounts/{_accountId}/ai/run/{ModelEndpoint}";

            // Create JSON request body
            var requestBody = new
            {
                prompt = prompt,
                num_steps = 4 // Faster generation with fewer steps
            };

            var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            // Create request
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);

            // Send request
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Logger.Instance.Error("Cloudflare AI API error: {StatusCode} - {Error}",
                    response.StatusCode, errorContent);
                return null;
            }

            // Read the JSON response
            var responseContent = await response.Content.ReadAsStringAsync();

            // Parse JSON to get the base64 image
            using var jsonDoc = System.Text.Json.JsonDocument.Parse(responseContent);

            // The image is wrapped in a "result" property
            if (!jsonDoc.RootElement.TryGetProperty("result", out var resultElement))
            {
                Logger.Instance.Error("Cloudflare AI response missing 'result' property: {Response}", responseContent);
                return null;
            }

            if (!resultElement.TryGetProperty("image", out var imageElement))
            {
                Logger.Instance.Error("Cloudflare AI response missing 'image' property in result: {Response}", responseContent);
                return null;
            }

            var base64Image = imageElement.GetString();
            if (string.IsNullOrEmpty(base64Image))
            {
                Logger.Instance.Warning("Received empty image from Cloudflare AI");
                return null;
            }

            // Convert base64 to bytes
            var imageBytes = Convert.FromBase64String(base64Image);

            // Convert to Avalonia Bitmap
            using var stream = new MemoryStream(imageBytes);
            var bitmap = new Bitmap(stream);

            Logger.Instance.Information("Successfully generated AI image for: {Product} ({Size} bytes)",
                productName, imageBytes.Length);

            return bitmap;
        }
        catch (HttpRequestException ex)
        {
            Logger.Instance.Error(ex, "HTTP error while generating AI image");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to generate AI image for: {Product}", productName);
            return null;
        }
    }

    /// <summary>
    /// Builds a prompt for AI image generation
    /// </summary>
    private static string BuildPrompt(string productName, string? alternateNames, string? description)
    {
        var promptParts = new List<string>
        {
            "A high-quality, appetizing food photography of",
            productName
        };

        if (!string.IsNullOrEmpty(alternateNames))
        {
            promptParts.Add($"(also known as {alternateNames})");
        }

        if (!string.IsNullOrEmpty(description))
        {
            promptParts.Add($"- {description}");
        }

        promptParts.Add("on a clean white plate, professional food photography, natural lighting, 4k, detailed");

        var prompt = string.Join(" ", promptParts);

        Logger.Instance.Debug("Generated AI prompt: {Prompt}", prompt);

        return prompt;
    }

    /// <summary>
    /// Checks if the service is properly configured
    /// </summary>
    public bool IsConfigured()
    {
        return !string.IsNullOrEmpty(_apiToken) && !string.IsNullOrEmpty(_accountId);
    }
}
