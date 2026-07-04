using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static CodeGenerator.clsHelper;

namespace CodeGenerator
{
    internal class Program
    {
        private static string _projectDirectory = ConfigurationManager.AppSettings["projectDirectory"];

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            AnsiConsole.Write(
                                new FigletText("ADO Gen Code")
                                    .Centered()
                                    .Color(Color.Green));

            AnsiConsole.Write(new Rule("[yellow]Welcome in Joudi's Code Generator[/]").Justify(Justify.Left));
            AnsiConsole.MarkupLine("[grey]This tool generates DAL and BLL CRUD ADO.NET for you.[/]");
            AnsiConsole.MarkupLine("[red]Notice:[/] Please ensure database settings are configured in [cyan]clsHelper.connectionString[/].\n");

            Console.Write("First Time? (y/n): ");
            string answer = Console.ReadLine();
            if (answer.ToLower() == "yes" || answer.ToLower() == "y")
            {
                Console.Write("solution Name: ");
                string sn = Console.ReadLine();

                clsHelper.GenerateArchitectureSolution(_projectDirectory, sn);
            }

            // Fetching all tables from the database using clsHelper.GetAllTables()
            List<string> databaseTables = clsHelper.GetAllTables();
            clsHelper.LoadAvailableRoles();
            AnsiConsole.MarkupLine($"[cyan]Found {databaseTables.Count} tables in the database.[/]\n");

            foreach (string tName in databaseTables)
            {
                if (AnsiConsole.Confirm($"Do you want to generate code for table [green]{tName}[/]?"))
                {
                    await Run(tName);
                    Console.Clear();
                    AnsiConsole.Write(new FigletText("ADO Gen Code").Centered().Color(Color.Green));
                }
            }

            // Finished generating code for all tables
            AnsiConsole.WriteLine();
            Panel signaturePanel = new Panel(
                new Markup(
                    $"[bold gold1]🚀 Total Code Generated:[/] [bold green]{TotalLinesGenerated:N0} lines of clean code![/]\n\n" +
                    "[bold white]Developed with ❤️ by:[/] [bold green]Joudi[/]\n" +
                    "[bold white]Telegram:[/] [blue]@Joudi_Adeeb[/]\n" +
                    "[bold white]LinkedIn:[/] [blue]linkedin.com/in/joudi-mohammad-002685283[/]"
                )
            )
            .Header("[yellow]Generation Completed![/]")
            .BorderColor(Color.Gold1)
            .Padding(2, 1)
            .RoundedBorder()
            .Expand();
            AnsiConsole.Write(signaturePanel);
            AnsiConsole.MarkupLine("\n[grey]Press any key to exit...[/]");
            Console.ReadKey();
        }

        static bool isUserTable = false;

        public static async Task Run(string tableName)
        {
            clsHelper.tableName = tableName;
            clsHelper.Columns = clsHelper.getColumnsNameAndType();
            int count = clsHelper.Columns.Count;

            if (count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No Columns Found for table {tableName}, skipping...[/]");
                return;
            }
            StringBuilder DALFuncs = new StringBuilder();
            StringBuilder BLLFuncs = new StringBuilder();
            StringBuilder Controller = new StringBuilder();

            isUserTable = (tableName.ToLower() == "user" || tableName.ToLower() == "users");

            if (isUserTable)
            {
                AnsiConsole.MarkupLine("[yellow]-> Detecting User Table! Generating JWT Authentication Logic...[/]");

                clsHelper.allSPs.Add(clsSPs.loginSP());
                DALFuncs.Append(clsDAL.getAuthData());
                BLLFuncs.Append(clsBLL.checkLogin());
            }

            Console.Write($"\nEnter The Class Name For {tableName} (cls First will be added on it): ");
            clsHelper.objectName = Console.ReadLine();
            clsHelper.className = "cls" + clsHelper.objectName;
            string answer = "yes";

            clsHelper.mappedColumns = clsHelper.mappingTheColumns();
            clsHelper.ColumnsForCsharp = clsHelper.getColumnsForCsharp();
            Console.Write("\nFor DAL, BLL, And Stored Procedures:\n");

            // Get By:
            List<string> getByColumns = clsPresentation.getBy();
            foreach (string colName in getByColumns)
            {
                clsHelper.Column column = clsHelper.makeColumnByName(colName);
                clsHelper.allSPs.Add(clsSPs.selectByColumnSP(column));
                DALFuncs.Append(clsDAL.getRecordByColumnFunc(column));
                BLLFuncs.Append(clsBLL.getByFunc(column));
                BLLFuncs.Append(clsBLL.getBriefFunc(column));

                // Prompt console for allowed roles for this GetBy action and pass them to the Controller
                string getByRoles = clsPresentation.PromptForActionRoles($"GetBy{colName}");
                Controller.Append(clsAPIs.getByAction(column, getByRoles));
            }

            // Update:
            Console.Write("update? yes/no: ");
            answer = Console.ReadLine();
            if (answer.ToLower() == "yes" || answer.ToLower() == "y")
            {
                clsHelper.allSPs.Add(clsSPs.updateSP());
                DALFuncs.Append(clsDAL.updateFunc());
                BLLFuncs.Append(clsBLL.updateFunc());

                // Prompt console for Update action roles
                string updateRoles = clsPresentation.PromptForActionRoles("Update");
                Controller.Append(clsAPIs.updateAction(updateRoles));
            }

            // Delete:
            Console.Write("delete? yes/no: ");
            answer = Console.ReadLine();
            if (answer.ToLower() == "yes" || answer.ToLower() == "y")
            {
                clsHelper.Column C = clsHelper.mappedColumns[0];
                clsHelper.allSPs.Add(clsSPs.deleteSP());
                DALFuncs.Append(clsDAL.deleteFunc(C));
                BLLFuncs.Append(clsBLL.deleteFunc(C));

                // Prompt console for Delete action roles
                string deleteRoles = clsPresentation.PromptForActionRoles("Delete");
                Controller.Append(clsAPIs.deleteAction(C, deleteRoles));
            }

            // Add:
            Console.Write("add? yes/no: ");
            answer = Console.ReadLine();
            if (answer.ToLower() == "yes" || answer.ToLower() == "y")
            {
                clsHelper.allSPs.Add(clsSPs.addSP());
                DALFuncs.Append(clsDAL.addFunc());
                BLLFuncs.Append(clsBLL.addFunc());

                // Prompt console for Add action roles
                string addRoles = clsPresentation.PromptForActionRoles("Add");
                Controller.Append(clsAPIs.addAction(addRoles));
            }

            // isExist:
            Console.Write("isExist? yes/no: ");
            answer = Console.ReadLine();
            if (answer.ToLower() == "yes" || answer.ToLower() == "y")
            {
                List<string> Columns = clsPresentation.existBy();
                foreach (string colName in Columns)
                {
                    clsHelper.Column column = clsHelper.makeColumnByName(colName);

                    clsHelper.allSPs.Add(clsSPs.isExistByColumnSP(column));
                    DALFuncs.Append(clsDAL.isExistsFunc(column));
                    BLLFuncs.Append(clsBLL.isExistsFunc(column));

                    // Prompt console for IsExist action roles
                    string existRoles = clsPresentation.PromptForActionRoles($"ExistsBy{colName}");
                    Controller.Append(clsAPIs.isExistAction(column, existRoles));
                }
            }

            // Paging:
            Console.Write("Paging? yes/no: ");
            answer = Console.ReadLine();
            if (answer.ToLower() == "yes" || answer.ToLower() == "y")
            {
                clsHelper.allSPs.Add(clsSPs.PagingSP());
                DALFuncs.Append(clsDAL.PagingFunc());
                BLLFuncs.Append(clsBLL.PagingFunc());

                // Prompt console for Paging action roles
                string pagingRoles = clsPresentation.PromptForActionRoles("GetPage (Paging)");
                Controller.Append(clsAPIs.pagingAction(pagingRoles));
            }

            // getAll:
            Console.Write("getAll? yes/no: ");
            answer = Console.ReadLine();
            if (answer.ToLower() == "yes" || answer.ToLower() == "y")
            {
                clsHelper.Column firstColumn = clsHelper.ColumnsForCsharp[0];
                DALFuncs.Append(clsDAL.getAllFunc());

                BLLFuncs.Append(clsBLL.getAllBriefByFunc(firstColumn));
                BLLFuncs.Append(clsBLL.getAllFullByFunc(firstColumn));

                // Prompt console for base GetAll action roles
                string getAllRoles = clsPresentation.PromptForActionRoles("GetAll");
                Controller.Append(clsAPIs.getAllBriefByAction(firstColumn, getAllRoles));
                Controller.Append(clsAPIs.getAllFullByAction(firstColumn, getAllRoles));

                clsHelper.allSPs.Add(clsSPs.selectAllSP());
                Console.Write("GetAll Method Generated, do you want 'GetAll By' ? (yes/no): ");
                answer = Console.ReadLine();
                if (answer.ToLower() == "yes" || answer.ToLower() == "y")
                {
                    List<string> Columns = clsPresentation.getAllBy();
                    foreach (string colName in Columns)
                    {
                        clsHelper.Column column = clsHelper.makeColumnByName(colName);
                        clsHelper.allSPs.Add(clsSPs.selectAllBySP(column));
                        DALFuncs.Append(clsDAL.getAllByColumnFunc(column));

                        BLLFuncs.Append(clsBLL.getAllBriefByFunc(column));
                        BLLFuncs.Append(clsBLL.getAllFullByFunc(column));

                        // Prompt console for GetAllBy roles for each selected column
                        string getAllByRoles = clsPresentation.PromptForActionRoles($"GetAllBy{colName}");
                        Controller.Append(clsAPIs.getAllBriefByAction(column, getAllByRoles));
                        Controller.Append(clsAPIs.getAllFullByAction(column, getAllByRoles));
                    }
                }
            }

            await saveFilesAsync(DALFuncs, BLLFuncs, Controller);
        }

        public static async Task saveFilesAsync(StringBuilder DALFuncs, StringBuilder BLLFuncs, StringBuilder Controller)
        {
            try
            {
                if (!Directory.Exists(_projectDirectory))
                {
                    Directory.CreateDirectory(_projectDirectory);
                }

                // Check for Folders
                string dal = Path.Combine(_projectDirectory, "DAL");
                string bll = Path.Combine(_projectDirectory, "BLL");
                string dto = Path.Combine(_projectDirectory, "Shared", "DTOs");
                string briefDto = Path.Combine(dto, "Brief");
                string fullDto = Path.Combine(dto, "Full");
                string controllersFolder = Path.Combine(_projectDirectory, "WebAPI", "Controllers");

                if (!Directory.Exists(dal)) Directory.CreateDirectory(dal);
                if (!Directory.Exists(bll)) Directory.CreateDirectory(bll);
                if (!Directory.Exists(dto)) Directory.CreateDirectory(dto);
                if (!Directory.Exists(briefDto)) Directory.CreateDirectory(briefDto);
                if (!Directory.Exists(fullDto)) Directory.CreateDirectory(fullDto);

                if (isUserTable)
                {
                    // Call the Auth logic now relocated inside clsHelper
                    await clsHelper.Auth();
                }
                if (!Directory.Exists(controllersFolder)) Directory.CreateDirectory(controllersFolder);

                // Exact file paths
                string dalPath = Path.Combine(dal, $"{clsHelper.className}DAL.cs");
                string bllPath = Path.Combine(bll, $"{clsHelper.className}.cs");
                string BriefDTOPath = Path.Combine(briefDto, $"{clsHelper.className}BriefDTO.cs");
                string FullDTOPath = Path.Combine(fullDto, $"{clsHelper.className}FullDTO.cs");
                string controllerPath = Path.Combine(controllersFolder, $"{clsHelper.objectName}Controller.cs");

                // Save files
                Console.Write("-> Making Stored Procedures... ");
                clsHelper.InjectAllToDB();
                Console.WriteLine("[Done]");

                Console.Write("-> Making DAL Class... ");
                string dalCode = clsDAL.classStructure(DALFuncs);
                using (StreamWriter writer = new StreamWriter(dalPath))
                {
                    await writer.WriteAsync(dalCode);
                }
                TrackLines(dalCode); // Catch and count!
                Console.WriteLine("[Done]");

                Console.Write("-> Making BLL Class... ");
                string bllCode = clsBLL.classStructure(BLLFuncs);
                using (StreamWriter writer = new StreamWriter(bllPath))
                {
                    await writer.WriteAsync(bllCode);
                }
                TrackLines(bllCode); // Catch and count!
                Console.WriteLine("[Done]");

                Console.Write("-> Making DTO Class... ");
                string BriefDTOCode = clsAPIs.BriefDTO();
                using (StreamWriter writer = new StreamWriter(BriefDTOPath))
                {
                    await writer.WriteAsync(BriefDTOCode);
                }

                string FullDTOCode = clsAPIs.FullDTO();
                using (StreamWriter writer = new StreamWriter(FullDTOPath))
                {
                    await writer.WriteAsync(FullDTOCode);
                }

                TrackLines(BriefDTOCode); // Catch and count!
                TrackLines(FullDTOCode); // Catch and count!
                Console.WriteLine("[Done]");

                Console.Write("-> Making Web API Controller... ");
                string controllerCode = clsAPIs.controllerStructure(Controller);
                using (StreamWriter writer = new StreamWriter(controllerPath))
                {
                    await writer.WriteAsync(controllerCode);
                }
                TrackLines(controllerCode); // Catch and count!
                Console.WriteLine("[Done]");

                AnsiConsole.MarkupLine($"\n[green]Success:[/] Files generated and saved successfully!");
                AnsiConsole.MarkupLine($"[grey]Stored Procedures:[/] [bold green]Injected Directly into DB![/]");
                AnsiConsole.MarkupLine($"[grey]DAL Class:[/] [cyan]{dalPath}[/]");
                AnsiConsole.MarkupLine($"[grey]BLL Class:[/] [cyan]{bllPath}[/]");
                AnsiConsole.MarkupLine($"[grey]Web API Controller:[/] [cyan]{controllerPath}[/]");
                AnsiConsole.MarkupLine($"[grey]DTO Class:[/] [cyan]{dto}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"\n[red]Error while saving files:[/] {ex.Message}");
            }
        }
    }
}