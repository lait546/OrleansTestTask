using Microsoft.Extensions.Logging;
using Orleans.Core;

//using Orleans.Core;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;


namespace ChatRoom.Service
{
    public class PlayerGrain : Grain, IUser
    {
        private readonly ILogger<PlayerGrain> _logger;
        private readonly IPersistentState<User> _storage;
        
        public PlayerGrain(
        ILogger<PlayerGrain> logger,
            [PersistentState("player", "gameState")] IPersistentState<User> storage
        )
        {
            _logger = logger;
            _storage = storage;
        }

        public async Task AddPoints()
        {
            
            _storage.State.Points++;

            await _storage.WriteStateAsync();
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ACTIVATED");
            _storage.State.Id = this.GetPrimaryKey();
            return base.OnActivateAsync(cancellationToken);
        }

        public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
        {
            _logger.LogInformation("ACTIVATED");
            await _storage.WriteStateAsync();
            await base.OnDeactivateAsync(reason, cancellationToken);
        }
    }
}
