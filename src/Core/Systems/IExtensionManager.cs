namespace SwarmUI.Core.Systems;

public interface IExtensionManager
{
    void AddExtensionPath(string path);

    void Setup();

    void Shutdown();

    void RunOnAll(Action<Extension> action);
}
