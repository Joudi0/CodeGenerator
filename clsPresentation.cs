using System;
using System.Collections.Generic;

namespace CodeGenarator
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
    }
}