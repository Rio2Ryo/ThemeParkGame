// ============================================================
// ThemeParkGame - LLM API Client Abstraction
// 複数LLMプロバイダー対応APIクライアント
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ThemeParkGame.AI
{
    /// <summary>Supported LLM provider types.</summary>
    public enum LLMProvider
    {
        GeminiFlash,    // Google Gemini Flash - recommended for real-time NPC chat
        OpenAI,         // OpenAI GPT-4o / GPT-4o-mini
        Claude          // Anthropic Claude
    }

    /// <summary>
    /// Represents a single message in the LLM conversation.
    /// </summary>
    [Serializable]
    public class LLMMessage
    {
        public string Role;    // "system", "user", "assistant"
        public string Content;

        public LLMMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }

    /// <summary>
    /// Result of an LLM API call.
    /// </summary>
    public class LLMResponse
    {
        public bool Success { get; set; }
        public string Content { get; set; }
        public string ErrorMessage { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public float LatencySeconds { get; set; }
    }

    /// <summary>
    /// Configuration for an LLM provider connection.
    /// </summary>
    [Serializable]
    public class LLMProviderConfig
    {
        public LLMProvider Provider;
        public string ApiKey;
        public string ModelId;
        public string Endpoint;
        public float Temperature;
        public int MaxTokens;

        /// <summary>Creates default config for the specified provider.</summary>
        public static LLMProviderConfig CreateDefault(LLMProvider provider)
        {
            return provider switch
            {
                LLMProvider.GeminiFlash => new LLMProviderConfig
                {
                    Provider = LLMProvider.GeminiFlash,
                    ModelId = "gemini-2.0-flash",
                    Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent",
                    Temperature = 0.8f,
                    MaxTokens = 256
                },
                LLMProvider.OpenAI => new LLMProviderConfig
                {
                    Provider = LLMProvider.OpenAI,
                    ModelId = "gpt-4o-mini",
                    Endpoint = "https://api.openai.com/v1/chat/completions",
                    Temperature = 0.8f,
                    MaxTokens = 256
                },
                LLMProvider.Claude => new LLMProviderConfig
                {
                    Provider = LLMProvider.Claude,
                    ModelId = "claude-sonnet-4-20250514",
                    Endpoint = "https://api.anthropic.com/v1/messages",
                    Temperature = 0.8f,
                    MaxTokens = 256
                },
                _ => throw new ArgumentOutOfRangeException(nameof(provider))
            };
        }
    }

    /// <summary>
    /// Tracks cumulative API usage for cost management.
    /// </summary>
    public class APIUsageTracker
    {
        public int TotalRequests { get; private set; }
        public int TotalPromptTokens { get; private set; }
        public int TotalCompletionTokens { get; private set; }
        public int FailedRequests { get; private set; }
        public float TotalLatencySeconds { get; private set; }

        // Cost estimation (rough USD per 1M tokens)
        private const float GeminiFlashInputCostPer1M = 0.075f;
        private const float GeminiFlashOutputCostPer1M = 0.30f;
        private const float OpenAIMiniInputCostPer1M = 0.15f;
        private const float OpenAIMiniOutputCostPer1M = 0.60f;
        private const float ClaudeInputCostPer1M = 3.0f;
        private const float ClaudeOutputCostPer1M = 15.0f;

        /// <summary>Budget limit in estimated USD. 0 = unlimited.</summary>
        public float BudgetLimitUSD { get; set; } = 0f;

        /// <summary>Current estimated cost in USD.</summary>
        public float EstimatedCostUSD { get; private set; }

        /// <summary>Whether the budget limit has been exceeded.</summary>
        public bool IsBudgetExceeded => BudgetLimitUSD > 0 && EstimatedCostUSD >= BudgetLimitUSD;

        /// <summary>Average latency across all requests.</summary>
        public float AverageLatency => TotalRequests > 0 ? TotalLatencySeconds / TotalRequests : 0f;

        public void RecordRequest(LLMResponse response, LLMProvider provider)
        {
            TotalRequests++;

            if (response.Success)
            {
                TotalPromptTokens += response.PromptTokens;
                TotalCompletionTokens += response.CompletionTokens;
                TotalLatencySeconds += response.LatencySeconds;

                // Estimate cost based on provider
                float inputCost, outputCost;
                switch (provider)
                {
                    case LLMProvider.GeminiFlash:
                        inputCost = response.PromptTokens * GeminiFlashInputCostPer1M / 1_000_000f;
                        outputCost = response.CompletionTokens * GeminiFlashOutputCostPer1M / 1_000_000f;
                        break;
                    case LLMProvider.OpenAI:
                        inputCost = response.PromptTokens * OpenAIMiniInputCostPer1M / 1_000_000f;
                        outputCost = response.CompletionTokens * OpenAIMiniOutputCostPer1M / 1_000_000f;
                        break;
                    case LLMProvider.Claude:
                        inputCost = response.PromptTokens * ClaudeInputCostPer1M / 1_000_000f;
                        outputCost = response.CompletionTokens * ClaudeOutputCostPer1M / 1_000_000f;
                        break;
                    default:
                        inputCost = 0;
                        outputCost = 0;
                        break;
                }
                EstimatedCostUSD += inputCost + outputCost;
            }
            else
            {
                FailedRequests++;
            }
        }

        public void Reset()
        {
            TotalRequests = 0;
            TotalPromptTokens = 0;
            TotalCompletionTokens = 0;
            FailedRequests = 0;
            TotalLatencySeconds = 0;
            EstimatedCostUSD = 0;
        }
    }

    /// <summary>
    /// Abstract base class for LLM API clients.
    /// Handles common concerns: retries, backoff, rate limiting.
    /// Concrete subclasses format requests for specific providers.
    /// </summary>
    public abstract partial class LLMApiClientBase
    {
        protected LLMProviderConfig Config { get; private set; }

        /// <summary>Maximum number of retry attempts on transient failures.</summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>Base delay in seconds for exponential backoff.</summary>
        public float BaseRetryDelay { get; set; } = 1.0f;

        /// <summary>Request timeout in seconds.</summary>
        public int TimeoutSeconds { get; set; } = 15;

        protected LLMApiClientBase(LLMProviderConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Sends a list of messages to the LLM and returns the response via callback.
        /// Handles retries with exponential backoff internally.
        /// </summary>
        public IEnumerator SendRequest(
            List<LLMMessage> messages,
            Action<LLMResponse> onComplete)
        {
            LLMResponse lastResponse = null;

            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                if (attempt > 0)
                {
                    // Exponential backoff: 1s, 2s, 4s, ...
                    float delay = BaseRetryDelay * Mathf.Pow(2, attempt - 1);
                    // Add jitter to prevent thundering herd
                    delay += UnityEngine.Random.Range(0f, delay * 0.3f);
                    yield return new WaitForSecondsRealtime(delay);
                    Debug.Log($"[LLMApiClient] Retry attempt {attempt}/{MaxRetries}");
                }

                float startTime = Time.realtimeSinceStartup;

                // Build the provider-specific request
                string url = BuildRequestUrl();
                string body = BuildRequestBody(messages);
                byte[] bodyBytes = Encoding.UTF8.GetBytes(body);

                using var request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = TimeoutSeconds;

                // Set headers per provider
                SetRequestHeaders(request);

                yield return request.SendWebRequest();

                float latency = Time.realtimeSinceStartup - startTime;

                if (request.result == UnityWebRequest.Result.Success)
                {
                    lastResponse = ParseResponse(request.downloadHandler.text, latency);
                    if (lastResponse.Success)
                    {
                        onComplete?.Invoke(lastResponse);
                        yield break;
                    }
                }
                else
                {
                    lastResponse = new LLMResponse
                    {
                        Success = false,
                        ErrorMessage = $"HTTP {request.responseCode}: {request.error}",
                        LatencySeconds = latency
                    };

                    // Don't retry on client errors (4xx) except 429 (rate limit)
                    if (request.responseCode >= 400 && request.responseCode < 500 &&
                        request.responseCode != 429)
                    {
                        Debug.LogWarning($"[LLMApiClient] Non-retryable error: {request.responseCode}");
                        break;
                    }
                }

                Debug.LogWarning($"[LLMApiClient] Request failed (attempt {attempt + 1}): {lastResponse?.ErrorMessage}");
            }

            // All retries exhausted
            onComplete?.Invoke(lastResponse ?? new LLMResponse
            {
                Success = false,
                ErrorMessage = "All retry attempts exhausted"
            });
        }

        /// <summary>Builds the full request URL for this provider.</summary>
        protected abstract string BuildRequestUrl();

        /// <summary>Builds the JSON request body for this provider.</summary>
        protected abstract string BuildRequestBody(List<LLMMessage> messages);

        /// <summary>Sets provider-specific HTTP headers.</summary>
        protected abstract void SetRequestHeaders(UnityWebRequest request);

        /// <summary>Parses the provider-specific JSON response.</summary>
        protected abstract LLMResponse ParseResponse(string jsonResponse, float latency);
    }

    // ================================================================
    // Gemini Flash Implementation
    // ================================================================

    /// <summary>
    /// Google Gemini Flash API client.
    /// Recommended for real-time NPC conversations due to low latency and cost.
    /// </summary>
    public class GeminiFlashClient : LLMApiClientBase
    {
        public GeminiFlashClient(LLMProviderConfig config) : base(config) { }

        protected override string BuildRequestUrl()
        {
            string url = Config.Endpoint.Replace("{model}", Config.ModelId);
            return $"{url}?key={Config.ApiKey}";
        }

        protected override string BuildRequestBody(List<LLMMessage> messages)
        {
            // Gemini uses a different message format: system instruction + contents
            var sb = new StringBuilder();
            sb.Append("{");

            // Extract system message if present
            string systemInstruction = null;
            var contentMessages = new List<LLMMessage>();

            foreach (var msg in messages)
            {
                if (msg.Role == "system")
                {
                    systemInstruction = msg.Content;
                }
                else
                {
                    contentMessages.Add(msg);
                }
            }

            // System instruction
            if (systemInstruction != null)
            {
                sb.Append("\"system_instruction\":{\"parts\":[{\"text\":");
                sb.Append(JsonEscape(systemInstruction));
                sb.Append("}]},");
            }

            // Contents (conversation turns)
            sb.Append("\"contents\":[");
            for (int i = 0; i < contentMessages.Count; i++)
            {
                if (i > 0) sb.Append(",");
                string role = contentMessages[i].Role == "assistant" ? "model" : "user";
                sb.Append("{\"role\":\"");
                sb.Append(role);
                sb.Append("\",\"parts\":[{\"text\":");
                sb.Append(JsonEscape(contentMessages[i].Content));
                sb.Append("}]}");
            }
            sb.Append("],");

            // Generation config
            sb.Append("\"generationConfig\":{");
            sb.Append($"\"temperature\":{Config.Temperature},");
            sb.Append($"\"maxOutputTokens\":{Config.MaxTokens}");
            sb.Append("}");

            sb.Append("}");
            return sb.ToString();
        }

        protected override void SetRequestHeaders(UnityWebRequest request)
        {
            request.SetRequestHeader("Content-Type", "application/json");
        }

        protected override LLMResponse ParseResponse(string jsonResponse, float latency)
        {
            try
            {
                var json = JsonUtility.FromJson<GeminiResponseWrapper>(jsonResponse);
                if (json?.candidates != null && json.candidates.Length > 0 &&
                    json.candidates[0].content?.parts != null &&
                    json.candidates[0].content.parts.Length > 0)
                {
                    return new LLMResponse
                    {
                        Success = true,
                        Content = json.candidates[0].content.parts[0].text,
                        PromptTokens = json.usageMetadata?.promptTokenCount ?? 0,
                        CompletionTokens = json.usageMetadata?.candidatesTokenCount ?? 0,
                        TotalTokens = json.usageMetadata?.totalTokenCount ?? 0,
                        LatencySeconds = latency
                    };
                }

                return new LLMResponse
                {
                    Success = false,
                    ErrorMessage = "Gemini response parsing failed: no valid candidates",
                    LatencySeconds = latency
                };
            }
            catch (Exception ex)
            {
                return new LLMResponse
                {
                    Success = false,
                    ErrorMessage = $"Gemini response parsing exception: {ex.Message}",
                    LatencySeconds = latency
                };
            }
        }

        // Gemini response JSON structures (for JsonUtility)
        [Serializable]
        private class GeminiResponseWrapper
        {
            public GeminiCandidate[] candidates;
            public GeminiUsageMetadata usageMetadata;
        }

        [Serializable]
        private class GeminiCandidate
        {
            public GeminiContent content;
        }

        [Serializable]
        private class GeminiContent
        {
            public GeminiPart[] parts;
        }

        [Serializable]
        private class GeminiPart
        {
            public string text;
        }

        [Serializable]
        private class GeminiUsageMetadata
        {
            public int promptTokenCount;
            public int candidatesTokenCount;
            public int totalTokenCount;
        }
    }

    // ================================================================
    // OpenAI Implementation
    // ================================================================

    /// <summary>
    /// OpenAI Chat Completions API client (GPT-4o, GPT-4o-mini).
    /// </summary>
    public class OpenAIClient : LLMApiClientBase
    {
        public OpenAIClient(LLMProviderConfig config) : base(config) { }

        protected override string BuildRequestUrl()
        {
            return Config.Endpoint;
        }

        protected override string BuildRequestBody(List<LLMMessage> messages)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"model\":\"{Config.ModelId}\",");
            sb.Append($"\"temperature\":{Config.Temperature},");
            sb.Append($"\"max_tokens\":{Config.MaxTokens},");
            sb.Append("\"messages\":[");

            for (int i = 0; i < messages.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("{\"role\":\"");
                sb.Append(messages[i].Role);
                sb.Append("\",\"content\":");
                sb.Append(JsonEscape(messages[i].Content));
                sb.Append("}");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        protected override void SetRequestHeaders(UnityWebRequest request)
        {
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {Config.ApiKey}");
        }

        protected override LLMResponse ParseResponse(string jsonResponse, float latency)
        {
            try
            {
                var json = JsonUtility.FromJson<OpenAIResponseWrapper>(jsonResponse);
                if (json?.choices != null && json.choices.Length > 0 &&
                    json.choices[0].message != null)
                {
                    return new LLMResponse
                    {
                        Success = true,
                        Content = json.choices[0].message.content,
                        PromptTokens = json.usage?.prompt_tokens ?? 0,
                        CompletionTokens = json.usage?.completion_tokens ?? 0,
                        TotalTokens = json.usage?.total_tokens ?? 0,
                        LatencySeconds = latency
                    };
                }

                return new LLMResponse
                {
                    Success = false,
                    ErrorMessage = "OpenAI response parsing failed: no valid choices",
                    LatencySeconds = latency
                };
            }
            catch (Exception ex)
            {
                return new LLMResponse
                {
                    Success = false,
                    ErrorMessage = $"OpenAI response parsing exception: {ex.Message}",
                    LatencySeconds = latency
                };
            }
        }

        [Serializable]
        private class OpenAIResponseWrapper
        {
            public OpenAIChoice[] choices;
            public OpenAIUsage usage;
        }

        [Serializable]
        private class OpenAIChoice
        {
            public OpenAIMessage message;
        }

        [Serializable]
        private class OpenAIMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
        private class OpenAIUsage
        {
            public int prompt_tokens;
            public int completion_tokens;
            public int total_tokens;
        }
    }

    // ================================================================
    // Claude (Anthropic) Implementation
    // ================================================================

    /// <summary>
    /// Anthropic Claude Messages API client.
    /// </summary>
    public class ClaudeClient : LLMApiClientBase
    {
        private const string AnthropicVersion = "2023-06-01";

        public ClaudeClient(LLMProviderConfig config) : base(config) { }

        protected override string BuildRequestUrl()
        {
            return Config.Endpoint;
        }

        protected override string BuildRequestBody(List<LLMMessage> messages)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"model\":\"{Config.ModelId}\",");
            sb.Append($"\"max_tokens\":{Config.MaxTokens},");
            sb.Append($"\"temperature\":{Config.Temperature},");

            // Claude separates system from messages
            string systemContent = null;
            var conversationMessages = new List<LLMMessage>();

            foreach (var msg in messages)
            {
                if (msg.Role == "system")
                {
                    systemContent = msg.Content;
                }
                else
                {
                    conversationMessages.Add(msg);
                }
            }

            if (systemContent != null)
            {
                sb.Append("\"system\":");
                sb.Append(JsonEscape(systemContent));
                sb.Append(",");
            }

            sb.Append("\"messages\":[");
            for (int i = 0; i < conversationMessages.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("{\"role\":\"");
                sb.Append(conversationMessages[i].Role);
                sb.Append("\",\"content\":");
                sb.Append(JsonEscape(conversationMessages[i].Content));
                sb.Append("}");
            }
            sb.Append("]}");

            return sb.ToString();
        }

        protected override void SetRequestHeaders(UnityWebRequest request)
        {
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-api-key", Config.ApiKey);
            request.SetRequestHeader("anthropic-version", AnthropicVersion);
        }

        protected override LLMResponse ParseResponse(string jsonResponse, float latency)
        {
            try
            {
                var json = JsonUtility.FromJson<ClaudeResponseWrapper>(jsonResponse);
                if (json?.content != null && json.content.Length > 0)
                {
                    return new LLMResponse
                    {
                        Success = true,
                        Content = json.content[0].text,
                        PromptTokens = json.usage?.input_tokens ?? 0,
                        CompletionTokens = json.usage?.output_tokens ?? 0,
                        TotalTokens = (json.usage?.input_tokens ?? 0) + (json.usage?.output_tokens ?? 0),
                        LatencySeconds = latency
                    };
                }

                return new LLMResponse
                {
                    Success = false,
                    ErrorMessage = "Claude response parsing failed: no valid content",
                    LatencySeconds = latency
                };
            }
            catch (Exception ex)
            {
                return new LLMResponse
                {
                    Success = false,
                    ErrorMessage = $"Claude response parsing exception: {ex.Message}",
                    LatencySeconds = latency
                };
            }
        }

        [Serializable]
        private class ClaudeResponseWrapper
        {
            public ClaudeContentBlock[] content;
            public ClaudeUsage usage;
        }

        [Serializable]
        private class ClaudeContentBlock
        {
            public string type;
            public string text;
        }

        [Serializable]
        private class ClaudeUsage
        {
            public int input_tokens;
            public int output_tokens;
        }
    }

    // ================================================================
    // Client Factory
    // ================================================================

    /// <summary>
    /// Factory for creating the appropriate LLM client based on provider configuration.
    /// </summary>
    public static class LLMClientFactory
    {
        /// <summary>
        /// Creates an LLM API client instance for the specified provider config.
        /// </summary>
        public static LLMApiClientBase Create(LLMProviderConfig config)
        {
            return config.Provider switch
            {
                LLMProvider.GeminiFlash => new GeminiFlashClient(config),
                LLMProvider.OpenAI => new OpenAIClient(config),
                LLMProvider.Claude => new ClaudeClient(config),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(config), $"Unsupported LLM provider: {config.Provider}")
            };
        }
    }

    // ================================================================
    // JSON Utility
    // ================================================================

    /// <summary>
    /// Extension methods shared by all client implementations.
    /// </summary>
    public abstract partial class LLMApiClientBase
    {
        /// <summary>
        /// Escapes a string for safe inclusion in a JSON value.
        /// Returns the string wrapped in double quotes.
        /// </summary>
        protected static string JsonEscape(string input)
        {
            if (input == null) return "null";

            var sb = new StringBuilder(input.Length + 16);
            sb.Append('"');

            foreach (char c in input)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append($"\\u{(int)c:X4}");
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }
    }
}
