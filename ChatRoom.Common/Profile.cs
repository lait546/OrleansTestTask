using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans.Core;
using Orleans.Runtime;

namespace ChatRoom
{
    public class Profile : Grain
    {
        public string Nickname { get; init; } = "";
        public int Points { get; set; }
        private readonly IPersistentState<Profile> _profile;

        public Profile(
        [PersistentState("user", "userStore")] IPersistentState<Profile> profile)
        {
            _profile = profile;
        }
    }
}
