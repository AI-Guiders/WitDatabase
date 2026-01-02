using OutWit.Database.Samples.ConsoleApp.Examples;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("============================================================");
Console.WriteLine("|         WitDatabase Console Sample Application           |");
Console.WriteLine("|              Pure .NET Embedded Database                  |");
Console.WriteLine("============================================================");
Console.WriteLine();

var examples = new (string Name, Func<Task> Run)[]
{
    ("Basic CRUD Operations", BasicCrudExample.RunAsync),
    ("Transactions & Savepoints", TransactionExample.RunAsync),
    ("Encryption Demo", EncryptionExample.RunAsync),
    ("LSM-Tree Storage", LsmTreeExample.RunAsync),
    ("Bulk Operations", BulkOperationsExample.RunAsync),
};

while (true)
{
    Console.WriteLine("Select an example to run:");
    Console.WriteLine();

    for (int i = 0; i < examples.Length; i++)
    {
        Console.WriteLine($"  {i + 1}. {examples[i].Name}");
    }
    Console.WriteLine($"  0. Exit");
    Console.WriteLine();

    Console.Write("Enter choice (0-{0}): ", examples.Length);
    var input = Console.ReadLine();

    if (!int.TryParse(input, out var choice) || choice < 0 || choice > examples.Length)
    {
        Console.WriteLine("Invalid choice. Please try again.");
        Console.WriteLine();
        continue;
    }

    if (choice == 0)
    {
        Console.WriteLine("Goodbye!");
        break;
    }

    Console.WriteLine();
    Console.WriteLine(new string('=', 60));
    
    try
    {
        await examples[choice - 1].Run();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
    }

    Console.WriteLine(new string('=', 60));
    Console.WriteLine();
    Console.WriteLine("Press any key to continue...");
    Console.ReadKey(intercept: true);
    Console.WriteLine();
}
