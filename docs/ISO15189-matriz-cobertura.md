# Matriz de cobertura ISO 15189:2022

## Propósito y alcance

Esta matriz relaciona los requisitos principales de ISO 15189:2022 con las capacidades de QMSFlowDoc. Es una herramienta de preparación de auditoría; no constituye por sí sola una declaración de conformidad ni sustituye la evaluación del organismo de acreditación.

Los estados significan:

- **Cubierto**: el sistema mantiene registros y trazabilidad suficientes para el requisito.
- **Parcial**: existe apoyo funcional, pero se requiere procedimiento, evidencia externa o mejora.
- **Fuera de alcance**: corresponde al laboratorio, al LIS o a la práctica clínica, no al QMS.

| ISO 15189:2022 | Estado | Evidencia en QMSFlowDoc | Acción pendiente |
| --- | --- | --- | --- |
| 4.1 Imparcialidad | Parcial | Declaración de independencia en auditorías internas y roles segregados. | Registrar conflictos de interés y su revisión periódica. |
| 4.2 Confidencialidad e información | Parcial | Autenticación, autorización por permiso, visor documental protegido, auditoría HMAC y errores sin detalle técnico. | Formalizar clasificación de datos, plazo de retención y prueba periódica de restauración. |
| 4.3 Requisitos relativos a pacientes | Fuera de alcance | No se almacenan resultados ni datos de paciente como función principal. | Integrar con LIS si se incorpora información clínica. |
| 5.1-5.5 Gobernanza, director, estructura y políticas | Parcial | Configuración de laboratorio, usuarios, roles, políticas y revisión por dirección. | Mantener en el gestor documental los nombramientos, delegaciones y objetivos aprobados. |
| 5.6 Gestión del riesgo | Cubierto | Módulo Mejora Continua: matriz de riesgos, acciones, eficacia, evidencia y aprobación. | Revisar riesgos en la revisión por dirección conforme al procedimiento local. |
| 6.2 Personal | Cubierto | Expediente, formación, competencias, evaluaciones, autorizaciones, vencimientos y evidencias. | Completar el catálogo de competencias específico por técnica y puesto. |
| 6.3 Instalaciones y condiciones ambientales | Parcial | Equipos e incidencias permiten documentar impacto. | Incorporar registros periódicos de condiciones ambientales o enlazarlos a un sistema externo validado. |
| 6.4 Equipamiento | Cubierto | Inventario de equipos, aceptación inicial, mantenimiento, calibración, QC, averías e impacto en resultados. | Cargar certificados y planes metrológicos vigentes por equipo. |
| 6.5 Reactivos y consumibles | Cubierto | Proveedores, lotes, cuarentena, liberación, caducidad, stock y movimientos auditables. | Mantener evaluación documental de proveedores y criterios de aceptación. |
| 6.6 Servicios externos y suministros | Parcial | Evaluación de proveedores en Mejora Continua y trazabilidad en Inventario. | Documentar acuerdos, criterios de selección y reevaluación de servicios críticos. |
| 6.7 Servicios de asesoramiento | Parcial | El gestor documental puede conservar guías e informes. | Definir y registrar la actividad de asesoramiento clínico cuando aplique. |
| 7.1-7.2 Procesos preanalíticos | Fuera de alcance | No es un LIS ni gestiona solicitudes/muestras de pacientes. | Gestionar mediante LIS/procedimientos o integración específica. |
| 7.3 Procesos analíticos e IQC | Parcial | IQC, reglas Westgard, equipos y EQA documentan la validez del desempeño. | La validación/verificación de métodos y estimación de incertidumbre requieren registros técnicos específicos. |
| 7.3.7 EQA | Cubierto | Programas, inscripciones, rondas, resultados, desviaciones, CAPA y verificación de eficacia. | Mantener evidencia de tratamiento de muestras EQA y criterios de desempeño. |
| 7.4 Procesos postanalíticos | Fuera de alcance | No emite ni comunica informes de resultados clínicos. | Gestionar en LIS o módulo clínico separado. |
| 7.5 Trabajo no conforme | Cubierto | No conformidades, contención, CAPA, eficacia, quejas e incidencias de equipo. | Asegurar que el procedimiento interno define comunicación y retirada de resultados cuando aplique. |
| 7.6 Control de datos y gestión de la información | Parcial | Control de acceso, documentos versionados, auditoría encadenada HMAC, copias cifradas y restauración controlada. | Validar el entorno de producción, ACL, copias externas y plan de continuidad. |
| 8.2-8.4 Documentación y control de registros | Cubierto | Documentos versionados, aprobación, firmas, historial, auditoría protegida y exportación. | Definir y aprobar tabla de retención documental. |
| 8.5 Riesgos y oportunidades | Cubierto | Matriz de riesgos, medidas, residual, revisión de eficacia y seguimiento. | Programar revisiones por proceso y cambio significativo. |
| 8.6 Mejora | Cubierto | Acciones, CAPA, quejas, riesgos y seguimiento de eficacia. | Establecer indicadores de calidad por proceso. |
| 8.7 No conformidades y acción correctiva | Cubierto | NC, análisis, contención, CAPA, verificación de eficacia y auditoría. | Formar a usuarios para documentar causa raíz y evidencia objetiva. |
| 8.8 Evaluaciones | Cubierto | Auditorías internas, hallazgos, planes y vinculación a NC. | Mantener programa anual y competencia/independencia de auditores. |
| 8.9 Revisión por la dirección | Parcial | Módulo Mejora Continua genera entradas y reportes. | Registrar acta firmada, decisiones, recursos, responsables y seguimiento de acciones. |

## Prioridades de implementación

1. **Alta**: evidencia operativa de restauración, retención y custodia de la clave HMAC; control de ACL y soporte de copia externa.
2. **Alta**: registros técnicos de validación/verificación de métodos, incertidumbre de medida e intervalos de referencia cuando QMSFlowDoc vaya a cubrirlos.
3. **Media**: registro de conflictos de interés, revisión de imparcialidad y condiciones ambientales.
4. **Media**: estructurar la revisión por dirección con entradas, decisiones y seguimiento formal.
5. **Media**: integración documentada con LIS para requisitos pre y postanalíticos que permanecen fuera del alcance del QMS.

## Evidencia para una auditoría

Antes de una auditoría, exportar desde QMSFlowDoc los registros de auditoría, revisión por dirección, riesgos, CAPA, competencia, equipos, IQC/EQA y documentos aprobados. Complementarlos con procedimientos aprobados, actas firmadas, certificados, registros del LIS y las pruebas de restauración realizadas.
