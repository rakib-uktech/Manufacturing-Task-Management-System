using Npgsql;

namespace TPL_TM.Services.AI
{
    public class ManufacturingAIService
    {
        private readonly IConfiguration _configuration;

        public ManufacturingAIService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<string> GetContextAsync(string question)
        {
            return await Ask(question);
        }
        public async Task<string> Ask(string question)
        {
            question = question.ToLower();


            if (
               question.Contains("production") ||
               question.Contains("output") ||
               question.Contains("units") ||
               question.Contains("produced")
              )
            {
                return await GetProductionAnswer();
            }


            if (
                question.Contains("downtime") ||
                question.Contains("stoppage") ||
                question.Contains("lost time")
                )
            {
                return await GetDowntimeAnswer();
            }


            if (
                question.Contains("waste") ||
                question.Contains("scrap") ||
                question.Contains("reject")
                )
            {
                return await GetWasteAnswer();
            }


            return "I could not identify the manufacturing KPI.";
        }



        private async Task<string> GetProductionAnswer()
        {
            using var conn = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            await conn.OpenAsync();


            var sql = @"
                SELECT 
                    COALESCE(SUM(product_count),0) AS total,
                    COUNT(DISTINCT machine_name) AS machines
                FROM production_count
                WHERE timestamp_start >= CURRENT_DATE;
            ";


            using var cmd = new NpgsqlCommand(sql, conn);

            using var reader = await cmd.ExecuteReaderAsync();


            if (await reader.ReadAsync())
            {
                var total = reader.GetInt64(0);
                var machines = reader.GetInt64(1);


                return $@"
Today's Production:

Total Units Produced: {total:N0}

Active Machines: {machines}

Date: {DateTime.Today:dd/MM/yyyy}
";
            }


            return "No production data found today.";
        }



        private async Task<string> GetDowntimeAnswer()
        {
            using var conn = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            await conn.OpenAsync();


            var sql = @"
                SELECT 
                    COALESCE(SUM(downtime),0)
                FROM downtime
                WHERE created_on >= CURRENT_DATE;
            ";


            using var cmd = new NpgsqlCommand(sql, conn);

            var result = await cmd.ExecuteScalarAsync();


            return $@"
Today's Downtime:

{Convert.ToInt32(result):N0} minutes
";
        }




        private async Task<string> GetWasteAnswer()
        {
            using var conn = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            await conn.OpenAsync();


            var sql = @"
                SELECT 
                    COALESCE(SUM(waste_weight),0)
                FROM waste
                WHERE created_on >= CURRENT_DATE;
            ";


            using var cmd = new NpgsqlCommand(sql, conn);

            var result = await cmd.ExecuteScalarAsync();


            return $@"
Today's Waste:

{Convert.ToDecimal(result):N1} kg
";
        }
    }
}