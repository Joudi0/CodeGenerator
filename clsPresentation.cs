using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenarator
{
    internal class clsPresentation
    {
        // SPs Functions

        public static List<string> getBySP()
        {
            List<string> Columns = new List<string>();
            string answer = "yes";
            do
            {
                Console.Write("Enter the column name to generate 'SELECT By' Procedure for it: ");
                string columnName = Console.ReadLine();

                if (clsHelper.getColumnIndex(columnName) < 0)
                {
                    Console.WriteLine("Column not Found.\n");
                }
                else
                {

                    Columns.Add(columnName);

                }
                Console.Write("Do you want to add another 'SELECT By' Procedure? yes/no: ");
                answer = Console.ReadLine();

            } while (answer.ToLower() == "yes" || answer.ToLower() == "y");
            return Columns;
        }

        public static List<string> existBySP()
        {
            List<string> Columns = new List<string>();

            string answer = "yes";
            do
            {
                Console.Write("Enter the column name to generate 'isExist By' Procedure for it: ");
                string columnName = Console.ReadLine();

                if (clsHelper.getColumnIndex(columnName) < 0)
                {
                    Console.WriteLine("Column not Found.\n");
                }
                else
                {
                    Columns.Add(columnName);

                }
                Console.WriteLine("Do you want to add another 'isExist By' Procedure? yes/no");
                answer = Console.ReadLine();

            } while (answer.ToLower() == "yes" || answer.ToLower() == "y");
            return Columns;
        }

        public static List<string> getAllBySP()
        {
            List<string> Columns = new List<string>();

            string answer = "yes";
            do
            {
                Console.Write("Enter the column name to generate 'getAll By' Procedure for it: ");
                string columnName = Console.ReadLine();

                if (clsHelper.getColumnIndex(columnName) < 0)
                {
                    Console.WriteLine("Column not Found.\n");
                }
                else
                {
                    Columns.Add(columnName);

                }
                Console.WriteLine("Do you want to add another 'getAll By' Procedure? yes/no: ");
                answer = Console.ReadLine();

            } while (answer.ToLower() == "yes" || answer.ToLower() == "y");
            return Columns;
        }


        // DAL Functions
        public static List<string> getBy()
        {
            List<string> Columns = new List<string>();
            string answer = "yes";
            do
            {
                Console.Write("Enter the column name to generate 'Get By' method for it: ");
                string columnName = Console.ReadLine();

                if (clsHelper.getColumnIndex(columnName) < 0)
                {
                    Console.WriteLine("Column not Found.\n");
                }
                else
                {

                    Columns.Add(columnName);

                }
                Console.Write("Do you want to add another 'Get By' method? (yes/no): ");
                answer = Console.ReadLine();

            } while (answer.ToLower() == "yes" || answer.ToLower() == "y");
            return Columns;
        }

        public static List<string> existBy()
        {
            List<string> Columns = new List<string>();

            string answer = "yes";
            do
            {
                Console.Write("Enter the column name to generate 'isExist By' method for it: ");
                string columnName = Console.ReadLine();

                if (clsHelper.getColumnIndex(columnName) < 0)
                {
                    Console.WriteLine("Column not Found.\n");
                }
                else
                {
                    Columns.Add(columnName);

                }
                Console.Write("Do you want to add another 'isExist By' method? (yes/no): ");
                answer = Console.ReadLine();

            } while (answer.ToLower() == "yes" || answer.ToLower() == "y");
            return Columns;
        }

        public static List<string> getAllBy()
        {
            List<string> Columns = new List<string>();

            string answer = "yes";
            do
            {
                Console.Write("Enter the column name to generate 'getAll By' method for it: ");
                string columnName = Console.ReadLine();

                if (clsHelper.getColumnIndex(columnName) < 0)
                {
                    Console.WriteLine("Column not Found.\n");
                }
                else
                {
                    Columns.Add(columnName);

                }
                Console.Write("Do you want to add another 'getAll By' method? (yes/no): ");
                answer = Console.ReadLine();

            } while (answer.ToLower() == "yes" || answer.ToLower() == "y");
            return Columns;
        }

        // BLL Functions

        public static string initiateBLL()
        {
            string answer = "yes";
            string BLLFuncs = "";
            Console.Write("For BLL: Generate Properties? (yes/no): ");
            answer = Console.ReadLine();
            if (answer.ToLower() == "yes" || answer.ToLower() == "y")
            {
                BLLFuncs += clsBLL.writeProperties();
            }

            Console.Write("For BLL: Generate Empty Constructor? (yes/no): ");
            answer = Console.ReadLine();
            if (answer.ToLower() == "yes" || answer.ToLower() == "y")
            {
                BLLFuncs += clsBLL.writeConstructors(true);
            }
            Console.Write("For BLL: Generate Full Constructor? (yes/no): ");
            answer = Console.ReadLine();
            if (answer.ToLower() == "yes" || answer.ToLower() == "y")
            {
                BLLFuncs += clsBLL.writeConstructors(false);
            }

            return BLLFuncs;
        }
    }
}
