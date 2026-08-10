using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TPL_TM.Pages
{
    public class Manning_Schedule_CalendarModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public Manning_Schedule_CalendarModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public void OnGet()
        {
        }
        public JsonResult OnGetManningData()
        {
            var resources = new List<object>();
            var events = new List<object>();

            using var conn = new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            conn.Open();


            string sql = @"
            SELECT
                id,
                schedule_date,
                machine_line,
                machine_name,
                employee_username,
                shift,
                comments

            FROM manning_schedule

            ORDER BY schedule_date";


            using var cmd = new NpgsqlCommand(sql, conn);

            using var reader = cmd.ExecuteReader();


            var machines = new Dictionary<string, string>();


            while (reader.Read())
            {

                string machine =
                    reader["machine_name"].ToString();


                string line =
                    reader["machine_line"].ToString();


                if (!machines.ContainsKey(machine))
                {
                    machines.Add(machine, line);
                }


                DateTime date =
                    Convert.ToDateTime(reader["schedule_date"]);



                events.Add(new
                {
                    id = reader["id"],

                    resourceId = machine,


                    title =
                        reader["employee_username"]
                        + " (Shift "
                        + reader["shift"]
                        + ")",


                    start =
                        date.ToString("yyyy-MM-dd"),


                    end =
                        date.AddDays(1)
                        .ToString("yyyy-MM-dd"),


                    employee =
                        reader["employee_username"].ToString(),


                    shift =
                        reader["shift"].ToString(),


                    machineLine = line,


                    comments =
                        reader["comments"].ToString(),



                    backgroundColor =
                        reader["shift"].ToString() == "A"
                        ? "#198754"
                        :
                        reader["shift"].ToString() == "B"
                        ? "#ffc107"
                        :
                        "#dc3545"
                });

            }



            foreach (var m in machines)
            {

                resources.Add(new
                {
                    id = m.Key,

                    title = m.Key,

                    machineLine = m.Value

                });

            }



            return new JsonResult(new
            {
                resources,
                events
            });
        }
    }
}
