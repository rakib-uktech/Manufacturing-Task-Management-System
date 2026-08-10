using Microsoft.AspNetCore.Mvc.RazorPages;
using Markdig;
using System.Collections.Generic;

namespace TPL_TM.Pages
{
    public class HelpModel : PageModel
    {
        // Sample data for help topics
        public List<HelpTopic> HelpTopics { get; set; }

        public void OnGet()
        {
            HelpTopics = new List<HelpTopic>
    {
        new HelpTopic
        {
            Name = "Assign Tasks",
            Description = @"📘 **User Manual: Assign Task Form**
🛠 **Overview**
The Assign Task form is used to assign tasks to team members. This form includes several fields for task details, such as Work Order, Task Name, Description, Assignee, Quantity, Unit, Priority Level, and Comments.

📝 **How to Use the Assign Task Form**
1. **Opening the Form**
    The Assign Task modal form will auto-open when the page loads for quick access to assigning new tasks.

2. **Form Fields**
   **Work Order:**
    - **Purpose**: Enter the unique reference number of the work order related to the task.
    - **How to Use**: Start typing the Work Order or select from the list of existing options. The list includes all available work orders from the system.
    - **Required**: Yes.
    - **Validation**: The form will display an error message if the work order is not selected.

   **Task Name:**
    - **Purpose**: Enter or select the task name (e.g., 'Inventory Request', 'Clear Waste').
    - **How to Use**: Start typing the Task Name or select from the list of existing task names. The system will automatically show matching options.
    - **Required**: Yes.
    - **Validation**: If no task name is selected, an error message will appear.

   **Task Description:**
    - **Purpose**: Enter a detailed description of the task.
    - **How to Use**: Start typing the Task Description or select from the available list of descriptions for common tasks.
    - **Required**: Yes.
    - **Validation**: If no description is selected, the system will show an error message.

   **Assign To:**
    - **Purpose**: Select the person or team who will be responsible for completing the task.
    - **How to Use**: Start typing the assignee's name or select from the list of available assignees.
    - **Required**: Yes.
    - **Validation**: An error message will appear if the assignee is not selected.

   **Quantity:**
    - **Purpose**: Enter the number of items or units related to the task.
    - **How to Use**: Type the quantity. The value must be at least 1.
    - **Required**: Yes.
    - **Validation**: The form will display an error message if the quantity is less than 1.

   **Unit:**
    - **Purpose**: Select the unit of measurement for the task (e.g., Piece, Case, Pallet).
    - **How to Use**: Select from the drop-down options: Piece, Case, Pallet, Kilogram.
    - **Required**: Yes.
    - **Validation**: An error message will appear if no unit is selected.

   **Priority Level:**
    - **Purpose**: Select the priority level for the task (e.g., High, Medium, Low).
    - **How to Use**: Choose the appropriate priority from the drop-down list.
    - **Required**: Yes.
    - **Validation**: An error message will appear if no priority level is selected.

   **Comments:**
    - **Purpose**: Optionally, enter any additional comments or special instructions for the task.
    - **How to Use**: Type the comments into the text area. This field is optional.
    - **Required**: No.

3. **Submitting the Form**
Once all required fields are filled in, click the Submit Task button at the bottom of the form to assign the task.

Validation: If any required field is missing or invalid, the system will prevent form submission and highlight the fields that need attention.

4. **Closing the Modal**
If you wish to close the form without submitting the task, click the Close (X) button at the top-right corner of the modal. This will close the form without saving any entered information.

📊 **Key Features**
- **Auto-Completion**: The form supports auto-completion for fields like Work Order, Task Name, Task Description, and Assign To, making it quicker to select predefined options.
- **Real-Time Validation**: The form provides immediate feedback if any required field is missing or incorrect, ensuring that all fields are filled out properly before submitting.
- **User-Friendly Interface**: The form is built with easy-to-use floating labels for each field, making it clear which data is required.

⚙️ **Troubleshooting**
- **Missing Data**: If you see an error message under a field (e.g., 'Work Order is required'), ensure you have filled in the required information before submitting.
- **Form Closes Unexpectedly**: If the form closes unexpectedly, ensure your browser or app is functioning correctly and that there are no conflicts with other modal dialogs.

🏁 **Final Notes**
Use this form to quickly assign tasks to team members, keeping track of important work orders, task details, and assignments.
The floating labels and dropdown lists ensure that task assignments are completed quickly and accurately.
"
        },
       new HelpTopic
{
    Name = "Requesting inventory",
    Description = @"📘 **User Manual: Request Inventory Form**

🛠 **Overview**  
The Request Inventory form is used to request inventory for tasks or machines. This form includes various fields for inventory details, such as Work Order, Inventory Name, Description, Machine Name, Quantity, Unit, Priority Level, and Comments.

---

📝 **How to Use the Request Inventory Form**

1. **Opening the Form**  
   - The Request Inventory modal will auto-open when you initiate a new inventory request.

2. **Form Fields**  

   **Work Order:**  
   - **Purpose:** Select the unique reference number of the work order related to the inventory request.  
   - **How to Use:** Start typing or select from the list of available work orders.  
   - **Required:** ✅  
   - **Validation:** An error message will appear if the field is left blank.

   **Inventory Name:**  
   - **Purpose:** Select or enter the name of the inventory item.  
   - **How to Use:** Type to search or select from filtered options.  
   - **Required:** ✅  
   - **Validation:** Must be selected or entered.

   **Inventory Description:**  
   - **Purpose:** View a detailed description of the selected inventory.  
   - **How to Use:** Auto-populated based on inventory name; read-only.  
   - **Required:** ❌

   **Machine Name:**  
   - **Purpose:** Identify the machine or team responsible for the inventory.  
   - **How to Use:** Type or choose from the dropdown.  
   - **Required:** ✅  
   - **Validation:** Field must not be empty.

   **Quantity:**  
   - **Purpose:** Enter the amount of inventory required.  
   - **How to Use:** Type a number (minimum 1).  
   - **Required:** ✅  
   - **Validation:** Must be at least 1.

   **Unit:**  
   - **Purpose:** Define the measurement unit (e.g., Piece, Case, Pallet, Kilogram).  
   - **How to Use:** Select from the dropdown.  
   - **Required:** ✅

   **Priority Level:**  
   - **Purpose:** Set the urgency level (High, Medium, Low).  
   - **How to Use:** Select from dropdown.  
   - **Required:** ✅

   **Comments:**  
   - **Purpose:** Add extra notes or special instructions.  
   - **How to Use:** Type freely.  
   - **Required:** ❌

3. **Submitting the Form**  
   - Once all required fields are complete, click **Submit Request**.  
   - Any missing or invalid field will trigger an error message and prevent submission.

4. **Closing the Modal**  
   - Click the **Close (X)** button in the top-right to cancel the request without saving.

---

📊 **Key Features**  
- **Dynamic Filtering:** Inventory options adjust based on selected Work Order.  
- **Auto-Populated Fields:** Inventory Description fills in automatically.  
- **Real-Time Validation:** Immediate feedback for incomplete or incorrect fields.  
- **Predefined Options:** Dropdowns for common fields simplify the process.

---

⚙️ **Troubleshooting**

- **Missing Data Error:** Ensure required fields are selected or filled.  
- **Form Won’t Submit:** Double-check validation errors on screen.  
- **Modal Not Closing:** Confirm browser JavaScript is enabled.

---

🏁 **Final Notes**  
This form ensures a quick and accurate way to request inventory tied to specific tasks or machines. Its smart filters and helpful UI make it easy to use for both new and experienced users."
},
       new HelpTopic
{
    Name = "Move Item",
    Description = @"📘 **User Manual: Move Item Form**

🛠 **Overview**  
The Move Item form is used to transfer items from one location to another within the system. It includes fields for source and destination locations, item details, quantity, units, priority level, and comments. The modal opens automatically and validates all required fields before submission.

---

📝 **How to Use the Move Item Form**

1. **Opening the Form**  
   - This modal opens automatically on page load and is centered on the screen.
   - Closing the modal will redirect the user back to the homepage.

2. **Form Fields**

   **Source Location:**  
   - **Purpose:** Select the current location of the item.  
   - **How to Use:** Start typing or select from the dropdown.  
   - **Required:** ✅  
   - **Validation:** Must be filled or an error will appear.

   **Destination Location:**  
   - **Purpose:** Specify where the item should be moved to.  
   - **How to Use:** Start typing or select from the dropdown.  
   - **Required:** ✅  
   - **Validation:** Cannot be left blank.

   **Item Name:**  
   - **Purpose:** Choose the item you want to move.  
   - **How to Use:** Type to search or select from the list.  
   - **Required:** ✅  
   - **Features:**  
     - Item Description auto-fills based on your selection.  
     - Pressing **Enter** fetches updated info via AJAX.  
   - **Validation:** Required for submission.

   **Item Description:**  
   - **Purpose:** View the description of the selected item.  
   - **How to Use:** Auto-filled from the Item Name selection.  
   - **Required:** ✅ (read-only field)  
   - **Error Handling:** Shows message if item not found.

   **Quantity:**  
   - **Purpose:** Enter how many units to move.  
   - **How to Use:** Type a number (minimum: 1).  
   - **Required:** ✅  
   - **Validation:** Must be 1 or more.

   **Unit:**  
   - **Purpose:** Specify the measurement unit.  
   - **Options:** Piece, Case, Pallet, Kilogram  
   - **Required:** ✅

   **Priority Level:**  
   - **Purpose:** Define how urgent the item move is.  
   - **Options:** High, Medium, Low  
   - **Required:** ✅

   **Comments:**  
   - **Purpose:** Add optional notes or special instructions.  
   - **How to Use:** Free text area.  
   - **Required:** ❌

3. **Submitting the Form**  
   - Click the **Move Item** button to submit.
   - Validation errors will highlight incomplete or incorrect fields.
   - A JavaScript validation prevents submission if required fields are missing.

4. **Closing the Modal**  
   - Click the **X (Close)** button to cancel and return to the homepage.

---

📊 **Key Features**  
- **Auto-Open Modal:** Opens automatically for a smooth workflow.  
- **Auto-Fill Description:** Based on selected item, updated via JS and optional AJAX.  
- **Client-Side Validation:** Ensures correct data before submission.  
- **Dynamic DataLists:** Fields pull from server-provided lists.  
- **AJAX Item Lookup:** Extra validation if item is manually typed.

---

⚙️ **Troubleshooting**

- **Missing Source or Destination:** Ensure both are filled from the dropdown.  
- **Item Not Found:** Ensure Item Name matches list options or check spelling.  
- **Form Won’t Submit:** Required fields are likely incomplete or invalid.  
- **AJAX Error:** Check internet connection or contact system admin.

---

🏁 **Final Notes**  
The Move Item form helps users efficiently reassign items between locations while keeping track of item details and priority. Smart auto-fill and validation ensure minimal user error and accurate record-keeping."
},
       new HelpTopic
{
    Name = "Clear Waste",
    Description = @"📘 **User Manual: Clear Waste Form**

🛠 **Overview**  
The Clear Waste form allows users to request the removal of waste from a specified location. This form supports various waste types and descriptions, along with priority settings and quantity input. It opens automatically and ensures all required information is validated before submission.

---

📝 **How to Use the Clear Waste Form**

1. **Form Behavior**  
   - The modal opens **automatically** when the page loads.
   - Clicking the **X** (close) button will **redirect to the homepage**.

2. **Form Fields**

   **Waste Location:**  
   - **Purpose:** Choose where the waste is currently located.  
   - **How to Use:** Type or select from a list of locations.  
   - **Required:** ✅  
   - **Validation:** Must be selected or an error will display.

   **Waste Type:**  
   - **Purpose:** Define the category of waste being cleared.  
   - **Options Include:**  
     - General Waste, BBC, Mixed Paper, Virgin White, Hazardous Waste, Medical Waste, Organic Waste, Plastic Waste, Electronic Waste, Recyclable Waste, Non-Recyclable Waste, Construction Waste  
   - **Required:** ✅  
   - **Validation:** Must be selected from dropdown.

   **Waste Description:**  
   - **Purpose:** Provide a specific description of the waste.  
   - **Options Include:**  
     - Paper/Cardboard, Plastic Waste, Metal Scraps, Chemical Waste, Electronic Parts, Broken Glass, Construction Debris  
   - **How to Use:** Select from the dropdown or type a matching value.  
   - **Required:** ✅  
   - **Validation:** Required for form submission.

   **Quantity:**  
   - **Purpose:** Enter the amount of waste to be cleared.  
   - **How to Use:** Input a number.  
   - **Minimum:** 1  
   - **Required:** ✅  
   - **Validation:** Must be a valid number ≥ 1.

   **Unit:**  
   - **Purpose:** Specify the measurement unit for the waste.  
   - **Options:** Piece, Case, Pallet, Kilogram  
   - **Required:** ✅

   **Priority Level:**  
   - **Purpose:** Indicate urgency for waste clearing.  
   - **Options:** High, Medium, Low  
   - **Required:** ✅

   **Comments:**  
   - **Purpose:** Add any additional notes or details for the waste clearing request.  
   - **Required:** ❌  
   - **Optional Input.**

3. **Submitting the Form**  
   - Click the **Clear Waste** button to submit.
   - The form uses **client-side validation** to ensure all required fields are filled correctly.
   - Incomplete or incorrect fields will show red warnings and prevent submission.

4. **Closing the Modal**  
   - Use the **close (X)** button to exit the form.  
   - You will be redirected to the main index page.

---

📊 **Key Features**

- **Auto-Open Modal:** Ensures quick and guided data input.  
- **Extensive Waste Type Support:** Predefined waste types for clarity.  
- **Validation Enabled:** Prevents incomplete requests from being submitted.  
- **Datalist Options:** Helps users select valid locations and waste descriptions.  
- **Responsive Design:** Adjusts to various screen sizes and centers the modal.

---

⚙️ **Troubleshooting**

- **Waste Location Not Found:** Ensure you're selecting from the suggested list or typing correctly.  
- **Quantity Error:** Must be a number greater than or equal to 1.  
- **Submit Button Not Working:** One or more required fields are likely incomplete.  
- **Redirect Unexpected:** Modal close button will return to the homepage.

---

🏁 **Final Notes**  
The Clear Waste form ensures that waste is handled efficiently and correctly by capturing essential details such as type, location, and priority. It improves operational hygiene and environmental responsibility within the facility."
},
        new HelpTopic
        {
            Name = "Managing tasks",
            Description = @"📘 **User Manual: Managing Tasks**
🛠 **Overview**
This section explains how to manage tasks from the task list and mark them as complete.

📝 **Steps to Manage Tasks**
1. **Accessing the Task List**
    - Navigate to the main dashboard and click on 'Task List'.
    - You will see all currently assigned and pending tasks.

2. **Viewing Task Details**
    - Click on a task to view detailed information such as Task Name, Assigned To, Priority, and Status.

3. **Updating Task Status**
    - To mark a task as complete, click the **'Complete'** button next to the task.
    - The task status will be updated in real-time and archived if completed.

4. **Editing or Deleting Tasks**
    - You can edit a task by clicking the **'Edit'** icon.
    - To delete a task, click the **'Delete'** icon and confirm your action.

📊 **Key Features**
- **Real-Time Updates**: Tasks are updated instantly across all user dashboards.
- **Audit Logs**: Every update is logged with timestamps and user info.
- **Easy Filtering**: Filter tasks by priority, assignee, or status.

⚙️ **Troubleshooting**
- **Task not updating?** Ensure you have the necessary permissions.
- **Missing task?** Use filters to search or refresh the task list.

🏁 **Final Notes**
Managing tasks efficiently helps keep the team productive and organized. Use the tools provided to stay on top of task progress."
        },
        new HelpTopic
        {
            Name = "Task priority levels",
            Description = @"📘 **User Manual: Task Priority Levels**
🛠 **Overview**
Priority levels help categorize tasks by urgency and importance.

📝 **Available Priority Levels**
- **High**
    - Tasks that need immediate attention or are blocking others.
- **Medium**
    - Tasks that are important but not urgent.
- **Low**
    - Tasks that are nice to complete but not time-sensitive.

📊 **Best Practices**
- Assign **High** only to blockers or mission-critical issues.
- Use **Medium** for tasks that need doing within the workday or week.
- **Low** should be used for tasks with no immediate deadline.

⚙️ **Troubleshooting**
- If you are unsure of what level to pick, consult with your team lead or supervisor.

🏁 **Final Notes**
Correct priority settings ensure the right focus and resource allocation across teams."
        },
        new HelpTopic
        {
            Name = "System settings",
            Description = @"📘 **User Manual: System Settings**
🛠 **Overview**
System Settings allow administrators to configure platform preferences.

📝 **What You Can Configure**
1. **User Permissions**
    - Set roles and permissions for each user.

2. **Notification Preferences**
    - Enable or disable alerts for specific actions.

3. **Task Settings**
    - Default priority levels, auto-assignment rules, etc.

4. **Interface Customization**
    - Theme selection, label language, date/time formats.

📊 **Key Features**
- Centralized control panel for easy management.
- Role-based access ensures secure configurations.
- Customizable to suit different workflows.

⚙️ **Troubleshooting**
- **Changes not saving?** Check your admin permissions.
- **Interface not updating?** Clear browser cache or refresh.

🏁 **Final Notes**
Keep your system configured correctly for optimal performance and a seamless user experience."
        }
    };

            foreach (var topic in HelpTopics)
            {
                topic.Description = Markdown.ToHtml(topic.Description);
            }
        }

    }

    public class HelpTopic
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
