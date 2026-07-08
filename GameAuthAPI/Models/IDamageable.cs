namespace GameAuthAPI.Models
{
    public interface IDamageable
    {
        int Health { get; set; }
        int Defense { get; }
        // Можно добавить другие общие свойства для сущностей, по которым можно наносить урон
    }
}