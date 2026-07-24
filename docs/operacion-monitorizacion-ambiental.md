# Operación de monitorización ambiental

## Alcance

Este procedimiento cubre la importación mensual del fichero horario de la centralita, la revisión de resultados y el archivo del informe PDF. Apoya ISO 15189:2022, apartado 6.3 (instalaciones y condiciones ambientales).

## Formato admitido

Cada línea usa punto y coma y fecha `dd/MM/yyyy`:

```text
fecha;hora;NEVERA;temperatura_nevera;temperatura_congelador_o_NC
fecha;hora;HABITACION;temperatura;humedad
fecha;hora;CENTRAL;evento
```

- Una línea `NEVERA` crea una lectura de nevera y otra de congelador.
- `NC` identifica una sonda de congelador no conectada; se considera incidencia.
- `CENTRAL` conserva eventos de centralita, alarmas, correo y Telegram, pero no se incorpora a medias térmicas.
- Los ficheros pueden contener el mes completo. Las lecturas duplicadas por fecha/hora/origen se omiten.

## Límites iniciales

Los valores se editan por un administrador en **Configuración > Límites ambientales** y se aplican a importaciones posteriores:

| Área | Mínimo | Máximo |
| --- | ---: | ---: |
| Nevera | 2 °C | 8 °C |
| Congelador | -25 °C | -15 °C |
| Habitación | 18 °C | 25 °C |
| Humedad | 40 % | 60 % |

Cambiar un límite exige documentar la justificación en el procedimiento local y revisar el impacto sobre los lotes importados a continuación.

## Flujo mensual

1. Conserve el `.txt` original en el repositorio documental controlado o en la ubicación definida por el laboratorio.
2. Abra **Monitorización ambiental** e importe el fichero del mes.
3. Revise el resumen del lote: periodo, lecturas, duplicados, eventos CENTRAL, fuera de rango y sondas `NC`.
4. Revise las gráficas de temperatura y humedad.
5. Genere el PDF para el lote seleccionado. El informe incluye estadísticas, incidencias, eventos y curvas embebidas.
6. Archive el PDF como registro controlado y trate cada desviación mediante el procedimiento de trabajo no conforme/CAPA cuando aplique.
7. Incluya el resumen de desviaciones y sondas `NC` en la revisión por dirección.

## Tratamiento de incidencias

- **Fuera de rango**: evaluar duración, impacto sobre muestras/reactivos/equipos y acciones de contención. Registrar una no conformidad cuando exista impacto o incumplimiento del procedimiento.
- **Sonda NC**: restaurar conexión, verificar el dato con método alternativo y registrar la evaluación del periodo afectado.
- **Evento CENTRAL de alarma**: corroborar la lectura correspondiente y conservar la evidencia de notificación/actuación.

La aplicación no elimina ni modifica las líneas originales importadas; mantiene lote, usuario y hora de importación para trazabilidad.
