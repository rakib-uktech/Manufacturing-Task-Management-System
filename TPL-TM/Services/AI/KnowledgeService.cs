using Npgsql;
using System.Text;

namespace TPL_TM.Services.AI
{
    public class KnowledgeService : IKnowledgeService
    {

        private readonly IConfiguration _configuration;


        public KnowledgeService(
            IConfiguration configuration)
        {
            _configuration = configuration;
        }


        private string ExpandQuestion(string question)
        {

            question = question.ToLower();


            if (question.Contains("start") &&
               question.Contains("shift"))
            {
                question += " shift entry initialization production start";
            }


            if (question.Contains("capa"))
            {
                question += " corrective preventive action";
            }


            if (question.Contains("downtime"))
            {
                question += " machine downtime recording";
            }


            return question;

        }
        public async Task<string> GetContextAsync(
            string question)
        {

            var result = new StringBuilder();



            using var con =
                new NpgsqlConnection(
                _configuration
                .GetConnectionString("DefaultConnection"));



            await con.OpenAsync();



            string sql = @"

                WITH search_words AS
                (
                    SELECT word
                    FROM regexp_split_to_table(lower(@q),' ') word
                    WHERE length(word) > 3
                )

                SELECT
                    title,
                    section,
                    content,
                    keywords,


                (
                    -- Title is strongest
                    (
                        SELECT COUNT(*)
                        FROM search_words sw
                        WHERE lower(title) LIKE '%' || sw.word || '%'
                    ) * 10


                    +

                    -- Keywords next
                    (
                        SELECT COUNT(*)
                        FROM search_words sw
                        WHERE lower(keywords) LIKE '%' || sw.word || '%'
                    ) * 7


                    +

                    -- Section
                    (
                        SELECT COUNT(*)
                        FROM search_words sw
                        WHERE lower(section) LIKE '%' || sw.word || '%'
                    ) * 5


                    +

                    -- Content
                    (
                        SELECT COUNT(*)
                        FROM search_words sw
                        WHERE lower(content) LIKE '%' || sw.word || '%'
                    ) * 2

                ) AS score


                FROM sop_documents


                WHERE

                EXISTS
                (
                    SELECT 1
                    FROM search_words sw

                    WHERE

                    lower(title) LIKE '%' || sw.word || '%'

                    OR

                    lower(keywords) LIKE '%' || sw.word || '%'

                    OR

                    lower(section) LIKE '%' || sw.word || '%'

                    OR

                    lower(content) LIKE '%' || sw.word || '%'
                )


                ORDER BY score DESC


                LIMIT 5;

                ";



            using var cmd =
                new NpgsqlCommand(sql, con);



            var searchQuestion = ExpandQuestion(question);


            cmd.Parameters.AddWithValue(
            "q",
            searchQuestion);



            using var reader =
                await cmd.ExecuteReaderAsync();



            while (await reader.ReadAsync())
            {

                result.AppendLine($@"

SOP:
{reader["title"]}

Section:
{reader["section"]}

Content:
{reader["content"]}

---------------------

");

            }


            return result.ToString();

        }
    }
}