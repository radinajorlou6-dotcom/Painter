public interface ISerializer
{
    void SaveGame(GameData data);
    GameData LoadGame();
}