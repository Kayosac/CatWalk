// То, что пишется в сохранение. SaveGame собирает все такие компоненты в сцене по SaveKey
// и хранит их состояние строкой (обычно JsonUtility). Ключ уникален и постоянен между запусками.
public interface ISaveable
{
    string SaveKey { get; }
    string Save();
    void Load(string data);
}
