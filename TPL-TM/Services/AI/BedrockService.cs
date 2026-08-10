using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using System.Text;
using System.Text.Json;
using TPL_TM.Models.AI;

namespace TPL_TM.Services.AI;

public class BedrockService : IOpenAIService
{
    private readonly IConfiguration _configuration;
    private readonly AmazonBedrockRuntimeClient _client;
    private readonly IKnowledgeService _knowledgeService;
    private readonly ManufacturingAIService _manufacturingAIService;


    public BedrockService(
        IConfiguration configuration,
        IKnowledgeService knowledgeService,
        ManufacturingAIService manufacturingAIService)
    {
        _configuration = configuration;
        _knowledgeService = knowledgeService;
        _manufacturingAIService = manufacturingAIService;

        var awsConfig = configuration.GetSection("AWS");

        _client = new AmazonBedrockRuntimeClient(
            awsConfig["AccessKey"],
            awsConfig["SecretKey"],
            RegionEndpoint.GetBySystemName(
                awsConfig["Region"] ?? "eu-west-1"));
    }


    public async Task<AIResponse> AskAsync(AIRequest request)
    {
        try
        {
            // =====================================
            // RAG STEP 1: Retrieve SOP Knowledge
            // =====================================
            var ragContext =
    await _knowledgeService.GetContextAsync(request.Question);

            var manufacturingContext =
                await _manufacturingAIService.GetContextAsync(request.Question);

            var finalContext = $@"

===== COMPANY SOP =====

{ragContext}

===== LIVE MANUFACTURING DATA =====

{manufacturingContext}

===== USER CONTEXT =====

{request.Context}

";

            // =====================================
            // SYSTEM PROMPT
            // =====================================
            var systemPrompt = @"
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
3. If information is unavailable, reply exactly:
   'I could not find this information in the approved Manufacturing Knowledge Base.'
4. Provide clear step-by-step answers.
5. Keep answers suitable for operators, supervisors and management.
";

            // =====================================
            // Build Claude prompt
            // =====================================
            var prompt = $@"
{systemPrompt}

====================
COMPANY KNOWLEDGE
====================
{finalContext}

====================
QUESTION
====================
{request.Question}
";

            var requestBody = new
            {
                anthropic_version = "bedrock-2023-05-31",
                max_tokens = 1200,
                temperature = 0,
                messages = new[]
                {
                        new
                        {
                            role = "user",
                            content = new[]
                            {
                                new
                                {
                                    type = "text",
                                    text = prompt
                                }
                            }
                        }
                    }
            };

            var response = await _client.InvokeModelAsync(
                new InvokeModelRequest
                {
                    // Use the SAME working model
                    ModelId = "anthropic.claude-3-haiku-20240307-v1:0",
                    ContentType = "application/json",
                    Accept = "application/json",
                    Body = new MemoryStream(
                        Encoding.UTF8.GetBytes(
                            JsonSerializer.Serialize(requestBody)))
                });

            using var reader = new StreamReader(response.Body);
            var raw = await reader.ReadToEndAsync();

            using var doc = JsonDocument.Parse(raw);

            string answer = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? "Empty response";

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
                Error = ex.ToString()
            };
        }
    }
}