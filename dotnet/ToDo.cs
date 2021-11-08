using System;
using System.Threading.Tasks;
using Nest;

/**
 * Simple TODO list management application for the CLI.
 *
 * The primary purpose of this class is to illustrate basic usage of the
 * high-level .NET client for Elasticsearch, NEST.
 *
 * This class holds the application logic, plus the Main method used as
 * an entry point.
 */
class ToDo
{
    /**
     * Application entry point.
     */
    static int Main(string[] args)
    {
        var todo = new ToDo();

        // Attempt to parse the first command line argument,
        // used as a subcommand. If this isn't available, we
        // simply output the usage text and exit.
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

        // Now, dispatch the appropriate task for the command
        // selected.
        Task<int> task;
        switch (command)
        {
            // Output the list items, optionally filtered by a search term
            case "list":
                task = todo.ListItemsAsync(args.Length >= 2 ? args[1] : "");
                break;
            
            // Add an item to the list
            case "add":
                if (args.Length < 2) {
                    Console.WriteLine("usage: todo add ITEM");
                    return 0;
                }
                task = todo.AddItemAsync(args[1]);
                break;

            // Check off one or more items on the list
            case "check":
                if (args.Length < 2) {
                    Console.WriteLine("usage: todo check TERM");
                    return 0;
                }
                task = todo.CheckItemsAsync(args[1]);
                break;

            // Clear all items from the list
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
        // Create settings for a connection to Elasticsearch.
        //
        // As the server is not explicitly specified, we are
        // expecting an instance to be running on localhost over
        // the default port, 9200.
        // 
        // We also explicitly set up a mapping between the Item
        // class and the "todo" index, such that all usage of
        // that class is channelled into that index.
        //
        // For more details on connection settings, see:
        //   https://www.elastic.co/guide/en/elasticsearch/client/net-api/current/configuration-options.html#_connectionsettings_with_elasticclient
        //
        var settings = new ConnectionSettings()
            .DefaultMappingFor<Item>(m => m.IndexName("todo"));

        // Create a client instance using the settings above.
        // This acts a a wrapper for all API calls, and will
        // be safely garbage collected when it goes out of scope.
        //
        Client = new ElasticClient(settings);
        
    }

    /**
     * Display the list of items, filtering by the <c>term</c>
     * provided. If this is an empty string, then all items will
     * be displayed.
     */
    async Task<int> ListItemsAsync(string term)
    {
        // The SearchAsync method of the client is used here to
        // carry out a database search. The "Item" parameter
        // specifies that we are looking for Item instances in
        // our search result, and also that we should use the
        // "todo" index, as specified in the ConnectionSettings
        // mapping, above.
        //
        var response = await Client.SearchAsync<Item>(s => s
            .Query(q => q                       // our search is based on a query that
                .Term(f => f.Text, term)        // matches "term" to the "text" field
            )
        );

        // Loop through and output all documents in the response.
        // As specified in the SearchAsync call, each of these will
        // be an Item instance.
        //
        foreach (Item doc in response.Documents) {
            Console.WriteLine(doc);
        }
        return 0;
    }

    /**
     * Add an item to the list, using the text provided.
     */
    async Task<int> AddItemAsync(string text)
    {
        // The IndexDocumentAsync method takes the Item instance
        // and passes it into the database for creation.
        //
        Item item = new Item(text);
        var response = await Client.IndexDocumentAsync(item);
        return response.IsValid ? 0 : 1;
    }

    /**
     * Update one or more items in the list, setting the "done"
     * flag to true. Items are selected based on the provided
     * search term, such that all items matching that term will
     * be updated.
     */
    async Task<int> CheckItemsAsync(string term)
    {
        // The UpdateByQueryAsync call allows us to run an update
        // script on all documents that match a particular query.
        // Further details of the "Painless" scripting languages
        // can be found here:
        //   https://www.elastic.co/guide/en/elasticsearch/reference/current/modules-scripting-painless.html
        //
        var response = await Client.UpdateByQueryAsync<Item>(s => s
            .Query(q => q                       // our search is based on a query that
                .Term(f => f.Text, term)        // matches "term" to the "text" field
            )
            .Script("ctx._source.done = true")  // update the matched documents
        );
        return response.IsValid ? 0 : 1;
    }

    /**
     * Clear all items from the list.
     */
    async Task<int> ClearItemsAsync()
    {
        // The DeleteByQueryAsync method is similar to its "Update"
        // sibling (above) in that we can select documents from the
        // index for deletion.
        //
        var response = await Client.DeleteByQueryAsync<Item>(s => s
            .QueryOnQueryString("*")            // match all documents using the query string "*"
        );
        return response.IsValid ? 0 : 1;
    }

    // Somewhere to store the ElasticClient instance
    ElasticClient Client;

}

/**
 * The <c>Item</c> class models an entry in the TODO list.
 * This is used for all CRUD operations (above) and is
 * automatically serialised and deserialise as required.
 * All public fields are serialised into the database,
 * whereas private fields are not.
 */
class Item
{
    public Item(String text) {
        Text = text;
        Done = false;
    }

    /**
     * Textual representation used for list item output.
     */
    public override string ToString()
    {
        return $"[{(Done ? "X" : " ")}] {Text}";
    }

    /// The text of the item
    public string Text;
    
    /// Flag to indicate whether or not the item has been done 
    public bool Done;
}