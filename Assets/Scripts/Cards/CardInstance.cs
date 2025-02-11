public class CardInstance
{
    public CardDefinition Definition { get; private set; }

    public int CurrentStamina;
    public bool HasUsedFirstSnap;
    //etc.


    public CardInstance(CardDefinition def)
    {
        this.Definition = def;
        this.CurrentStamina = def.Stamina;
    }
}
