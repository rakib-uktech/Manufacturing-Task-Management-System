using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Data.Odbc;
using System.Threading.Tasks;

namespace TPL_TM.Pages.Api
{
    [Route("api/workorder")]
    [ApiController]
    public class ScheduleNetSuiteApiController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ScheduleNetSuiteApiController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost("update")]
        public async Task<IActionResult> UpdateWorkOrderTime([FromBody] WorkOrderUpdateDto dto)
        {
            if (dto == null || dto.InternalId <= 0 || dto.StartHour >= dto.EndHour)
                return BadRequest("Invalid data.");

            if (!DateTime.TryParse(dto.Date, out var taskDate) ||
                !DateTime.TryParse(dto.EndDate, out var taskEndDate))
                return BadRequest("Invalid date format.");

            try
            {
                string connStr = _configuration.GetConnectionString("NetSuiteOdbc");
                using (var conn = new OdbcConnection(connStr))
                {
                    await conn.OpenAsync();

                    // Explicitly cast to double to avoid Math.Floor ambiguity
                    var startHour = (int)Math.Floor((double)dto.StartHour);
                    var startMin = (int)Math.Floor(((double)dto.StartHour - startHour) * 60);

                    var endHour = (int)Math.Floor((double)dto.EndHour);
                    var endMin = (int)Math.Floor(((double)dto.EndHour - endHour) * 60);

                    string startTime = $"{dto.Date} {startHour:D2}:{startMin:D2}:00";
                    string endTime = $"{dto.EndDate} {endHour:D2}:{endMin:D2}:00";

                    string updateSql = @"
                    UPDATE transaction
                    SET startdate = ?, 
                        custbodyproduction_start_time = ?, 
                        custbodyproduction_end_time = ?
                    WHERE id = ? AND recordtype = 'workorder'";

                    using (var cmd = new OdbcCommand(updateSql, conn))
                    {
                        cmd.Parameters.AddWithValue("?", taskDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("?", startTime);
                        cmd.Parameters.AddWithValue("?", endTime);
                        cmd.Parameters.AddWithValue("?", dto.InternalId);

                        int affected = await cmd.ExecuteNonQueryAsync();
                        if (affected == 0)
                            return NotFound("Work order not found or update failed.");
                    }
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"NetSuite update error: {ex.Message}");
            }
        }


        public class WorkOrderUpdateDto
        {
            public int InternalId { get; set; }    // NEW: internalid from NetSuite
            public string Date { get; set; }
            public string EndDate { get; set; }
            public int StartHour { get; set; }
            public int EndHour { get; set; }
        }
    }

}
