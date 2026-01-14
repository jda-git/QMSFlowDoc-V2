# CHANGELOG

Todos los cambios notables en QMSFlowDoc se documentan en este archivo.

## [1.1.0] - 2026-01-14

### Seguridad (ISO 15189 Compliance)
- **BCrypt Password Hashing**: Reemplazado SHA256 por BCrypt para el hashing seguro de contraseñas
- **Política de Contraseñas**: Implementada validación de contraseñas (mínimo 8 caracteres, 1 mayúscula, 1 número)
- **Bloqueo de Cuenta**: Bloqueo automático después de 5 intentos fallidos de login (30 minutos)
- **Autorización por Roles**: Eliminadas contraseñas hardcodeadas del código, reemplazadas por verificación de roles JWT

### Audit Trail (ISO 15189 Compliance)
- **Servicio Centralizado**: Nuevo `AuditService` con logging persistente a base de datos
- **Before/After Snapshots**: Captura de estado antes y después de cambios para trazabilidad
- **Integrity Hashing**: Hash de integridad SHA256 para cada entrada de auditoría
- **Eventos de Login**: Registro de LOGIN_OK/LOGIN_FAIL/LOGIN_BLOCKED/ACCOUNT_LOCKED

### Integridad de Datos (ISO 15189 Compliance)
- **Soft-Delete**: Implementado en StaffController (IsActive=false), AuthorizationsController (Status=REVOCADA), InventoryController (Status=OBSOLETO)
- **Preservación de Registros**: Los registros críticos ya no se eliminan físicamente

### Nuevos Endpoints API
- `POST /auth/reset-password/{userId}` - Reset de contraseña por administrador
- `POST /auth/change-password` - Cambio de contraseña por usuario
- `POST /auth/unlock/{userId}` - Desbloqueo de cuenta por administrador

### Cambios en Cliente
- `EquipmentView`: Verificación de permisos por rol en lugar de contraseña
- `InventoryView`: Verificación de permisos por rol en lugar de contraseña  
- `DocumentsView`: Verificación de permisos por rol en lugar de contraseña

### Modelos Actualizados
- `User`: Añadidos campos `FailedLoginAttempts`, `LockedUntil`, `PasswordChangedAt`
- `AuditLog`: Añadido campo `Result` para OK/FAIL

## [1.0.0] - 2025-01-XX

### Lanzamiento Inicial
- Sistema de gestión de documentos con versionado
- Gestión de personal y formación
- Inventario de reactivos
- Equipamiento y mantenimiento
- No conformidades y CAPA
