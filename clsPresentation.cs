using System;
using System.Collections.Generic;

namespace CodeGenerator
{
    internal class clsPresentation
    {
        public static List<string> getBy()
        {
            List<string> columns = new List<string>();
            string answer = "yes";
            do
            {
                Console.Write("Enter the column name to generate 'Get By' method and SP for it: ");
                string columnName = Console.ReadLine();

                if (clsHelper.getColumnIndex(columnName) < 0)
                {
                    Console.WriteLine("Column not Found.\n");
                }
                else
                {
                    columns.Add(columnName);
                }
                Console.Write("Do you want to add another 'Get By' method? (yes/no): ");
                answer = Console.ReadLine();

            } while (answer.ToLower() == "yes" || answer.ToLower() == "y");
            return columns;
        }

        public static List<string> existBy()
        {
            List<string> columns = new List<string>();
            string answer = "yes";
            do
            {
                Console.Write("Enter the column name to generate 'isExist By' method and SP for it: ");
                string columnName = Console.ReadLine();

                if (clsHelper.getColumnIndex(columnName) < 0)
                {
                    Console.WriteLine("Column not Found.\n");
                }
                else
                {
                    columns.Add(columnName);
                }
                Console.Write("Do you want to add another 'isExist By' method? (yes/no): ");
                answer = Console.ReadLine();

            } while (answer.ToLower() == "yes" || answer.ToLower() == "y");
            return columns;
        }

        public static List<string> getAllBy()
        {
            List<string> columns = new List<string>();
            string answer = "yes";
            do
            {
                Console.Write("Enter the column name to generate 'getAll By' method and SP for it: ");
                string columnName = Console.ReadLine();

                if (clsHelper.getColumnIndex(columnName) < 0)
                {
                    Console.WriteLine("Column not Found.\n");
                }
                else
                {
                    columns.Add(columnName);
                }
                Console.Write("Do you want to add another 'getAll By' method? (yes/no): ");
                answer = Console.ReadLine();

            } while (answer.ToLower() == "yes" || answer.ToLower() == "y");
            return columns;
        }

        public static string PromptForActionRoles(string actionName)
        {
            Console.WriteLine($"\n--- Select Roles for [{actionName}] Action ---");

            // Displaying dynamically loaded roles from the database
            for (int i = 0; i < clsHelper.AvailableRoles.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {clsHelper.AvailableRoles[i]}");
            }
            Console.WriteLine($"{clsHelper.AvailableRoles.Count + 1}. Anonymous (Public Access)");

            Console.Write("Enter choice numbers separated by commas (e.g., 1,2) or single choice: ");
            string input = Console.ReadLine();
            int choice = 0;
            int.TryParse(input, out choice);
            while (input.Length == 2 && (choice > clsHelper.AvailableRoles.Count || choice <= 0))
            {
                Console.Write("please enter choice between 1 and " + clsHelper.AvailableRoles.Count);
                input = Console.ReadLine();
                int.TryParse(input, out choice);
            }
            // Handle empty or whitespace inputs default to Anonymous or handle gracefully
            if (string.IsNullOrWhiteSpace(input)) return "Anonymous";

            // If the user selects the last option for Anonymous access
            if (input.Trim() == (clsHelper.AvailableRoles.Count + 1).ToString())
            {
                return "Anonymous";
            }

            // Parsing choices and joining selected roles into a comma-separated string
            List<string> selectedRoles = new List<string>();
            var choices = input.Split(',');
            foreach (var C in choices)
            {
                if (int.TryParse(C.Trim(), out int index) && index > 0 && index <= clsHelper.AvailableRoles.Count)
                {
                    selectedRoles.Add(clsHelper.AvailableRoles[index - 1]);
                }
            }

            // Fallback to Anonymous if no valid choices were parsed
            return selectedRoles.Count > 0 ? string.Join(",", selectedRoles) : "Anonymous";
        }
    }
}
