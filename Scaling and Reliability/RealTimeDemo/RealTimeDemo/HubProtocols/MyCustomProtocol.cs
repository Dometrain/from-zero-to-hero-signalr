using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace RealTimeDemo.HubProtocols;

public class MyCustomProtocol : IHubProtocol
{
    public string Name => "MyCustomProtocol";
    public int Version => 1;
    public TransferFormat TransferFormat => TransferFormat.Text;
    
    public bool TryParseMessage(
        ref ReadOnlySequence<byte> input,
        IInvocationBinder binder,
        [NotNullWhen(true)] out HubMessage? message)
    {
        message = null;

        if (input.Length == 0)
            return false;

        var inputString = Encoding.UTF8.GetString(input.ToArray());

        var separatorIndex = inputString.IndexOf('\n');
        if (separatorIndex < 0)
            return false; // wait for more data

        var singleMessage = inputString.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Last();

        input = input.Slice(separatorIndex + 1);

        if (singleMessage == "Ping")
        {
            return false;
        }

        var parts = singleMessage.Split('|', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return false;

        if (parts[0] == "Invocation")
        {
            var target = parts[1];
            if (!int.TryParse(parts[2], out int argCount))
                return false;

            var args = new object?[argCount];
            for (int i = 0; i < argCount; i++)
                args[i] = parts[3 + i];

            message = new InvocationMessage(target, args);
            return true;
        }

        return false;
    }

    public void WriteMessage(HubMessage message, IBufferWriter<byte> output)
    {
        if (message is InvocationMessage invocation)
        {
            var sb = new StringBuilder();
            sb.Append("Invocation|");
            sb.Append(invocation.Target).Append('|');
            sb.Append(invocation.Arguments.Length);

            foreach (var arg in invocation.Arguments)
            {
                sb.Append('|').Append(arg?.ToString());
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            output.Write(bytes);
        }
    }

    public ReadOnlyMemory<byte> GetMessageBytes(HubMessage message)
    {
        var buffer = new ArrayBufferWriter<byte>();
        WriteMessage(message, buffer);
        return buffer.WrittenMemory;
    }

    public bool IsVersionSupported(int version) => version == Version;

    
}