/// <summary>
/// Entityが接触した時のコールバック。Colliderを持つオブジェクトに付ける。
/// Entity本体を変更せずにギミック（ハザード、アイテム、スイッチ等）の反応を定義できる。
/// HandleContacts()でOverlapEntityの結果からGetComponentsで取得・発火される。
/// </summary>
public interface IEntityContact
{
    void OnEntityContact(Entity entity);
}
