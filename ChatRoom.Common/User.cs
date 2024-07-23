using Orleans;
using Orleans.Core;
using Orleans.Runtime;
using System.Reflection;

namespace ChatRoom;

[GenerateSerializer]
[Alias("User")]
public record class User (string nickname)
{
    public Guid Id { get; set; }
    [Id(0)] public string Nickname { get; init; } = nickname;
    [Id(1)] public int Number { get; set; }
    [Id(2)] public int Points { get; set; }
    [Id(3)] public bool IsGuessed { get; set; }

    public void AddPoints()
    {
        Points++;
    }
}