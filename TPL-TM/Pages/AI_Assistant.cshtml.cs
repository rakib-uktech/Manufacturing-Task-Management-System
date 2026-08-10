using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TPL_TM.Models.AI;
using TPL_TM.Services.AI;

namespace TPL_TM.Pages
{
    public class AI_AssistantModel : PageModel
    {
        private readonly IOpenAIService _openAIService;
        private readonly ManufacturingAIService _manufacturingAIService;
        private readonly ILogger<AI_AssistantModel> _logger;

        public AI_AssistantModel(
          IOpenAIService openAIService,
          ManufacturingAIService manufacturingAIService,
          ILogger<AI_AssistantModel> logger)
        {
            _openAIService = openAIService;
            _manufacturingAIService = manufacturingAIService;
            _logger = logger;
        }

        public void OnGet()
        {

        }

        public class ChatRequest
        {
            public string Message { get; set; } = "";
        }

        public async Task<IActionResult> OnPostChatAsync(
            [FromBody] ChatRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.Message))
                {
                    return new JsonResult(new
                    {
                        success = false,
                        answer = "Question cannot be empty."
                    });
                }

                string context = "";

                var manufacturingData =
                    await _manufacturingAIService.Ask(request.Message);

                context = manufacturingData;


                var aiRequest = new AIRequest
                {
                    Question = request.Message,
                    Context = context
                };

                var aiResponse =
                    await _openAIService.AskAsync(aiRequest);

                if (!aiResponse.Success)
                {
                    return new JsonResult(new
                    {
                        success = false,
                        answer = aiResponse.Error
                    });
                }

                return new JsonResult(new
                {
                    success = true,
                    answer = aiResponse.Answer
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI Assistant Error");

                return new JsonResult(new
                {
                    success = false,
                    answer = ex.ToString()
                });
            }
        }
    }
}