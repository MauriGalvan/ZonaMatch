namespace ZonaMatch.Application.DTOs
{
    // Bloque "contexto" del wizard: { personas, edades[], movilidad[], mascotas[] }.
    // Se valida en el servicio: personas 1..8; movilidad y mascotas se guardan verbatim.
    public class ContextoRequest
    {
        public int Personas { get; set; } = 1;

        // Rango etario por integrante, en orden; puede venir menos edades que personas
        public List<string?> Edades { get; set; } = [];

        // a-pie, transporte-publico, vehiculo, bici — igual que mascotas: verbatim
        public List<string> Movilidad { get; set; } = [];

        public List<string> Mascotas { get; set; } = [];
    }
}
