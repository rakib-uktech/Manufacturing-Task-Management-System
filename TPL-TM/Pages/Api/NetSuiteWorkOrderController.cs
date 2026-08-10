using Microsoft.AspNetCore.Mvc;
using NetSuite;

namespace TPL_TM.Pages.Api
{
    [Route("api/netsuite/workorder")]
    [ApiController]
    public class NetSuiteWorkOrderController : ControllerBase
    {
        private readonly NetSuiteClient _netSuiteClient;

        public NetSuiteWorkOrderController(NetSuiteClient netSuiteClient)
        {
            _netSuiteClient = netSuiteClient;
        }

        public class WorkOrderUpdateRequest
        {
            public int WorkOrderId { get; set; }
            public string StartDate { get; set; }
            public string EndDate { get; set; }
            public int StartHour { get; set; }
            public int EndHour { get; set; }
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] WorkOrderUpdateRequest request)
        {
            if (request == null)
                return BadRequest(new { success = false, message = "No request body provided." });

            try
            {
                // Parse incoming dates
                if (!DateTime.TryParse(request.StartDate, out var startDate))
                    return BadRequest(new { success = false, message = "Invalid StartDate format." });
                if (!DateTime.TryParse(request.EndDate, out var endDate))
                    return BadRequest(new { success = false, message = "Invalid EndDate format." });

                // Combine date + hour into full datetime for main transaction
                var startDateTime = startDate.Date.AddHours(request.StartHour);
                var endDateTime = endDate.Date.AddHours(request.EndHour);

                // Format custom fields for NetSuite (12-hour format)
                string startTimeString = DateTime.Today.AddHours(request.StartHour).ToString("hh:mm tt"); // e.g., 06:00 AM
                string endTimeString = DateTime.Today.AddHours(request.EndHour).ToString("hh:mm tt"); // e.g., 08:00 AM

                // Debug: log payload
                Console.WriteLine($"Updating WO {request.WorkOrderId}: {startDateTime} - {endDateTime}, {startTimeString} - {endTimeString}");

                // Call NetSuite REST API to update
                var updatedWorkOrder = await _netSuiteClient.UpdateWorkOrderScheduleAsync(
                    workOrderId: request.WorkOrderId.ToString(),
                    startDate: startDateTime,
                    endDate: endDateTime,
                    customStartTime: startTimeString,
                    customEndTime: endTimeString
                );

                return Ok(new
                {
                    success = true,
                    message = "Work order updated successfully in NetSuite",
                    updatedWorkOrder
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Failed to update work order: {ex.Message}"
                });
            }
        }
    }
}
