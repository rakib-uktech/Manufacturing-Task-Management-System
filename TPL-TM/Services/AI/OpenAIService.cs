using System.Text;
using System.Text.Json;
using TPL_TM.Models.AI;

namespace TPL_TM.Services.AI
{
    public class OpenAIService : IOpenAIService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly IKnowledgeService _knowledgeService;


        public OpenAIService(
            IConfiguration configuration,
            HttpClient httpClient,
            IKnowledgeService knowledgeService)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _knowledgeService = knowledgeService;
        }



        public async Task<AIResponse> AskAsync(AIRequest request)
        {
            try
            {

                // ===============================
                // RAG STEP 1
                // Retrieve SOP Knowledge
                // ===============================

                var ragContext =
                    await _knowledgeService
                    .GetContextAsync(request.Question);



                // Combine user supplied context + RAG context

                var finalContext = $@"

Company Knowledge:

{ragContext}


Additional Context:

{request.Context}

";



                var apiKey =
                    _configuration["OpenAI:ApiKey"];



                _httpClient.DefaultRequestHeaders.Clear();


                _httpClient.DefaultRequestHeaders.Add(
                    "Authorization",
                    $"Bearer {apiKey}");




                var systemPrompt = """

You are the Manufacturing AI Assistant for
TPL Manufacturing Task Management System.

Your responsibilities:

- Production support
- Quality procedures
- Inventory operations
- CAPA management
- Shift management
- OEE explanation
- NetSuite ERP guidance


Rules:

1. Always use the provided company knowledge first.

2. Never invent SOP procedures.

3. If information is unavailable,
reply:
"I could not find this information in the approved Manufacturing Knowledge Base."


4. Provide clear step-by-step answers.

5. Keep answers suitable for operators,
supervisors and management.

""";




                var body = new
                {
                    model = "gpt-5",

                    input =
                    $"{systemPrompt}\n\n" +

                    $"Knowledge:\n{finalContext}\n\n" +

                    $"Question:\n{request.Question}"
                };




                var json =
                    JsonSerializer.Serialize(body);



                var response =
                    await _httpClient.PostAsync(
                        "https://api.openai.com/v1/responses",

                        new StringContent(
                            json,
                            Encoding.UTF8,
                            "application/json"));




                if (!response.IsSuccessStatusCode)
                {
                    return new AIResponse
                    {
                        Success = false,
                        Error =
                        await response.Content.ReadAsStringAsync()
                    };
                }



                var responseJson =
                    await response.Content.ReadAsStringAsync();



                using JsonDocument doc =
                    JsonDocument.Parse(responseJson);



                string answer = "";



                if (doc.RootElement
                    .TryGetProperty("output", out var output))
                {

                    foreach (var item in output.EnumerateArray())
                    {

                        if (item.TryGetProperty(
                            "content",
                            out var content))
                        {

                            foreach (var c in content.EnumerateArray())
                            {

                                if (c.TryGetProperty(
                                    "text",
                                    out var txt))
                                {
                                    answer += txt.GetString();
                                }

                            }

                        }

                    }

                }



                return new AIResponse
                {
                    Success = true,
                    Answer = answer
                };

            }
            catch (Exception ex)
            {
                return new AIResponse
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }
    }
}