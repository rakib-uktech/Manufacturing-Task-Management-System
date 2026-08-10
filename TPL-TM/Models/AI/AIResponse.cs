namespace TPL_TM.Models.AI
{
    public class AIResponse
    {
        public bool Success { get; set; }

        public string Answer { get; set; } = "";

        public string Error { get; set; } = "";
    }
}