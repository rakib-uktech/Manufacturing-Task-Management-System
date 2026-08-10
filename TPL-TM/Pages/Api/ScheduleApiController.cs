using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Threading.Tasks;

namespace TPL_TM.Pages.Api
{
    [Route("api/schedule")]
    [ApiController]
    public class ScheduleApiController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ScheduleApiController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost("update")]
        public async Task<IActionResult> UpdateTaskTime([FromBody] UpdateTaskDto dto)
        {
            if (dto == null || dto.Id <= 0 || dto.StartHour >= dto.EndHour)
                return BadRequest("Invalid data.");

            if (!DateTime.TryParse(dto.Date, out var taskDate) ||
                !DateTime.TryParse(dto.EndDate, out var taskEndDate))
                return BadRequest("Invalid date format.");

            if (taskEndDate < taskDate)
                return BadRequest("End date cannot be earlier than start date.");

            try
            {
                var connStr = _configuration.GetConnectionString("DefaultConnection");
                using (var conn = new NpgsqlConnection(connStr))
                {
                    await conn.OpenAsync();

                    string updateSql = @"
                UPDATE task_schedule
                SET Task_Date = @Task_Date,
                    Task_EndDate = @Task_EndDate,
                    Task_StartTime = @StartTime,
                    Task_EndTime = @EndTime
                WHERE Id = @Id";

                    using (var cmd = new NpgsqlCommand(updateSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Task_Date", taskDate);
                        cmd.Parameters.AddWithValue("@Task_EndDate", taskEndDate);
                        cmd.Parameters.AddWithValue("@StartTime", new TimeSpan(dto.StartHour, 0, 0));
                        cmd.Parameters.AddWithValue("@EndTime", new TimeSpan(dto.EndHour, 0, 0));
                        cmd.Parameters.AddWithValue("@Id", dto.Id);

                        int affected = await cmd.ExecuteNonQueryAsync();
                        if (affected == 0)
                            return NotFound("Task not found.");
                    }
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Database error: {ex.Message}");
            }
        }

        public class UpdateTaskDto
        {
            public int Id { get; set; }
            public string Date { get; set; } // Start Date: yyyy-MM-dd
            public string EndDate { get; set; } // End Date: yyyy-MM-dd
            public int StartHour { get; set; }
            public int EndHour { get; set; }
        }

    }
}
