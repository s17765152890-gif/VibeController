namespace VibeController.Core.Devices;

public readonly record struct ControllerLightbarColor(
    byte Red,
    byte Green,
    byte Blue);

public interface IControllerLightbar
{
    void SetLightbarColor(ControllerLightbarColor color);
}
