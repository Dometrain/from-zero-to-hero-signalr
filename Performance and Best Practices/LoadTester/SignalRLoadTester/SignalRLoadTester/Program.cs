using Microsoft.AspNetCore.SignalR.Client;
using NBomber.CSharp;

namespace SignalRLoadTester;

class Program
{
    static void Main(string[] args)
    {
        var scenario = Scenario.Create("SignalRLoadTester", async context =>
            {
                var connection = new HubConnectionBuilder()
                    .WithUrl("https://localhost:7044/hubs/chat")
                    .Build();

                await connection.StartAsync();

                for (var i = 0; i < 4; i++)
                {
                    await connection.InvokeAsync("SendMessage", "Hello");
                    await Task.Delay(5000);
                }

                await connection.StopAsync();
                return Response.Ok();

            })
            .WithLoadSimulations(Simulation.RampingInject(20,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(10)));

        NBomberRunner.RegisterScenarios(scenario).Run();
    }
}