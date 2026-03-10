using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RealTimeDemo.Hubs;

public class StockHub : Hub
{

    private static readonly string[] Symbols = { "MSFT", "AAPL", "GOOG", "TSLA" };
    private static readonly Random Random = new();
    
    public async IAsyncEnumerable<byte[]> DownloadFile([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "nick.jpg");
        if (!File.Exists(path))
        {
            yield break;
        }
        
        var data = await File.ReadAllBytesAsync(path, cancellationToken);
        var chunkSize = 1024;

        for (int i = 0; i < data.Length; i += chunkSize)
        {
            var chunk = data.Skip(i).Take(chunkSize).ToArray();
            yield return chunk;
            await Task.Delay(100, cancellationToken);
        }
    }
    
    
    public async Task UploadBytes(ChannelReader<byte[]> stream)
    {
        await foreach (var chunk in stream.ReadAllAsync())
        {
            
            Console.WriteLine($"Received {chunk.Length} bytes");
        }
    }
    
    

    public async Task UploadNumber(ChannelReader<int> reader)
    {
        await foreach (var number in reader.ReadAllAsync())
        {
            Console.WriteLine($"Received number: {number}");
        }
    }
    
    [Authorize("PaidSubscriber")]
    public async IAsyncEnumerable<StockPrice> StreamStockPrices([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var symbol in Symbols)
            {
                yield return new StockPrice()
                {
                    Symbol = symbol,
                    Price = Math.Round(100 + Random.NextDouble() * 900, 2),
                    TimeStamp = DateTime.UtcNow
                };
            }
            
            await Task.Delay(1000, cancellationToken);
        }
    }
}

public record StockPrice
{
    public string Symbol { get; set; }
    public double Price { get; set; }
    public DateTime TimeStamp { get; set; }
}