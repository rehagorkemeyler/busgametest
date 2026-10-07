namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Aracın sürüş fiziğinden bağımsız okunabilir durumu.
    /// Kapı, durak ve HUD sistemleri fizik paketine (RCC vb.) değil bu arayüze bağlıdır;
    /// böylece araç kontrolcüsü değiştirildiğinde diğer sistemler etkilenmez.
    /// </summary>
    public interface IVehicleTelemetry
    {
        float SpeedKmh { get; }
        bool IsStopped { get; }
    }
}
