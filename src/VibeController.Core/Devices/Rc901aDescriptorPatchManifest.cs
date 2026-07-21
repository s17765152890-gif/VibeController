namespace VibeController.Core.Devices;

public static class Rc901aDescriptorPatchManifest
{
    // Capture mode is intentional. Do not activate a patch until the physical
    // RC901A report descriptor, its SHA-256, and the malformed item offset are known.
    public static HidDescriptorPatchDefinition? ActivePatch => null;
}
