# Configuracion externa de la instalacion

No guarde rutas, claves ni credenciales de produccion en `appsettings.json` ni en Git. Configure estas variables de entorno para la cuenta que ejecuta QMSFlowDoc:

```powershell
[Environment]::SetEnvironmentVariable('QMSFLOWDOC_DATA', 'C:\QMSFlowDoc\Data', 'Machine')
[Environment]::SetEnvironmentVariable('QMSFLOWDOC_SECRETS', 'C:\ProgramData\QMSFlowDoc\Secrets', 'Machine')
[Environment]::SetEnvironmentVariable('AuditIntegrity__KeyPath', 'C:\ProgramData\QMSFlowDoc\Secrets\audit-hmac.key', 'Machine')
```

`QMSFLOWDOC_DATA` debe estar en un volumen local del servidor y contener la base SQLite y `DocumentStorage`. Restrinja mediante ACL el acceso a la cuenta de servicio de QMSFlowDoc y a administradores autorizados. Las copias deben ubicarse en otra unidad o recurso protegido, nunca dentro de `DocumentStorage`.

Las copias de recuperación usan EFS de Windows por defecto (`BackupEncryption:Enabled=true`). La cuenta que ejecute QMSFlowDoc debe conservar su certificado EFS y los agentes de recuperación autorizados deben estar documentados; sin ellos, una restauración desde otro equipo no podrá leer la copia.
