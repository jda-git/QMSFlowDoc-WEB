# Checklist de despliegue y aceptación

## Antes del despliegue

- [ ] Confirmar que la rama a desplegar contiene las migraciones de auditoría, imparcialidad y monitorización ambiental.
- [ ] Ejecutar `dotnet restore "QMSFlowDoc V2.sln"` y `dotnet build "QMSFlowDoc V2.sln" -c Release`.
- [ ] Ejecutar `dotnet test src\QMSFlowDoc.Tests.Compliance\QMSFlowDoc.Tests.Compliance.csproj -c Release`.
- [ ] Verificar dependencias con `dotnet list "QMSFlowDoc V2.sln" package --vulnerable --include-transitive`.
- [ ] Configurar `QMSFLOWDOC_DATA`, `QMSFLOWDOC_SECRETS` y `AuditIntegrity__KeyPath` para la cuenta de servicio.
- [ ] Verificar ACL del directorio de datos, secretos y copias. La clave HMAC no se almacena en la base de datos ni en copias.

## Arranque y migración

- [ ] Arrancar QMSFlowDoc con la cuenta de servicio definitiva.
- [ ] Confirmar que se crean/aplican `ImpartialityDeclarations` y `EnvironmentalReadings` en SQLite.
- [ ] Comprobar inicio de sesión, autorización de rutas y acceso de auditoría solo con usuario autorizado.
- [ ] Confirmar que las claves de ASP.NET se conservan en el directorio de secretos protegido con DPAPI.

## Pruebas de aceptación funcional

- [ ] Crear una declaración de imparcialidad y revisarla; verificar el historial de auditoría.
- [ ] Crear método, versión, incertidumbre y validación; aprobar una versión y verificar la auditoría.
- [ ] Importar un `.txt` ambiental mensual de prueba.
- [ ] Revisar que NEVERA produce dos series y que `NC` queda registrado como sonda desconectada.
- [ ] Confirmar que los eventos CENTRAL se conservan y no distorsionan las medias.
- [ ] Generar PDF del lote: estadísticas, incidencias y curvas deben corresponder al periodo seleccionado.
- [ ] Cambiar un límite ambiental en Configuración y verificar que se aplica a una importación posterior.

## Continuidad y cierre

- [ ] Ejecutar una copia de recuperación manual y comprobar que se cifra mediante EFS bajo la cuenta autorizada.
- [ ] Verificar el conjunto con RecoveryTool usando `AuditIntegrity__KeyPath`.
- [ ] Restaurar una copia en un entorno aislado y documentar resultado, operador y duración.
- [ ] Guardar evidencias de esta aceptación en el gestor documental y registrar las acciones pendientes en Mejora Continua.

## Criterio de aceptación

El despliegue se acepta cuando no hay errores de compilación ni pruebas fallidas, la comprobación de vulnerabilidades no presenta paquetes vulnerables, las rutas protegidas exigen sesión, la auditoría HMAC verifica correctamente y las pruebas de copia/restauración e importación ambiental han quedado documentadas.
