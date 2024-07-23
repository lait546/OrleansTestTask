using Orleans.Runtime;

namespace ChatRoom;

public interface IChannelGrain : IGrainWithStringKey
{
    Task<StreamId> Join(string nickname);
    Task<StreamId> Leave(string nickname);
    Task<bool> Start();
    Task<int> GetNumber();
    Task<bool> Message(ChatMsg msg);
    Task<ChatMsg[]> ReadHistory(int numberOfMessages);
    Task<User[]> GetMembers();
}
