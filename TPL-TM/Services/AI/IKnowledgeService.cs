namespace TPL_TM.Services.AI
{
    public interface IKnowledgeService
    {
        Task<string> GetContextAsync(string question);
    }
}