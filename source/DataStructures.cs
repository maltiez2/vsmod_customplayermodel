using ProtoBuf;
using Vintagestory.API.Common;

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

public sealed class CustomPlayerModelConfig
{
    public int Version { get; set; } = 0;
    public string? DisplayedName { get; set; }
    public string BaseShapeCode { get; set; } = "";
    public Dictionary<string, string> WearableModelReplacers { get; set; } = [];
    public Dictionary<string, CompositeShape> WearableCompositeModelReplacers { get; set; } = [];
    public Dictionary<string, string> WearableModelReplacersByShape { get; set; } = [];
    public float[] CollisionBox { get; set; } = [];
    public float EyeHeight { get; set; } = 1.7f;
    public float[] SizeRange { get; set; } = [0.8f, 1.2f];
    public bool ScaleColliderWithSizeHorizontally { get; set; } = true;
    public bool ScaleColliderWithSizeVertically { get; set; } = true;
    public float[] MaxCollisionBox { get; set; } = [float.MaxValue, float.MaxValue];
    public float[] MinCollisionBox { get; set; } = [0, 0];
    public float MaxEyeHeight { get; set; } = float.MaxValue;
    public float MinEyeHeight { get; set; } = 0;
    public string[] AddTags { get; set; } = [];
    public string[] RemoveTags { get; set; } = [];
    public float ModelSizeFactor { get; set; } = 1;
    public float HeadBobbingScale { get; set; } = 1;
    public float GuiModelScale { get; set; } = 1;
    public float WalkEyeHeightMultiplier { get; set; } = 1;
    public float SprintEyeHeightMultiplier { get; set; } = 1;
    public float SneakEyeHeightMultiplier { get; set; } = 0.8f;
    public float StepHeight { get; set; } = 0.6f;
}