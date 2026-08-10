using TPL_TM.Models.AI;
namespace TPL_TM.Services.AI
{
    public interface IOpenAIService
    {
        Task<AIResponse> AskAsync(AIRequest request);
    }
}
