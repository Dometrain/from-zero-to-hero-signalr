package org.example;
import com.microsoft.signalr.HubConnection;
import com.microsoft.signalr.HubConnectionBuilder;
import java.util.Scanner;
//TIP To <b>Run</b> code, press <shortcut actionId="Run"/> or
// click the <icon src="AllIcons.Actions.Execute"/> icon in the gutter.
public class Main {
    static void main() {
        String hubUrl = "http://localhost:5276/hubs/crossplatform"; // your hub URL - you'll need to trust the SSL cert or use http for testing

        HubConnection hubConnection = HubConnectionBuilder.create(hubUrl).build();

        // Listen for server-pushed messages
        hubConnection.on("ReceiveMessage", (message) -> {
            System.out.println("Server says: " + message);
        }, String.class);

        hubConnection.start().blockingAwait();
        System.out.println("Connected to SignalR hub!");

        Scanner scanner = new Scanner(System.in);
        while (true) {
            System.out.print("Enter command: ");
            String command = scanner.nextLine();
            if (command.isEmpty()) break;

            // Send command to hub
            hubConnection.send("ProcessCommand", command);
        }

        hubConnection.stop().blockingAwait();
        System.out.println("Disconnected.");
    }

}
