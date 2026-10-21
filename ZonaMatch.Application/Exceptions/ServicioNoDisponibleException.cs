namespace ZonaMatch.Application.Exceptions
{
    // Un servicio externo (p. ej. el servidor de correo) no respondio; el pedido se puede reintentar (503)
    public class ServicioNoDisponibleException : Exception
    {
        public ServicioNoDisponibleException(string mensaje, Exception causa)
            : base(mensaje, causa)
        {
        }
    }
}
