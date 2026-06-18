using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Domain.SignalR
{
    public class ChatBotSig : Hub
    {
        public async Task SendTaskUpdate(string message)
        {
            await Clients.All.SendAsync("RecieveTaskUpdate", message);
        }
    }
}
