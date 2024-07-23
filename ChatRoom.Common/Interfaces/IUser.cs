using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatRoom
{
    public interface IUser : IGrainWithIntegerKey
    {
        Task AddPoints();
    }
}
