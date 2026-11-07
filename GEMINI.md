# Convenciones de código de ZonaMatch

Esta guía define cómo escribir código nuevo y cómo unificar gradualmente el existente. Se aplica a la API y, cuando corresponda, a los clientes que consumen su contrato.

## 1. Idioma y nombres

- El código fuente se escribe en inglés: nombres de proyectos, carpetas, namespaces, tipos, propiedades, variables, parámetros, métodos, rutas HTTP, nombres JSON, comentarios y pruebas.
- Usar las convenciones del lenguaje: `PascalCase` para tipos y miembros públicos, `camelCase` para variables y parámetros, interfaces con prefijo `I` y sufijo `Async` para métodos asíncronos.
- Nombrar en singular los tipos que representan una entidad, un elemento o un resultado individual (`Group`, `Participant`, `GroupResponse`). Cuando el contenido principal de una respuesta sea una colección, usar `{Resource}ListResponse`, con el recurso en singular (`GroupListResponse`, `ZoneListResponse`). Una respuesta de un único recurso mantiene el singular aunque incluya propiedades de colección (`GroupDetailsResponse` con `participants`). Para otros tipos que representan colecciones o agregados, usar el nombre plural del concepto (`ZoneReviews`, `ProfileRecommendations`).
- Nombrar las propiedades que contienen colecciones en plural (`participants`, `zones`). Para componentes, usar singular si presentan un elemento (`ParticipantCard`) y plural si presentan una colección (`ParticipantList`).
- Mantener plurales convencionales que describen un conjunto de campos u opciones, como `Props`, `Options` y `Values`; no cambiar un nombre solo porque termina en `s`.
- Los textos visibles para usuarios pueden estar en español, pero deben tratarse como contenido de producto y mantenerse separados de los nombres y comentarios del código.
- No cambiar nombres de rutas ni campos JSON existentes sin coordinar la migración del cliente. Para cambios incompatibles, publicar una nueva versión de la API o mantener temporalmente compatibilidad.

## 2. Arquitectura y responsabilidades

Mantener las responsabilidades separadas entre `ZonaMatch.Api`, `ZonaMatch.Application`, `ZonaMatch.Domain` y `ZonaMatch.Infrastructure`.

### API

- Los controladores definen rutas, autorización, binding HTTP y códigos/respuestas HTTP.
- Los controladores delegan cada operación a un caso de uso. No contienen reglas de negocio ni coordinan repositorios directamente.
- Mantener los métodos de endpoint cortos y pasar el `CancellationToken` recibido a la siguiente capa.

### Application

- Cada operación de la aplicación tiene un caso de uso propio, con un nombre de acción en inglés, por ejemplo `CreateGroup`, `GetZoneBySlug` o `RegisterUser`.
- Los casos de uso coordinan reglas de la operación, repositorios e interfaces de servicios. Pueden validar reglas de aplicación y devolver modelos de respuesta.
- Crear un servicio de aplicación cuando encapsule una capacidad reutilizada por varios casos de uso; mantener en cada caso de uso la lógica y coordinación propias de su operación.
- Las interfaces que necesita la aplicación para persistencia o integraciones externas se declaran en esta capa; la aplicación no referencia implementaciones concretas de infraestructura.
- No agregar lógica de negocio nueva a servicios genéricos de aplicación. Si una operación ya está en `Application.Services`, moverla gradualmente al caso de uso correspondiente.

### Domain

- Las entidades y objetos de valor contienen invariantes propias del dominio y no dependen de HTTP, EF Core, SMTP ni clientes externos.
- Mantener esta capa independiente de Application, Infrastructure y API.

### Infrastructure

- Aquí viven las implementaciones de repositorios y los adaptadores de servicios externos: correo, geocodificación, ruteo y acceso a base de datos.
- El adaptador implementa una interfaz declarada por Application. No colocar reglas de negocio en el adaptador.
- Aislar los detalles del proveedor externo para que puedan cambiarse sin modificar los casos de uso.

## 3. Rutas y contrato HTTP

Usar un patrón REST uniforme para las rutas nuevas:

- Prefijo versionado: `/api/v1`.
- Recursos en inglés, en minúsculas y en plural cuando corresponda: `zones`, `groups`, `reviews`.
- Usar `kebab-case` en segmentos compuestos y nombres de recursos; usar sustantivos y dejar que el verbo HTTP indique la operación.
- Usar parámetros con nombres claros y restricciones cuando se conozca el formato: `{id:guid}`, `{slug}`.
- Usar query string para filtros, búsqueda, paginación y opciones de lectura.
- Definir rutas explícitas, evitando depender de `[controller]` para generar el contrato público.

Ejemplos:

```text
GET    /api/v1/zones?search=palermo
GET    /api/v1/zones/{slug}
GET    /api/v1/zones/{slug}/reviews
POST   /api/v1/groups
GET    /api/v1/groups/{id:guid}
DELETE /api/v1/groups/{id:guid}
DELETE /api/v1/groups/{groupId:guid}/participants/{participantId:guid}
```

No crear rutas CRUD con verbos como `/Create`, `/Get`, `/Update` o `/Delete`. Para una operación que no encaje en CRUD, modelarla como un subrecurso o recurso de acción en plural, conservando la semántica HTTP.

Las rutas visibles de navegación del SPA (por ejemplo, `/analisis` y `/zonas`) pueden permanecer en español. Las rutas HTTP del API usan `/api/v1` y nombres de recursos en inglés. Si se cambia una ruta del API existente, coordinar y desplegar el cambio junto con sus clientes.

## 4. Inyección de dependencias en endpoints

- Inyectar los casos de uso en los parámetros del método del endpoint con `[FromServices]`.
- No usar campos `readonly` ni constructores de controlador para inyectar dependencias.
- Un caso de uso por endpoint mantiene acotada la lista de dependencias del método.
- Marcar explícitamente el origen HTTP de los valores cuando ayude a leer el contrato: `[FromRoute]`, `[FromQuery]` y `[FromBody]`.
- `CancellationToken` se recibe como parámetro del endpoint y se propaga a las operaciones asíncronas.

```csharp
[HttpGet("{slug}")]
public async Task<ActionResult<ZoneResponse>> GetBySlug(
    [FromRoute] string slug,
    [FromServices] GetZoneBySlug useCase,
    CancellationToken cancellationToken)
{
    var response = await useCase.ExecuteAsync(slug, cancellationToken);
    return Ok(response);
}
```

## 5. Requests y Responses

- Los modelos que representan el contrato HTTP terminan en `Request` o `Response`; no usar sufijos ambiguos como `Dto` para esos contratos.
- Separarlos por funcionalidad y por dirección:

```text
ZonaMatch.Application/DTOs/Group/Requests/CreateGroupRequest.cs
ZonaMatch.Application/DTOs/Group/Requests/UpdateGroupRequest.cs
ZonaMatch.Application/DTOs/Group/Responses/GroupResponse.cs
ZonaMatch.Application/DTOs/Group/Responses/GroupDetailsResponse.cs
```

- No mezclar requests y responses en un mismo archivo. Mantener nombres JSON consistentes en `camelCase`.
- No exponer entidades del dominio directamente en las respuestas. Mapearlas a modelos de respuesta explícitos.
- Los modelos internos que no representan un request o una response HTTP (por ejemplo, una caja geográfica usada internamente) no necesitan renombrarse artificialmente.

## 6. Errores y respuestas

- No usar `try/catch` en endpoints para convertir excepciones en respuestas HTTP. La conversión se realiza en el manejador global de excepciones.
- Definir una excepción reutilizable por cada código HTTP importante, por ejemplo `BadRequestException`, `UnauthorizedException`, `ForbiddenException`, `NotFoundException` y `ConflictException`.
- Usar esas mismas excepciones para todos los recursos. Por ejemplo, tanto grupos como contextos lanzan `NotFoundException`; no crear `GroupNotFoundException` ni `ContextNotFoundException`.
- Pasar un mensaje claro y contextual al constructor para identificar el recurso y la causa, por ejemplo `throw new NotFoundException("Group not found.");`.
- El manejador global mapea cada tipo de excepción a un único código HTTP y presenta `ProblemDetails`. No decidir el estado comparando el texto del mensaje.
- En los casos de uso, agregar un comentario breve junto al lanzamiento cuando ayude a explicar la regla o condición de negocio. El mensaje contextual y la traza identifican el error y su ubicación; no crear excepciones adicionales para agregar ese contexto.
- No lanzar `Exception` genérica para errores esperables. Los errores técnicos inesperados siguen el manejo genérico del manejador global y no deben convertirse artificialmente en errores de negocio.
- Reservar `catch` para recuperar, reintentar, liberar recursos o traducir un error de proveedor en un error tipado; no para construir una respuesta HTTP local.
- Mantener un formato consistente con `ProblemDetails` para errores y no incluir detalles internos en producción.
- Documentar las respuestas de cada endpoint con `ProducesResponseType` y usar `ActionResult<T>` cuando haya un tipo de respuesta.

Ejemplo de lanzamiento en un caso de uso:

```csharp
// Reject the request when the group does not exist.
throw new NotFoundException("Group not found.");
```

Mapeo inicial recomendado:

| Resultado | HTTP | Manejo |
| --- | --- | --- |
| Validación de request o regla de aplicación inválida | 400 | `ValidationProblemDetails` o `BadRequestException` |
| Falta autenticación o credenciales inválidas | 401 | Autorización ASP.NET o `UnauthorizedException` |
| Usuario autenticado sin permiso | 403 | `ForbiddenException` |
| Recurso inexistente | 404 | `NotFoundException` |
| Conflicto de estado o recurso duplicado | 409 | `ConflictException` |
| Dependencia temporalmente no disponible | 503 | `ServiceUnavailableException` o mapeo central controlado |
| Error inesperado | 500 | Respuesta genérica y registro del detalle interno |
| Límite de solicitudes excedido | 429 | Middleware de rate limiting |

No hace falta una excepción por recurso o por regla de negocio: las excepciones se comparten según el código HTTP. Códigos como 200, 201 y 204 son resultados exitosos, y otros como 429 ya los produce el middleware de rate limiting. La excepción expresa el tipo general de error y su mensaje aporta el contexto concreto.

## 7. Validación, respuestas y persistencia

- Validar formato y campos requeridos del body en el modelo `Request`; validar reglas de negocio en el caso de uso o en el dominio.
- Usar `201 Created` al crear un recurso cuando exista una ruta válida para consultarlo (`CreatedAtAction`); usar `204 No Content` cuando la operación no devuelve un cuerpo.
- Al usar `CreatedAtAction`, apuntar a una acción de consulta existente y pasar valores que coincidan con sus parámetros de ruta. Verificar que ASP.NET pueda generar la URL; si se usa una ruta nombrada, elegir el resultado correspondiente, como `CreatedAtRoute`.
- Pasar `CancellationToken` por todos los límites asíncronos, incluidos repositorios y clientes externos. Evitar `.Result` y `.Wait()`.
- Mantener las migraciones de EF Core en Infrastructure y no editar a mano archivos generados por EF.
- Leer configuración mediante opciones tipadas y validar al iniciar lo requerido para ejecutar la aplicación. No guardar secretos en el repositorio.

## 8. Registro de dependencias y configuración

- Registrar dependencias con el lifetime adecuado al recurso: repositorios y casos de uso normalmente `Scoped`; estado compartido solo cuando sea seguro para concurrencia.
- Mantener el registro de servicios organizado por capa o funcionalidad. Al crecer la lista de `Program.cs`, agruparla en métodos como `AddApplication()` y `AddInfrastructure(configuration)`.
- La composición de dependencias ocurre en el punto de entrada; las clases de negocio no crean implementaciones concretas por su cuenta.

## 9. Pruebas

### 9.1 Organización y alcance

- Mantener pruebas unitarias e integración en proyectos separados: `ZonaMatch.Tests` y `ZonaMatch.Api.IntegrationTests`.
- Organizar las pruebas unitarias por capa y funcionalidad (`Domain`, `UseCases`, `Infrastructure`) y las de integración por área de API o infraestructura (`Api`, `Infrastructure`). Seguir la estructura del código probado.
- Nombrar las clases de prueba con el nombre de la unidad bajo prueba más el sufijo `Tests`; concentrar cada clase en una unidad o responsabilidad relacionada. Ubicar utilidades compartidas en archivos identificables, como `*TestSupport` o `*TestFakes`.
- Probar el comportamiento público de la unidad. Los casos de uso son el foco de las pruebas unitarias de aplicación; las pruebas de controladores y repositorios deben cubrir principalmente el contrato HTTP, autorización, persistencia y mapeo entre capas mediante integración.

### 9.2 Nombres de casos

- Escribir nombres de pruebas e identificadores en inglés.
- Usar el patrón `MethodUnderTest_WhenCondition_ExpectedOutcome`, por ejemplo `GetGroup_WhenGroupDoesNotExist_ThrowsNotFoundException` o `CreateGroup_WhenRequestIsValid_PersistsGroupAndReturnsResponse`.
- Cada caso debe expresar una condición y el resultado esperado. Usar `[Fact]` para un escenario y `[Theory]` con `[InlineData]` cuando varias entradas verifican el mismo comportamiento.

### 9.3 Estructura y aserciones

- Estructurar cada prueba en Arrange, Act y Assert, en ese orden. Mantener la preparación local al caso para que sus datos y dependencias sean fáciles de entender.
- Usar xUnit para declarar y ejecutar los casos (`[Fact]`, `[Theory]`) y FluentAssertions para las aserciones (`.Should()`). Mantener un estilo de aserción consistente dentro de cada prueba; usar `Verify` de Moq cuando una interacción con un colaborador sea parte del comportamiento esperado.
- Comprobar resultados observables: valores devueltos, estado persistido, efectos relevantes, interacciones necesarias y excepciones esperadas. Cubrir tanto caminos exitosos como errores y límites importantes; evitar aserciones sobre detalles privados o de implementación que no formen parte del contrato.
- Mantener cada caso centrado en un comportamiento. Separar escenarios con resultados distintos y evitar pruebas que agrupen flujos no relacionados.

### 9.4 Dobles de prueba

- Aislar la unidad bajo prueba reemplazando sus dependencias por interfaces controlables.
- Usar Moq para dependencias simples cuando baste configurar una respuesta o verificar una llamada y sus argumentos. Usar fakes explícitos cuando se necesite estado en memoria, comportamiento reutilizable o una preparación de datos más clara.
- No convertir todos los fakes a Moq solo para uniformar el estilo: elegir el doble más legible para el escenario y mantener los fakes compartidos en archivos de soporte.
- No simular el comportamiento interno de Entity Framework para probar consultas o persistencia. Cubrir esos comportamientos con pruebas de integración contra PostgreSQL y el esquema real.

### 9.5 Integración, servicios externos y aislamiento

- Usar pruebas de integración para comprobar conjuntamente endpoints, middleware, autenticación, validación, serialización, persistencia y respuesta HTTP.
- Ejecutar las pruebas de persistencia con una base de datos descartable, aplicando las migraciones reales. Las pruebas que dependan de contenedores deben documentar que Docker debe estar disponible.
- Probar adaptadores de servicios externos con respuestas controladas o servicios locales de prueba. Las pruebas no deben depender de internet, credenciales reales ni cuentas de usuarios reales.
- Mantener los casos deterministas e independientes del orden de ejecución. Evitar pausas arbitrarias, datos compartidos entre pruebas y dependencias del reloj del sistema; fijar el tiempo o usar un `TimeProvider` controlado cuando corresponda.

### 9.6 Cobertura y CI

- El CI exige al menos un 90 % de cobertura de líneas en los ensamblados de producción; los ensamblados de pruebas no cuentan para ese umbral.
- La cobertura es una señal de alcance, no un sustituto de pruebas que verifiquen resultados y reglas de negocio. No agregar aserciones vacías ni casos sin comportamiento significativo solo para alcanzar el porcentaje.
- Antes de integrar cambios, ejecutar las pruebas afectadas y el conjunto de pruebas requerido por el pipeline; corregir fallos y mantener los resultados reproducibles.

## Lista rápida para revisión

- [ ] Nombres y comentarios del código en inglés; rutas HTTP del API en inglés; convenciones de C# aplicadas. Las rutas de navegación del SPA pueden seguir en español.
- [ ] La operación delega la lógica de aplicación a un caso de uso.
- [ ] El endpoint inyecta el caso de uso con `[FromServices]` y propaga `CancellationToken`.
- [ ] Los contratos terminan en `Request` o `Response` y están en la carpeta de funcionalidad singular y en `Requests` o `Responses`, según corresponda.
- [ ] La ruta usa el patrón REST establecido y no rompe el cliente existente.
- [ ] Los errores esperables usan la excepción compartida del código HTTP correspondiente y el manejador global define su respuesta.
- [ ] No hay `try/catch` local para devolver códigos HTTP.
- [ ] Se agregaron o actualizaron pruebas para el comportamiento modificado, con nombres en inglés y estructura Arrange, Act, Assert.
- [ ] Los casos unitarios usan dobles adecuados; los contratos HTTP y la persistencia se cubren con integración.
- [ ] El CI mantiene al menos un 90 % de cobertura de líneas en código de producción.
