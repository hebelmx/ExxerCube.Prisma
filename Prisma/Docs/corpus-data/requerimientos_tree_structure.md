
# Estructura Jerárquica de Tipos de Requerimientos

## 1. Requerimientos de Información

    ### 1.1. Información de Cuentas
        - **Descripción:** Solicitudes para confirmar la existencia de cuentas bancarias, de ahorro o inversión.
        - **Datos Clave:** Nombre del titular, RFC/CURP, tipo de cuenta.
        - **Ejemplo:** "Informe sobre la existencia de cuentas a nombre de..."

    ### 1.2. Información de Movimientos y Saldos
        - **Descripción:** Solicitudes de estados de cuenta, historial de transacciones, o saldos a una fecha específica.
        - **Datos Clave:** Periodo de tiempo, número de cuenta (si se conoce).
        - **Ejemplo:** "Remitir los estados de cuenta de los últimos seis meses..."

    ### 1.3. Información de Créditos y Préstamos
        - **Descripción:** Solicitudes sobre la existencia y estado de créditos, préstamos o financiamientos.
        - **Datos Clave:** Nombre del acreditado, tipo de crédito, estado del adeudo.
        - **Ejemplo:** "Informen sobre la existencia de cualquier tipo de crédito o préstamo otorgado a..."

## 2. Requerimientos de Ejecución

    ### 2.1. Aseguramiento de Cuentas y Fondos
        - **Descripción:** Órdenes para bloquear o congelar fondos en cuentas bancarias.
        - **Datos Clave:** Monto a asegurar, número de cuenta (si se conoce), motivo del aseguramiento.
        - **Ejemplo:** "Se solicita el aseguramiento de todas las cuentas bancarias..."

    ### 2.2. Desbloqueo de Cuentas
        - **Descripción:** Órdenes para liberar cuentas previamente aseguradas.
        - **Datos Clave:** Folio del requerimiento de aseguramiento original, motivo del desbloqueo.
        - **Ejemplo:** "Se solicita el desbloqueo de la cuenta..."

    ### 2.3. Transferencia de Fondos
        - **Descripción:** Órdenes para transferir fondos de una cuenta a otra, usualmente a una cuenta del juzgado o de un beneficiario.
        - **Datos Clave:** Cuenta de origen, cuenta de destino, monto a transferir.
        - **Ejemplo:** "Se ordena la transferencia de fondos de la cuenta..."

## 3. Requerimientos Especiales (Nuevas Tecnologías)

    ### 3.1. Información sobre Activos Virtuales
        - **Descripción:** Solicitudes de información sobre tenencia y operaciones con criptomonedas y otros activos virtuales.
        - **Datos Clave:** Nombre del titular, CURP, tipo de activo virtual (si se conoce).
        - **Ejemplo:** "Informen sobre cualquier cuenta o contrato de cualquier naturaleza a nombre de... relacionado con activos virtuales."
