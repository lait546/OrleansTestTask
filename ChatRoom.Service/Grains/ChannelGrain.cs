using Orleans.Runtime;
using Orleans.Serialization.Invocation;
using Orleans.Streams;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ChatRoom;

public class ChannelGrain : Grain, IChannelGrain
{
    private readonly List<ChatMsg> _messages = new(100);
    private readonly List<User> _onlineMembers = new(10);

    private IAsyncStream<ChatMsg> _stream = null!;
    private IAsyncStream _streamm = null!;

    private int Number = 2;
    private bool IsNext = true, IsStart = false;

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider("chat");

        var streamId = StreamId.Create(
            "ChatRoom", this.GetPrimaryKeyString());

        _stream = streamProvider.GetStream<ChatMsg>(
            streamId);

        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<bool> Start()
    {
        bool val = false;

        val = (_onlineMembers.Count >= 2 && _onlineMembers.Count != 0);

        if (!IsStart && val)
        {
            await _stream.OnNextAsync(
                new ChatMsg(
                    "Server",
                    $"Игра началась, загадано новое число от 0 до 100."));

            IsStart = true;
        }
        return IsStart;
    }

    public Task<int> GetNumber()
    {
        if (IsNext)
        {
            Number = GetRandom();
            IsNext = false;

            _stream.OnNextAsync(
                new ChatMsg(
                    "Server",
                    $"Загадано новое число от 0 до 100."));
        }

        return Task.FromResult(Number); 
    }

    public int GetRandom()
    {
        Random r = new Random();
        int rInt = r.Next(0, 100);

        return rInt;
    }

    public async Task<StreamId> Join(string nickname)
    {
        User user = new(nickname);
        _onlineMembers.Add(user);

        await _stream.OnNextAsync(
            new ChatMsg(
                nickname,
                $"{nickname} присоединился к комнате."));

        return _stream.StreamId;
    }

    public async Task<StreamId> Leave(string nickname)
    {
        User user = _onlineMembers.FindAll(x => x.Nickname == nickname)[0];
        _onlineMembers.Remove(user);

        await _stream.OnNextAsync(
            new ChatMsg(
                nickname,
                $"{nickname} вышел из комнаты..."));

        return _stream.StreamId;
    }

    public async Task<bool> Message(ChatMsg msg)
    {
        _messages.Add(msg);

        await _stream.OnNextAsync(msg);

        if (CheckNumberMessage(msg))
        {
            User? user = _onlineMembers.Find(x => x.Nickname == msg.Author);
            user.Number = msg.Text.RemoveLetters();
            user.IsGuessed = true;

            await _stream.OnNextAsync(
                new ChatMsg(
                    "Server",
                    $"Игрок {msg.Author} загадал число {user.Number}"));

            await TryEndRound(msg);
        }

        return true;
    }

    public async Task<bool> TryEndRound(ChatMsg msg)
    {
        User? user = _onlineMembers.Find(x => x.Nickname == msg.Author), 
        winner = user;

        for (int i = 0; i < _onlineMembers.Count; i++)
        {
            if (!_onlineMembers[i].IsGuessed)
                return false;
        }

        for (int i = 0; i < _onlineMembers.Count; i++)
        {
            int num1 = Number > winner.Number ? Number - winner.Number : winner.Number - Number,
                num2 = Number > _onlineMembers[i].Number ? Number - _onlineMembers[i].Number : _onlineMembers[i].Number - Number;

            if (num1 > num2)
                winner = _onlineMembers[i];
        }

        for (int i = 0; i < _onlineMembers.Count; i++)
        {
            _onlineMembers[i].IsGuessed = false;
            _onlineMembers[i].Number = 0;
        }

        await _stream.OnNextAsync(
                new ChatMsg(
                    "Server",
                    $" - Игрок {winner?.Nickname} выиграл! Было загадано число {Number}. -"));

        await GrainFactory.GetGrain<IUser>(winner.Number).AddPoints();
        winner.AddPoints();
        IsNext = true;
        await GetNumber();

        return true;
    }

    private bool CheckNumberMessage(ChatMsg msg)
    {
        if (msg.Text.Any(c => char.IsNumber(c)))
        {
            return true;
        }

        return false;
    }

    public Task<User[]> GetMembers() => Task.FromResult(_onlineMembers.ToArray());

    public Task<ChatMsg[]> ReadHistory(int numberOfMessages)
    {
        var response = _messages
            .OrderByDescending(x => x.Created)
            .Take(numberOfMessages)
            .OrderBy(x => x.Created)
            .ToArray();

        return Task.FromResult(response);
    }
}