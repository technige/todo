package tech.nige;

import co.elastic.clients.base.ApiException;
import co.elastic.clients.base.ElasticsearchError;
import co.elastic.clients.base.RestClientTransport;
import co.elastic.clients.base.Transport;
import co.elastic.clients.elasticsearch.ElasticsearchClient;
import co.elastic.clients.elasticsearch._core.SearchResponse;
import co.elastic.clients.elasticsearch._core.search.Hit;
import co.elastic.clients.json.jackson.JacksonJsonpMapper;
import jakarta.json.Json;
import jakarta.json.JsonString;
import org.apache.http.HttpHost;
import org.apache.http.auth.AuthScope;
import org.apache.http.auth.UsernamePasswordCredentials;
import org.apache.http.client.CredentialsProvider;
import org.apache.http.impl.client.BasicCredentialsProvider;
import org.elasticsearch.client.RestClient;

import java.io.IOException;

public class ToDo {

    public static final String USER = "elastic";
    public static final String PASSWORD = "RUzfgAgXP2Me19HwFjdw";
    public static final String HOST = "localhost";
    public static final int PORT = 9200;

    public static void main(String[] args) {
        ToDo todo = new ToDo();

        // Attempt to parse the first command line argument,
        // used as a subcommand. If this isn't available, we
        // simply output the usage text and exit.
        String command;
        try {
            command = args[0];
        }
        catch (IndexOutOfBoundsException ex) {
            System.out.println("usage:");
            System.out.println("  todo list [TERM]   list items, optionally matching a given term");
            System.out.println("  todo add ITEM      add an item to the list");
            System.out.println("  todo check TERM    check items that match a given term");
            System.out.println("  todo clear         clear all items");
            System.exit(0);
            return;
        }

        int exitStatus;
        if ("list".equals(command)) {
            exitStatus = todo.listItems(args.length >= 2 ? args[1] : "");
        }
        else if ("add".equals(command)) {
            if (args.length < 2) {
                System.out.println("usage: todo add ITEM");
                exitStatus = 0;
            }
            else {
                exitStatus = todo.addItem(args[1]);
            }
        }
        else if ("check".equals(command)) {
            if (args.length < 2) {
                System.out.println("usage: todo check TERM");
                exitStatus = 0;
            }
            else {
                exitStatus = todo.checkItems(args[1]);
            }
        }
        else {
            System.out.println("Unknown command");
            exitStatus = 1;
        }
        System.exit(exitStatus);

    }

    public ToDo() {
        final CredentialsProvider credentialsProvider = new BasicCredentialsProvider();
        credentialsProvider.setCredentials(AuthScope.ANY,
                new UsernamePasswordCredentials(USER, PASSWORD));

        // Create the low-level client
        RestClient restClient = RestClient.builder(new HttpHost(HOST, PORT))
                .setHttpClientConfigCallback(httpClientBuilder -> httpClientBuilder
                        .setDefaultCredentialsProvider(credentialsProvider)).build();

        // Create the transport that provides JSON and http services to API clients
        Transport transport = new RestClientTransport(restClient, new JacksonJsonpMapper());

        client = new ElasticsearchClient(transport);
    }

    /**
     * Display the list of items, filtering by the <c>term</c>
     * provided. If this is an empty string, then all items will
     * be displayed.
     */
    int listItems(String term) {
        SearchResponse<Item> search;
        try {
            search = client.search(
                    s -> s.index("todo").query(q -> q.term(f -> f.field("text").value(term))),
                    Item.class
            );
        } catch (ApiException e) {
            dumpError(e);
            return 1;
        } catch(IOException e) {
            e.printStackTrace();
            return 1;
        }
        for (Hit<Item> hit : search.hits().hits()) {
            System.out.println(hit.source());
        }
        return 0;
    }

    int addItem(String text) {
        Item item = new Item(text);
        try {
            client.index(b -> b.index("todo").document(item));
        } catch (ApiException e) {
            dumpError(e);
            return 1;
        } catch (IOException e) {
            e.printStackTrace();
            return 1;
        }
        return 0;
    }

    int checkItems(String term) {
        JsonString updateScript = Json.createValue("ctx._source.done = true");
        try {
            client.updateByQuery(s -> s.index("todo").query(q -> q.term(f -> f.field("text").value(term))
                    .script(b -> b.script(updateScript))));
        } catch (ApiException e) {
            dumpError(e);
            return 1;
        } catch (IOException e) {
            e.printStackTrace();
            return 1;
        }
        return 0;
    }

    void dumpError(ApiException e) {
        if (e.error() instanceof ElasticsearchError) {
            ElasticsearchError error = (ElasticsearchError) e.error();
            System.err.println("Error " + error.status());
            error.error().asJsonObject().forEach(
                    (key, value) -> System.err.println("  " + key + ": " + value));
        }
        else {
            e.printStackTrace();
        }
    }

    ElasticsearchClient client;

    public static class Item {

        public Item() {
            this("");
        }

        public Item(String text) {
            this.text = text;
        }

        public String getText() {
            return text;
        }

        public void setText(String text) {
            this.text = text;
        }

        public boolean isDone() {
            return done;
        }

        public void setDone(boolean done) {
            this.done = done;
        }

        @Override
        public String toString() {
            return String.format("[%s] %s", done ? "X" : " ", text);
        }

        private String text;

        private boolean done;

    }
}