# QMSFlowDoc Recovery Tool

Este ejecutable restaura un conjunto de recuperación completo sin iniciar la
aplicación web. Cada conjunto contiene la base SQLite y el repositorio
documental que le corresponde.

## Procedimiento operativo

1. Detenga el servicio o proceso de QMSFlowDoc.Web en el servidor.
2. Copie el conjunto elegido desde `RecoverySets` a un medio accesible por el
   servidor, si fuera necesario.
3. Compruebe primero su integridad:

   ```powershell
   QMSFlowDoc.RecoveryTool.exe verify --set "D:\Backups\RecoverySets\<conjunto>"
   ```

4. Restaure solo cuando la comprobación sea correcta:

   ```powershell
   QMSFlowDoc.RecoveryTool.exe restore --set "D:\Backups\RecoverySets\<conjunto>" --database "D:\QMS\Base_datos\qmsflowdoc.db" --documents "D:\QMS\DocumentRepository" --operator "nombre.apellidos" --confirm RESTORE
   ```

La herramienta conserva las copias sustituidas con el sufijo
`before_restore_...` y escribe un informe en `RecoveryReports`. Revise el
informe antes de reiniciar la web.

## Publicación como ejecutable independiente

```powershell
dotnet publish .\src\QMSFlowDoc.RecoveryTool\QMSFlowDoc.RecoveryTool.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Guarde el `.exe` publicado fuera del directorio de datos de QMSFlowDoc y
pruebe el procedimiento de restauración de forma periódica en un entorno no
productivo.
