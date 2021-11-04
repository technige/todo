using System;
using System.Threading.Tasks;
using Nest;

class ToDo
{
    
    static int Main(string[] args)
    {
        var todo = new ToDo();

        string command;
        try
        {
            command = args[0];
        }
        catch (System.IndexOutOfRangeException)
        {
            Console.WriteLine("usage:");
            Console.WriteLine("  todo list [TERM]   list items, optionally matching a given term");
            Console.WriteLine("  todo add ITEM      add an item to the list");
            Console.WriteLine("  todo check TERM    check items that match a given term");
            Console.WriteLine("  todo clear         clear all items");
            return 0;
        }

        Task<int> task;
        switch (command)
        {
            case "list":
                task = todo.ListItemsAsync(args.Length > 1 ? args[1] : "");
                break;
            
            case "add":
                task = todo.AddItemAsync(args[1]);
                break;

            case "check":
                task = todo.CheckItemsAsync(args[1]);
                break;

            case "clear":
                task = todo.ClearItemsAsync();
                break;

            default:
                Console.WriteLine("Unknown command");
                return 1;

        }

        return task.GetAwaiter().GetResult();
    }

    ToDo()
    {
        var settings = new ConnectionSettings()
            .DefaultMappingFor<Item>(m => m.IndexName("todo"));
        Client = new ElasticClient(settings);
        
    }

    async Task<int> ListItemsAsync(string term)
    {
        var response = await Client.SearchAsync<Item>(s => s
            .Query(q => q
                .Term(f => f.Text, term)
            )
        );
        foreach (var data in response.Documents) {
            Console.WriteLine(data);
        }
        return 0;
    }

    async Task<int> AddItemAsync(string item)
    {
        var response = await Client.IndexDocumentAsync(new Item(item));
        return response.IsValid ? 0 : 1;
    }

    async Task<int> CheckItemsAsync(string term)
    {
        var response = await Client.UpdateByQueryAsync<Item>(s => s
            .Query(q => q
                .Term(f => f.Text, term)
            )
            .Script("ctx._source.done = true")
        );
        return response.IsValid ? 0 : 1;
    }

    async Task<int> ClearItemsAsync()
    {
        var response = await Client.DeleteByQueryAsync<Item>(s => s
            .Query(q => q
                .QueryString(qs=>qs.Query("*"))
            )
        );
        return response.IsValid ? 0 : 1;
    }

    ElasticClient Client;

}

class Item
{
    public Item(String text) {
        Text = text;
        Done = false;
    }

    public override string ToString()
    {
        return $"[{(Done ? "X" : " ")}] {Text}";
    }

    public string Text;
    public bool Done;
}