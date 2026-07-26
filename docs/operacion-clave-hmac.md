# Clave HMAC de auditoria

QMSFlowDoc firma los nuevos registros de auditoria con HMAC-SHA-256. La clave no debe estar en la base de datos, el repositorio documental ni los directorios de copias.

1. Cree `C:\ProgramData\QMSFlowDoc\Secrets`.
2. Genere una clave de 32 bytes o mas y guarde su Base64 en la primera linea de `audit-hmac.key`:

```powershell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 })) | Set-Content C:\ProgramData\QMSFlowDoc\Secrets\audit-hmac.key -NoNewline
```

3. Restrinja el archivo a la cuenta que ejecuta QMSFlowDoc y a administradores. No lo copie en backups ni lo suba a Git.
4. Configure la variable de entorno de la maquina `AuditIntegrity__KeyPath` con `C:\ProgramData\QMSFlowDoc\Secrets\audit-hmac.key` y reinicie la aplicacion.

Para rotar, inserte una nueva clave Base64 como primera linea y conserve las claves anteriores en las lineas siguientes. QMS usara la primera para nuevas entradas y las restantes para verificar el historial. Retire una clave antigua solo cuando ya no existan registros que dependan de ella.
