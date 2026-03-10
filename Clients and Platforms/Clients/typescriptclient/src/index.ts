import * as signalR from "@microsoft/signalr";

// Adjust URL to your hub endpoint
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5276/hubs/crossplatform")
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Information)
    .build();

// Listen for messages from the hub
connection.on("ReceiveMessage", (message: string) => {
    console.log(message);
});

// Start the connection
async function start() {
    try {
        await connection.start();
        console.log("✅ Connected to SignalR hub");
    } catch (err) {
        console.error("❌ Connection error:", err);
        setTimeout(start, 5000);
    }
}

connection.onclose(start);
start();

// Read input from the console
import * as readline from "readline";

const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout
});

function prompt() {
    rl.question("> ", async (input) => {
        if (input === "exit") {
            await connection.stop();
            rl.close();
            return;
        }
        await connection.invoke("ProcessCommand", input);
        prompt();
    });
}

prompt();
