using System.Net;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Contacto
{
    // Manda al equipo por mail lo que alguien envio desde el formulario de contacto de la landing
    public class EnviarFormularioContacto
    {
        // Argentina no tiene horario de verano: siempre UTC-3
        private static readonly TimeSpan HoraArgentina = TimeSpan.FromHours(-3);

        private readonly IEmailService _email;
        private readonly ContactoOpciones _opciones;
        private readonly TimeProvider _reloj;

        public EnviarFormularioContacto(IEmailService email, ContactoOpciones opciones, TimeProvider reloj)
        {
            _email = email;
            _opciones = opciones;
            _reloj = reloj;
        }

        public async Task EjecutarAsync(FormularioContactoDto solicitud, CancellationToken cancellationToken = default)
        {
            // Un bot completo el campo oculto: se responde igual que si saliera bien, pero no se manda nada
            if (!string.IsNullOrWhiteSpace(solicitud.Sitio))
                return;

            var email = solicitud.Email.Trim();
            var nombre = Limpiar(solicitud.Nombre);
            var zona = Limpiar(solicitud.Zona);
            var fecha = _reloj.GetUtcNow().ToOffset(HoraArgentina);

            var asunto = $"Formulario de contacto: {nombre ?? email}";
            // Lo que escribio la persona se codifica para que no pueda meter HTML en el mail
            var cuerpo = $"""
                <h3>Nuevo mensaje del formulario de contacto</h3>
                <p><strong>Email:</strong> {WebUtility.HtmlEncode(email)}</p>
                <p><strong>Nombre:</strong> {WebUtility.HtmlEncode(nombre ?? "No lo indico")}</p>
                <p><strong>Donde busca:</strong> {WebUtility.HtmlEncode(zona ?? "No lo indico")}</p>
                <p><strong>Fecha:</strong> {fecha:dd/MM/yyyy HH:mm} (hora de Argentina)</p>
                """;

            try
            {
                await _email.SendEmailAsync(_opciones.Destinatario, asunto, cuerpo, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new ServiceUnavailableException("No pudimos enviar el formulario en este momento. Intenta de nuevo en unos minutos.", ex);
            }
        }

        // Espacios de mas y saltos de linea afuera; vacio pasa a null
        private static string? Limpiar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return null;

            return string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
