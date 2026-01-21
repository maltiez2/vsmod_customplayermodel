namespace CustomPlayerModel;

public class Settings
{
    public bool PlayersWhiteList { get; set; } = false;
    public bool ModelsWhiteList { get; set; } = false;
    public bool ShapesWhiteList { get; set; } = false;
    public bool TexturesWhiteList { get; set; } = false;
    public bool SoundsWhiteList { get; set; } = false;

    public bool ConfigTraitsAndClass {  get; set; } = true;
    public bool ConfigCollisionBox { get; set; } = true;
    public bool ConfigEyeHeight { get; set; } = true;
    public bool ConfigStepHeight { get; set; } = true;
    public bool ConfigSize { get; set; } = true;
    public bool ConfigOxygen { get; set; } = true;
}