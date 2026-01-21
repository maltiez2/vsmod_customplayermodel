using ProtoBuf;

namespace CustomPlayerModel;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class CustomModelPacket
{
    public string ModelName { get; set; } = "";
    public string Id { get; set; } = "";
    public byte[] Config { get; set; } = [];
    public byte[] Shape { get; set; } = [];
    public byte[] Texture { get; set; } = [];
    public byte[] Sound { get; set; } = [];
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class CustomModelRequestPacket
{
    public bool Data { get; set; } = false;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class CustomModelWhiteList
{
    public bool IsOnWhiteList { get; set; } = false;
    public string[] ModelsOnWhiteList { get; set; } = [];
    public bool PlayersWhiteList { get; set; } = false;
    public bool ModelsWhiteList { get; set; } = false;
}